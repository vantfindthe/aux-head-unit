// Lets a paired phone stream audio to this PC (Windows 10 2004+), the same
// thing the "Bluetooth Audio Receiver" app does: the PC acts as a speaker.
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace BTHeadUnit
{
    class BtDevice
    {
        public string Id, Name;
    }

    class Bluetooth
    {
        readonly object sync = new object();
        readonly Dictionary<string, string> devices = new Dictionary<string, string>();
        DeviceWatcher watcher;
        AudioPlaybackConnection connection;
        string connectedId;
        public string ConnectedName = "";
        public string Status = "Not connected";
        public string AutoConnectId;   // last device used; reconnects when it shows up
        public event Action Connecting;   // fires before the connection opens
        public static bool Verbose;        // log full exceptions (--bttest)

        public void Start()
        {
            watcher = DeviceInformation.CreateWatcher(AudioPlaybackConnection.GetDeviceSelector());
            watcher.Added += (w, d) =>
            {
                lock (sync) devices[d.Id] = d.Name;
                if (d.Id == AutoConnectId && connection == null)
                    System.Threading.ThreadPool.QueueUserWorkItem(_ => Connect(d.Id));
            };
            watcher.Removed += (w, u) => { lock (sync) devices.Remove(u.Id); };
            watcher.Updated += (w, u) => { };
            watcher.Start();
            // The phone may not be ready the first time (asleep, out of range, just disconnected): keep trying.
            retry = new System.Threading.Timer(_ => AutoConnect(), null, 20000, 20000);
        }

        System.Threading.Timer retry;
        int busy;

        void AutoConnect()
        {
            string id = AutoConnectId;
            if (id == null || IsConnected) return;
            lock (sync) if (!devices.ContainsKey(id)) return;
            Connect(id, true);
        }

        public List<BtDevice> Devices()
        {
            lock (sync) return devices.Select(kv => new BtDevice { Id = kv.Key, Name = kv.Value }).OrderBy(d => d.Name).ToList();
        }

        // quiet: an automatic retry, so don't fill the window with repeated attempts.
        public void Connect(string id, bool quiet = false)
        {
            if (System.Threading.Interlocked.Exchange(ref busy, 1) == 1) return;
            try { DoConnect(id, quiet); }
            finally { busy = 0; }
        }

        void DoConnect(string id, bool quiet)
        {
            string name;
            lock (sync) if (!devices.TryGetValue(id, out name)) name = "device";
            Disconnect();
            Status = "Connecting to " + name + "...";
            if (!quiet) Program.Log(Status);
            var connecting = Connecting;
            if (connecting != null) connecting();
            try
            {
                var c = AudioPlaybackConnection.TryCreateFromId(id);
                if (c == null) { Status = "Can't connect to " + name; if (!quiet) Program.Log(Status); return; }
                c.StateChanged += (s, a) =>
                {
                    if (s.State == AudioPlaybackConnectionState.Closed && connection == s)
                    {
                        Status = name + " disconnected";
                        Program.Log(Status);
                    }
                };
                WinRT.Wait(c.StartAsync());
                var result = WinRT.Wait(c.OpenAsync(), 15000);
                if (result.Status != AudioPlaybackConnectionOpenResultStatus.Success)
                {
                    Status = "Couldn't connect to " + name + " (" + result.Status + ")";
                    if (result.Status == AudioPlaybackConnectionOpenResultStatus.UnknownFailure || result.Status == AudioPlaybackConnectionOpenResultStatus.RequestTimedOut)
                        Status += ": turn on the phone's Bluetooth, unlock it and keep it nearby";
                    if (!quiet) Program.Log(Status);
                    c.Dispose();
                    return;
                }
                connection = c;
                connectedId = id;
                ConnectedName = name;
                AutoConnectId = id;
                Status = "Connected to " + name;
                Program.Log(Status + ". Play something on the phone.");
            }
            catch (Exception e)
            {
                Status = "Couldn't connect to " + name + ": " + e.Message;
                if (!quiet) Program.Log(Status);
                if (Verbose) Program.Log(e.ToString());
            }
        }

        public void Disconnect()
        {
            var c = connection;
            if (c == null) return;
            connection = null;
            connectedId = null;
            ConnectedName = "";
            Status = "Not connected";
            try { c.Dispose(); } catch { }
        }

        public bool IsConnected { get { return connection != null && connection.State == AudioPlaybackConnectionState.Opened; } }
        public string ConnectedId { get { return connectedId; } }
    }
}
