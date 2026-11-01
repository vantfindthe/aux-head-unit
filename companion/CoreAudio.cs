// Minimal Core Audio (WASAPI session) interop: enumerate per-app audio sessions
// on every active output device and get/set their mixer volume.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BTHeadUnit
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCo { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int streamFlags, out IntPtr sessionControl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int streamFlags, out IntPtr audioVolume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl2 session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        // IAudioSessionControl
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName(IntPtr value, IntPtr eventContext);
        [PreserveSig] int GetIconPath(IntPtr path);
        [PreserveSig] int SetIconPath(IntPtr value, IntPtr eventContext);
        [PreserveSig] int GetGroupingParam(IntPtr groupingParam);
        [PreserveSig] int SetGroupingParam(IntPtr groupingParam, IntPtr eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
        // IAudioSessionControl2
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute(int mute, ref Guid eventContext);
        [PreserveSig] int GetMute(out int mute);
    }

    class AudioSession
    {
        public string Key;      // session instance id (unique per stream)
        public uint Pid;
        public string Proc;     // lower-case process name, "" if unknown
        public bool Active;     // currently playing sound
        public ISimpleAudioVolume Volume;

        public float Get()
        {
            float v;
            return Volume.GetMasterVolume(out v) == 0 ? v : -1f;
        }

        public void Set(float v)
        {
            Guid ctx = CoreAudio.EventContext;
            Volume.SetMasterVolume(Math.Max(0f, Math.Min(1f, v)), ref ctx);
        }

        public bool Muted()
        {
            int m;
            return Volume.GetMute(out m) == 0 && m != 0;
        }

        public void SetMuted(bool muted)
        {
            Guid ctx = CoreAudio.EventContext;
            Volume.SetMute(muted ? 1 : 0, ref ctx);
        }
    }

    static class CoreAudio
    {
        // Tags our own volume changes so they're distinguishable in session events.
        public static Guid EventContext = new Guid("5B1E7C1D-3C0B-4D4B-9C5E-6A1B8D2F0E11");

        static readonly Dictionary<uint, string> procNames = new Dictionary<uint, string>();

        public static string ProcName(uint pid)
        {
            string name;
            if (procNames.TryGetValue(pid, out name)) return name;
            try { name = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); }
            catch { name = ""; }
            procNames[pid] = name;
            return name;
        }

        public static bool IncludeSystemSounds;   // --list --all

        public static List<AudioSession> GetSessions()
        {
            var list = new List<AudioSession>();
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCo();
            IMMDeviceCollection devices;
            if (enumerator.EnumAudioEndpoints(0 /*eRender*/, 1 /*DEVICE_STATE_ACTIVE*/, out devices) != 0) return list;
            uint count;
            devices.GetCount(out count);
            for (uint i = 0; i < count; i++)
            {
                IMMDevice device;
                if (devices.Item(i, out device) != 0) continue;
                Guid iid = typeof(IAudioSessionManager2).GUID;
                object o;
                if (device.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out o) != 0) continue;
                IAudioSessionEnumerator sessions;
                if (((IAudioSessionManager2)o).GetSessionEnumerator(out sessions) != 0) continue;
                int n;
                sessions.GetCount(out n);
                for (int k = 0; k < n; k++)
                {
                    IAudioSessionControl2 ctl;
                    if (sessions.GetSession(k, out ctl) != 0) continue;
                    bool system = ctl.IsSystemSoundsSession() == 0; // S_OK means "is system sounds"
                    if (system && !IncludeSystemSounds) continue;
                    uint pid;
                    ctl.GetProcessId(out pid);
                    string key;
                    ctl.GetSessionInstanceIdentifier(out key);
                    int state;
                    ctl.GetState(out state);
                    list.Add(new AudioSession { Key = key ?? "", Pid = pid, Proc = system ? "(system sounds)" : ProcName(pid), Active = state == 1, Volume = (ISimpleAudioVolume)ctl });
                }
            }
            return list;
        }
    }
}
