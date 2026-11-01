// Now-playing info and playback control through Windows' media controls
// (the same API the volume flyout uses): Spotify, browsers, and any other
// app that reports what it's playing.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.Media.Control;
using WF = Windows.Foundation;

namespace BTHeadUnit
{
    // The .NET Framework compiler can't `await` WinRT operations, so block on them.
    static class WinRT
    {
        public static T Wait<T>(WF.IAsyncOperation<T> op, int timeoutMs = 5000)
        {
            var done = new ManualResetEvent(false);
            op.Completed = delegate { done.Set(); };
            if (!done.WaitOne(timeoutMs)) { op.Cancel(); throw new TimeoutException("WinRT operation timed out"); }
            if (op.Status != WF.AsyncStatus.Completed) throw op.ErrorCode ?? new Exception("WinRT operation " + op.Status);
            return op.GetResults();
        }

        public static void Wait(WF.IAsyncAction op, int timeoutMs = 5000)
        {
            var done = new ManualResetEvent(false);
            op.Completed = delegate { done.Set(); };
            if (!done.WaitOne(timeoutMs)) { op.Cancel(); throw new TimeoutException("WinRT operation timed out"); }
            if (op.Status != WF.AsyncStatus.Completed) throw op.ErrorCode ?? new Exception("WinRT operation " + op.Status);
        }
    }

    class NowPlaying
    {
        public string App = "", Title = "", Artist = "", Album = "";
        public bool Playing;
        public double Position, Duration;
        public string Proc = "";   // process name the audio comes from, for volume control
    }

    class Media
    {
        GlobalSystemMediaTransportControlsSessionManager manager;
        string trackKey;
        public byte[] ArtJpeg;
        public int ArtId;

        public void Start()
        {
            manager = WinRT.Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
        }

        // Turns an app id like "Spotify.exe", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
        // "MSEdge" or "Chrome" into the process name that owns its audio.
        public static string ProcFromAppId(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return "";
            string name = appId;
            int bang = name.LastIndexOf('!');
            if (bang >= 0) name = name.Substring(bang + 1);
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            name = name.ToLowerInvariant();
            if (name == "308046b0af4a39cb") return "firefox";
            return name;
        }

        static bool IsRunningProcess(string appId)
        {
            string proc = ProcFromAppId(appId);
            return proc != "" && Process.GetProcessesByName(proc).Length > 0;
        }

        // "pc": Spotify if it has a session, else whatever Windows considers current.
        // "bt": a session that doesn't belong to a desktop app, which is how a
        // phone's Bluetooth controls would show up, if Windows exposes them.
        public GlobalSystemMediaTransportControlsSession Pick(string source)
        {
            var sessions = manager.GetSessions().ToList();
            if (source == "bt")
                // Store apps ("...!App", e.g. the Snipping Tool's recordings) and desktop apps are on this PC.
                return sessions.FirstOrDefault(s => s.SourceAppUserModelId.IndexOf('!') < 0 && !IsRunningProcess(s.SourceAppUserModelId));
            var spotify = sessions.FirstOrDefault(s => s.SourceAppUserModelId.IndexOf("spotify", StringComparison.OrdinalIgnoreCase) >= 0);
            return spotify ?? manager.GetCurrentSession();
        }

        public System.Collections.Generic.List<GlobalSystemMediaTransportControlsSession> Sessions()
        {
            return manager.GetSessions().ToList();
        }

        public NowPlaying Read(GlobalSystemMediaTransportControlsSession s)
        {
            var np = new NowPlaying();
            if (s == null)
            {
                // Nothing to show (e.g. the phone, whose songs Windows doesn't pass on): drop the
                // last cover so the head unit shows its placeholder instead of the PC's old one.
                trackKey = null;
                if (ArtJpeg != null) { ArtJpeg = null; ArtId++; }
                return np;
            }
            np.Proc = ProcFromAppId(s.SourceAppUserModelId);
            np.App = np.Proc.Length > 0 ? char.ToUpper(np.Proc[0]) + np.Proc.Substring(1) : "";
            var props = WinRT.Wait(s.TryGetMediaPropertiesAsync(), 2000);
            np.Title = props.Title ?? "";
            np.Artist = props.Artist ?? "";
            np.Album = props.AlbumTitle ?? "";
            np.Playing = s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            var tl = s.GetTimelineProperties();
            np.Duration = (tl.EndTime - tl.StartTime).TotalSeconds;
            np.Position = tl.Position.TotalSeconds;
            if (np.Playing && tl.LastUpdatedTime.Year > 2000)
                np.Position += (DateTimeOffset.Now - tl.LastUpdatedTime).TotalSeconds;
            if (np.Duration > 0) np.Position = Math.Max(0, Math.Min(np.Duration, np.Position));

            string key = np.Title + "|" + np.Artist + "|" + np.Album;
            if (key != trackKey)
            {
                trackKey = key;
                ArtJpeg = null;
                try { if (props.Thumbnail != null) ArtJpeg = LoadArt(props.Thumbnail); }
                catch (Exception e) { Program.Log("Couldn't read album art: " + e.Message); }
                ArtId++;
            }
            return np;
        }

        public void Command(GlobalSystemMediaTransportControlsSession s, string cmd)
        {
            if (s == null) return;
            switch (cmd)
            {
                case "toggle": WinRT.Wait(s.TryTogglePlayPauseAsync()); break;
                case "play": WinRT.Wait(s.TryPlayAsync()); break;
                case "pause": WinRT.Wait(s.TryPauseAsync()); break;
                case "next": WinRT.Wait(s.TrySkipNextAsync()); break;
                case "prev": WinRT.Wait(s.TrySkipPreviousAsync()); break;
            }
        }

        static byte[] LoadArt(Windows.Storage.Streams.IRandomAccessStreamReference reference)
        {
            using (var stream = WinRT.Wait(reference.OpenReadAsync(), 3000))
            {
                var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
                uint size = WinRT.Wait<uint>(reader.LoadAsync((uint)stream.Size), 3000);
                var bytes = new byte[size];
                reader.ReadBytes(bytes);
                return SquareJpeg(bytes);
            }
        }

        // Album art, center-cropped to a 200px JPEG so it fits in one UDP packet.
        public static byte[] SquareJpeg(byte[] bytes)
        {
            using (var input = new MemoryStream(bytes))
            using (var img = Image.FromStream(input))
            using (var bmp = new Bitmap(200, 200))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    int side = Math.Min(img.Width, img.Height);
                    var src = new Rectangle((img.Width - side) / 2, (img.Height - side) / 2, side, side);
                    g.DrawImage(img, new Rectangle(0, 0, 200, 200), src, GraphicsUnit.Pixel);
                }
                var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                var p = new EncoderParameters(1);
                p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                using (var output = new MemoryStream())
                {
                    bmp.Save(output, jpeg, p);
                    return output.ToArray();
                }
            }
        }
    }
}
