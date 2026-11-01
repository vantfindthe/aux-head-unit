// Aux Head Unit companion for BeamNG.drive.
//
// Talks to the btHeadUnit mod over localhost UDP:
//   game  -> 127.0.0.1:47811  {"t":"state", ...} 20x/s, {"t":"cmd"}, {"t":"bt"}, {"t":"bye"}
//   us    -> 127.0.0.1:47812  {"t":"np", ...} 4x/s, {"t":"art", ...} when the game lacks the current cover
//
// The music itself never goes through the game: we follow the car's state
// (ignition, head unit power and volume, camera in or out of the car) by
// adjusting the playing app's volume in the Windows mixer.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Windows.Media.Control;

namespace BTHeadUnit
{
    static partial class Program
    {
        const int ListenPort = 47811;
        const int GamePort = 47812;
        const int LinkTimeoutMs = 2500;

        static readonly object sync = new object();
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        static readonly string statePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "state.json");

        // Latest car state from the game (guarded by sync)
        static long lastStateMs = -100000;
        static bool power = true, ignition = true, hasCar, inside;
        static double distance, volume = 0.8;
        static double speakerMax = 1;   // the car's speakers part: how loud they go (bass/treble need the sound to pass through us, not done yet)
        static string source = "pc", outsideMode = "realistic";
        static int gameArtId = -1;
        static string pcProc = "";   // process name of the PC media app, e.g. "spotify"

        static UdpClient udp;
        static readonly Media media = new Media();
        static readonly Bluetooth bt = new Bluetooth();
        static readonly ConcurrentQueue<string> mediaCommands = new ConcurrentQueue<string>();
        static readonly BlockingCollection<Dictionary<string, object>> btOps = new BlockingCollection<Dictionary<string, object>>();
        static readonly ManualResetEvent quitEvent = new ManualResetEvent(false);
        static volatile bool quit;
        static volatile bool btBaselineRequested;
        static Thread audioThread;
        static Dictionary<string, object> savedState = new Dictionary<string, object>();
        static readonly Dictionary<string, float> savedVolumes = new Dictionary<string, float>();

        // The Pro companion's song info, album art and player controls live in Pro.cs, which
        // isn't public. Without it these do nothing and this builds the free companion.
        static bool Pro = false;
        static partial void ProInit();
        static partial void ProPickPc(GlobalSystemMediaTransportControlsSession pcSession, ref string proc);
        static partial void ProCommand(string src, GlobalSystemMediaTransportControlsSession active, string cmd, ref bool done);
        static partial void ProNowPlaying(Dictionary<string, object> msg, NowPlaying np);
        static partial void ProSendArt(int haveArt);

        public static void Log(string msg)
        {
            lock (Console.Out) Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + msg);
        }

        [MTAThread] // blocking on WinRT/COM calls is only safe off an STA
        static int Main(string[] args)
        {
            if (args.Contains("--list"))
            {
                // Diagnostics: every app audio stream and its mixer volume (--all: with system sounds and ids).
                bool all = args.Contains("--all");
                CoreAudio.IncludeSystemSounds = all;
                foreach (var s in CoreAudio.GetSessions())
                    Console.WriteLine("{0,-20} pid {1,-6} volume {2:0.00}{3}{4}", s.Proc == "" ? "?" : s.Proc, s.Pid, s.Get(),
                        all ? (s.Active ? "  PLAYING" : "  idle   ") : "", all ? "  " + s.Key : "");
                return 0;
            }
            if (args.Contains("--media"))
            {
                // Diagnostics: every media session Windows knows about and what it reports.
                var m = new Media();
                m.Start();
                foreach (var s in m.Sessions())
                {
                    var np = m.Read(s);
                    Console.WriteLine("{0} | title '{1}' artist '{2}' album '{3}' playing {4} pos {5:0}/{6:0}",
                        s.SourceAppUserModelId, np.Title, np.Artist, np.Album, np.Playing, np.Position, np.Duration);
                }
                var phone = m.Pick("bt");
                Console.WriteLine("bt pick: " + (phone == null ? "none" : phone.SourceAppUserModelId));
                return 0;
            }
            if (args.Contains("--bttest"))
            {
                // Diagnostics: connect to the first paired phone and report what happens.
                Bluetooth.Verbose = true;
                var test = new Bluetooth();
                test.Start();
                Thread.Sleep(3000);
                var phone = test.Devices().FirstOrDefault();
                if (phone == null) { Log("No phones found."); return 1; }
                test.Connect(phone.Id);
                Thread.Sleep(3000);
                Log("Connected: " + test.IsConnected + ", status: " + test.Status);
                test.Disconnect();
                return 0;
            }

            ProInit();
            Console.Title = Pro ? "Aux Head Unit Pro companion" : "Aux Head Unit companion";
            CoreAudio.IncludeSystemSounds = true;   // the phone's stream may be filed under system audio
            Log((Pro ? "Aux Head Unit Pro" : "Aux Head Unit") + " companion for BeamNG.drive - keep this window open while you play.");
            Log("Close it (or press Ctrl+C) to stop; app volumes are put back when it exits.");

            LoadState();

            try { udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, ListenPort)); }
            catch (SocketException)
            {
                Log("Port " + ListenPort + " is already in use. Is the companion already running?");
                Thread.Sleep(5000);
                return 1;
            }
            // Don't let "port unreachable" from a closed game socket break Receive() (SIO_UDP_CONNRESET off)
            udp.Client.IOControl(-1744830452, new byte[] { 0 }, null);

            try { media.Start(); }
            catch (Exception e) { Log("Windows media controls unavailable: " + e.Message); }

            bt.Connecting += () => btBaselineRequested = true;
            try { bt.Start(); }
            catch (Exception e) { Log("Bluetooth audio receiving isn't available on this PC: " + e.Message); }

            audioThread = StartThread(AudioLoop, "audio");
            StartThread(MediaLoop, "media");
            StartThread(LinkLoop, "link");
            StartThread(BluetoothLoop, "bluetooth");

            Console.CancelKeyPress += (s, e) => { e.Cancel = true; quitEvent.Set(); };
            ctrlHandler = type => { quitEvent.Set(); Shutdown(); return true; };
            SetConsoleCtrlHandler(ctrlHandler, true);

            Log("Waiting for BeamNG.drive...");
            quitEvent.WaitOne();
            Shutdown();
            return 0;
        }

        static Thread StartThread(ThreadStart body, string name)
        {
            var t = new Thread(() =>
            {
                try { body(); }
                catch (Exception e) { Log("The " + name + " worker stopped: " + e); }
            });
            t.Name = name;
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
            return t;
        }

        static int shutdownDone;
        static void Shutdown()
        {
            if (Interlocked.Exchange(ref shutdownDone, 1) == 1) return;
            quit = true;
            if (audioThread != null) audioThread.Join(3000);   // restores app volumes on its way out
            bt.Disconnect();
            SaveState();
            try { udp.Close(); } catch { }
        }

        // ---------------------------------------------------------------- game link

        static void LinkLoop()
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (!quit)
            {
                byte[] data;
                try { data = udp.Receive(ref from); }
                catch (Exception) { if (quit) return; continue; }

                Dictionary<string, object> m;
                try { m = json.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(data)); }
                catch { continue; }
                if (m == null) continue;

                switch (Str(m, "t"))
                {
                    case "state":
                        lock (sync)
                        {
                            if (clock.ElapsedMilliseconds - lastStateMs > LinkTimeoutMs) Log("BeamNG.drive connected.");
                            lastStateMs = clock.ElapsedMilliseconds;
                            power = Bool(m, "power", true);
                            ignition = Bool(m, "ign", true);
                            hasCar = Bool(m, "hasCar", false);
                            inside = Bool(m, "inside", false);
                            distance = Num(m, "dist", 0);
                            volume = Math.Max(0, Math.Min(1, Num(m, "vol", 0.8)));
                            speakerMax = Math.Max(0, Num(m, "spkMax", 1));
                            source = Str(m, "src") == "bt" ? "bt" : "pc";
                            outsideMode = Str(m, "outside") == "full" ? "full" : "realistic";
                            gameArtId = (int)Num(m, "haveArt", -1);
                        }
                        break;
                    case "cmd":
                        mediaCommands.Enqueue(Str(m, "c"));
                        break;
                    case "bt":
                        btOps.Add(m);
                        break;
                    case "bye":
                        lock (sync) lastStateMs = -100000;
                        Log("BeamNG.drive closed the head unit.");
                        break;
                }
            }
        }

        // JavaScriptSerializer writes ' & < > as '-style escapes, which the
        // game's JSON decoder mangles (Bluetooth device ids contain '&'). JSON
        // doesn't need them escaped, so put the plain characters back.
        static readonly Regex htmlEscapes = new Regex(@"(?<!\\)((?:\\\\)*)\\u00(27|26|3c|3e)", RegexOptions.IgnoreCase);

        static void Send(Dictionary<string, object> msg)
        {
            string text = htmlEscapes.Replace(json.Serialize(msg), m => m.Groups[1].Value + (char)Convert.ToInt32(m.Groups[2].Value, 16));
            var bytes = Encoding.UTF8.GetBytes(text);
            try { lock (udp) udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, GamePort)); }
            catch (Exception) { }
        }

        static string Cap(string s)
        {
            return s.Length > 200 ? s.Substring(0, 200) : s;
        }

        // ---------------------------------------------------------------- media

        static void MediaLoop()
        {
            string lastError = null;
            while (!quit)
            {
                string src;
                int haveArt;
                bool linked;
                lock (sync) { src = source; haveArt = gameArtId; linked = clock.ElapsedMilliseconds - lastStateMs < LinkTimeoutMs; }

                var np = new NowPlaying();
                try
                {
                    var pcSession = media.Pick("pc");
                    string proc = pcSession != null ? Media.ProcFromAppId(pcSession.SourceAppUserModelId) : "";
                    ProPickPc(pcSession, ref proc);   // Pro: another player (VLC) can take the PC source
                    lock (sync) pcProc = proc;
                    var active = src == "bt" ? media.Pick("bt") : pcSession;

                    string cmd;
                    while (mediaCommands.TryDequeue(out cmd))
                    {
                        // pausePc: the head unit switched to the phone, so stop the PC player.
                        // play/pause come with the head unit's power; play/pause/skip buttons are Pro.
                        try
                        {
                            bool done = false;
                            ProCommand(src, active, cmd, ref done);
                            if (done) continue;
                            if (cmd == "pausePc") media.Command(pcSession, "pause");
                            else if (cmd == "play" || cmd == "pause") media.Command(active, cmd);
                        }
                        catch (Exception e) { Log("Couldn't send '" + cmd + "' to the player: " + e.Message); }
                    }
                    np = media.Read(active);
                    lastError = null;
                }
                catch (Exception e)
                {
                    if (e.Message != lastError) Log("Couldn't read what's playing: " + e.Message);
                    lastError = e.Message;
                }

                // "tier" tells the mod whether to show song info and player controls.
                var devices = bt.Devices().Take(8).Select(d => (object)new Dictionary<string, object> { { "id", d.Id }, { "name", Cap(d.Name) } }).ToList();
                var msg = new Dictionary<string, object>
                {
                    { "t", "np" }, { "tier", Pro ? "pro" : "free" }, { "src", src }, { "app", Cap(np.App) }, { "playing", np.Playing },
                    { "bt", new Dictionary<string, object> {
                        { "devices", devices }, { "connected", bt.IsConnected }, { "name", bt.ConnectedName }, { "status", bt.Status } } },
                };
                ProNowPlaying(msg, np);
                if (linked)
                {
                    Send(msg);
                    ProSendArt(haveArt);
                }
                Thread.Sleep(250);
            }
        }

        // ---------------------------------------------------------------- bluetooth

        static void BluetoothLoop()
        {
            foreach (var op in btOps.GetConsumingEnumerable())
            {
                if (quit) return;
                if (Str(op, "c") == "connect") bt.Connect(Str(op, "id"));
                else if (Str(op, "c") == "disconnect") { bt.Disconnect(); bt.AutoConnectId = null; Log("Bluetooth disconnected."); }
                SaveState();
            }
        }

        // ---------------------------------------------------------------- volume

        // The app's mixer volume (0..1): with stock speakers the head unit's volume is what the
        // Windows mixer shows; the car's speakers scale it (939 Guys top out at 65). The mixer
        // can't go past 100, so speakers rated louder than that just reach 100 sooner.
        static double TargetGain()
        {
            if (!power || !ignition || !hasCar) return 0;
            double full = Math.Min(1, volume * speakerMax);
            if (inside || outsideMode == "full") return full;
            if (distance > 80) return 0;
            // Standing next to the car with the doors shut: roughly half as loud, fading with distance.
            return full * 0.5 / (1 + distance / 6.0);
        }

        static bool IsSystemProcess(AudioSession s)
        {
            return s.Pid == 0 || s.Pid == (uint)Process.GetCurrentProcess().Id || s.Proc == "(system sounds)"
                || s.Proc == "" || s.Proc == "svchost" || s.Proc == "audiodg" || s.Proc == "system";
        }

        static void AudioLoop()
        {
            var original = new Dictionary<string, float>();   // session -> its mixer volume before we touched it
            var lastSet = new Dictionary<string, float>();
            var unmuted = new HashSet<string>();               // sessions that were muted in the mixer when we took them
            var btSessions = new HashSet<string>();
            var seen = new HashSet<string>();
            HashSet<string> baseline = null;
            var sessions = new List<AudioSession>();
            long lastScan = -100000;
            double gainPc = -1, gainBt = -1;   // -1: not driving yet, start from the app's own volume
            bool wasLinked = false;
            string lastScanError = null;

            RestoreSavedVolumes();

            while (true)
            {
                long now = clock.ElapsedMilliseconds;
                if (quit || now - lastScan > 1000)
                {
                    try { sessions = CoreAudio.GetSessions(); lastScanError = null; }
                    catch (Exception e)
                    {
                        if (e.Message != lastScanError) Log("Couldn't read the Windows mixer: " + e.Message);
                        lastScanError = e.Message;
                    }
                    lastScan = now;
                    if (btBaselineRequested)
                    {
                        baseline = new HashSet<string>(sessions.Select(s => s.Key));
                        btBaselineRequested = false;
                    }
                    foreach (var s in sessions)
                    {
                        if (!seen.Add(s.Key)) continue;
                        if (baseline != null && !baseline.Contains(s.Key) && bt.IsConnected && IsSystemProcess(s))
                        {
                            btSessions.Add(s.Key);
                            Log("Found the phone's audio stream (" + (s.Proc == "" ? "pid " + s.Pid : s.Proc) + ").");
                        }
                        else if (bt.IsConnected)
                            Log("New audio stream from " + (s.Proc == "" ? "pid " + s.Pid : s.Proc) + ".");
                    }
                }

                bool linked;
                double target;
                string src, pc;
                lock (sync)
                {
                    linked = !quit && now - lastStateMs < LinkTimeoutMs;
                    target = TargetGain();
                    src = source;
                    pc = pcProc;
                }
                if (!linked && wasLinked && !quit) Log("Lost BeamNG.drive; putting app volumes back.");
                wasLinked = linked;

                // Smooth so changes sound like fades rather than jumps.
                if (!linked) gainPc = gainBt = -1;
                if (gainPc >= 0) gainPc += ((src == "pc" ? target : 0) - gainPc) * 0.3;
                if (gainBt >= 0) gainBt += ((src == "bt" ? target : 0) - gainBt) * 0.3;

                var controlled = new HashSet<string>();
                if (linked)
                {
                    foreach (var s in sessions)
                    {
                        // The phone's stream is played by a Windows service (svchost); its session can
                        // outlive a reconnect, so recognise it by that too, not only by being new.
                        bool isBt = btSessions.Contains(s.Key) || (bt.IsConnected && s.Proc == "svchost");
                        bool isPc = !isBt && pc != "" && s.Proc == pc;
                        if (!isBt && !isPc) continue;
                        try
                        {
                            float orig;
                            if (!original.TryGetValue(s.Key, out orig))
                            {
                                orig = s.Get();
                                if (orig < 0) continue;
                                original[s.Key] = orig;
                                lock (savedVolumes) savedVolumes[s.Proc] = orig;
                                SaveState();
                                // A stream muted in the Windows mixer stays silent whatever its volume,
                                // so the head unit takes over the mute too while it drives the stream.
                                if (s.Muted())
                                {
                                    s.SetMuted(false);
                                    unmuted.Add(s.Key);
                                    Log("Unmuted " + (isBt ? "the phone's audio" : s.Proc) + " in the Windows mixer; it's muted again when the head unit lets go.");
                                }
                            }
                            controlled.Add(s.Key);
                            if (isBt && gainBt < 0) gainBt = orig;
                            if (!isBt && gainPc < 0) gainPc = orig;
                            float want = (float)Math.Max(0, Math.Min(1, isBt ? gainBt : gainPc));
                            float prev;
                            if (!lastSet.TryGetValue(s.Key, out prev) || Math.Abs(prev - want) > 0.002f)
                            {
                                s.Set(want);
                                lastSet[s.Key] = want;
                            }
                        }
                        catch (Exception) { }
                    }
                }

                // Give back any session we're no longer driving (game gone, app switched, quitting).
                foreach (var key in original.Keys.Where(k => !controlled.Contains(k)).ToList())
                {
                    var s = sessions.FirstOrDefault(x => x.Key == key);
                    try
                    {
                        if (s != null)
                        {
                            s.Set(original[key]);
                            if (unmuted.Contains(key)) s.SetMuted(true);
                        }
                    }
                    catch (Exception) { }
                    original.Remove(key);
                    lastSet.Remove(key);
                    unmuted.Remove(key);
                }
                if (original.Count == 0 && savedVolumes.Count > 0)
                {
                    lock (savedVolumes) savedVolumes.Clear();
                    SaveState();
                }

                if (quit) return;
                Thread.Sleep(50);
            }
        }

        // If we crashed last time, apps may still be turned down in the mixer.
        static void RestoreSavedVolumes()
        {
            if (savedVolumes.Count == 0) return;
            try
            {
                foreach (var s in CoreAudio.GetSessions())
                {
                    float v;
                    if (savedVolumes.TryGetValue(s.Proc, out v)) s.Set(v);
                }
                Log("Put back app volumes left over from last time.");
            }
            catch (Exception) { }
            savedVolumes.Clear();
            SaveState();
        }

        // ---------------------------------------------------------------- state file

        static void LoadState()
        {
            try
            {
                if (!File.Exists(statePath)) return;
                savedState = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(statePath)) ?? new Dictionary<string, object>();
                bt.AutoConnectId = Str(savedState, "autoConnect");
                if (bt.AutoConnectId == "") bt.AutoConnectId = null;
                object vols;
                if (savedState.TryGetValue("volumes", out vols) && vols is Dictionary<string, object>)
                    foreach (var kv in (Dictionary<string, object>)vols)
                        savedVolumes[kv.Key] = (float)Convert.ToDouble(kv.Value);
            }
            catch (Exception e) { Log("Ignoring unreadable state.json: " + e.Message); }
        }

        static void SaveState()
        {
            try
            {
                Dictionary<string, object> vols;
                lock (savedVolumes) vols = savedVolumes.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                var state = new Dictionary<string, object> { { "autoConnect", bt.AutoConnectId ?? "" }, { "volumes", vols } };
                lock (statePath) File.WriteAllText(statePath, json.Serialize(state));
            }
            catch (Exception) { }
        }

        // ---------------------------------------------------------------- helpers

        static string Str(Dictionary<string, object> m, string k)
        {
            object v;
            return m.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        static bool Bool(Dictionary<string, object> m, string k, bool def)
        {
            object v;
            return m.TryGetValue(k, out v) && v is bool ? (bool)v : def;
        }

        static double Num(Dictionary<string, object> m, string k, double def)
        {
            object v;
            if (!m.TryGetValue(k, out v) || v == null) return def;
            try { return Convert.ToDouble(v); } catch { return def; }
        }

        delegate bool ConsoleCtrlHandler(int ctrlType);
        static ConsoleCtrlHandler ctrlHandler;   // kept in a field so the GC doesn't collect it

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handler, bool add);
    }
}
