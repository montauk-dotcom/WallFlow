using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Wallflow;

/// <summary>
/// Volume through Windows Core Audio: the volume of one app (as in the Volume Mixer) when its audio session
/// can be found, otherwise the system volume.
/// </summary>
public static class AudioControl
{
    static Guid _ctx = Guid.Empty;

    // ---------------- system volume ----------------
    static IAudioEndpointVolume? Endpoint()
    {
        try
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (en.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out var dev) != 0 || dev == null) return null;
            var iid = typeof(IAudioEndpointVolume).GUID;
            if (dev.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out var o) != 0) return null;
            return o as IAudioEndpointVolume;
        }
        catch { return null; }
    }

    public static (float volume, bool mute)? GetSystem()
    {
        var ep = Endpoint();
        if (ep == null) return null;
        ep.GetMasterVolumeLevelScalar(out var v);
        ep.GetMute(out var m);
        return (v, m);
    }

    public static void SetSystem(float v) { var ep = Endpoint(); ep?.SetMasterVolumeLevelScalar(Math.Clamp(v, 0, 1), ref _ctx); }
    public static void SetSystemMute(bool m) { var ep = Endpoint(); ep?.SetMute(m, ref _ctx); }

    // ---------------- per-app volume ----------------
    /// <summary>Finds the audio sessions of the app behind a media session (e.g. "chrome", "spotify").</summary>
    static ISimpleAudioVolume[] AppVolumes(string appKey)
    {
        var list = new System.Collections.Generic.List<ISimpleAudioVolume>();
        if (string.IsNullOrEmpty(appKey)) return list.ToArray();
        try
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (en.GetDefaultAudioEndpoint(0, 1, out var dev) != 0 || dev == null) return list.ToArray();
            var iid = typeof(IAudioSessionManager2).GUID;
            if (dev.Activate(ref iid, 23, IntPtr.Zero, out var o) != 0 || o is not IAudioSessionManager2 mgr) return list.ToArray();
            if (mgr.GetSessionEnumerator(out var sessions) != 0 || sessions == null) return list.ToArray();
            sessions.GetCount(out int n);
            for (int i = 0; i < n; i++)
            {
                if (sessions.GetSession(i, out var ctl) != 0 || ctl is not IAudioSessionControl2 c2) continue;
                c2.GetProcessId(out uint pid);
                if (pid == 0) continue;
                string name;
                try { using var p = Process.GetProcessById((int)pid); name = p.ProcessName.ToLowerInvariant(); }
                catch { continue; }
                if (name == appKey || name.Contains(appKey) || appKey.Contains(name))
                    if (ctl is ISimpleAudioVolume vol) list.Add(vol);
            }
        }
        catch { }
        return list.ToArray();
    }

    /// <summary>Process name to look for, from the media app id Windows reports.</summary>
    public static string AppKey(string aumid)
    {
        var a = (aumid ?? "").ToLowerInvariant();
        if (a.Contains("spotify")) return "spotify";
        if (a.Contains("chrome")) return "chrome";
        if (a.Contains("msedge") || a.Contains("edge")) return "msedge";
        if (a.Contains("firefox") || a == "308046b0af4a39cb") return "firefox";
        if (a.Contains("opera")) return "opera";
        if (a.Contains("yandex")) return "browser";
        if (a.Contains("telegram")) return "telegram";
        if (a.Contains("vlc")) return "vlc";
        if (a.Contains("zunemusic")) return "microsoft.media.player";
        var name = a.Split('!')[0].Split('_')[0];
        if (name.EndsWith(".exe")) name = name[..^4];
        var dot = name.LastIndexOf('.');
        return dot >= 0 && dot < name.Length - 1 ? name[(dot + 1)..] : name;
    }

    public static (float volume, bool mute)? GetApp(string appKey)
    {
        var v = AppVolumes(appKey);
        if (v.Length == 0) return null;
        v[0].GetMasterVolume(out var vol);
        v[0].GetMute(out var m);
        return (vol, m);
    }

    public static bool SetApp(string appKey, float v)
    {
        var vols = AppVolumes(appKey);
        foreach (var x in vols) x.SetMasterVolume(Math.Clamp(v, 0, 1), ref _ctx);
        return vols.Length > 0;
    }

    public static bool SetAppMute(string appKey, bool m)
    {
        var vols = AppVolumes(appKey);
        foreach (var x in vols) x.SetMute(m, ref _ctx);
        return vols.Length > 0;
    }

    // ---------------- COM ----------------
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr n);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr n);
        [PreserveSig] int GetChannelCount(out int c);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float v, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float v);
        [PreserveSig] int SetChannelVolumeLevel(uint ch, float db, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float v, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint ch, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float v);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool m, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool m);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr guid, int flags, out IntPtr ctl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, int flags, out IntPtr vol);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator e);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int n);
        [PreserveSig] int GetSession(int i, [MarshalAs(UnmanagedType.IUnknown)] out object ctl);
    }

    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        // IAudioSessionControl
        [PreserveSig] int GetState(out int s);
        [PreserveSig] int GetDisplayName(out IntPtr n);
        [PreserveSig] int SetDisplayName(IntPtr n, ref Guid ctx);
        [PreserveSig] int GetIconPath(out IntPtr p);
        [PreserveSig] int SetIconPath(IntPtr p, ref Guid ctx);
        [PreserveSig] int GetGroupingParam(out Guid g);
        [PreserveSig] int SetGroupingParam(ref Guid g, ref Guid ctx);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr n);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr n);
        // IAudioSessionControl2
        [PreserveSig] int GetSessionIdentifier(out IntPtr id);
        [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
        [PreserveSig] int GetProcessId(out uint pid);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float v, ref Guid ctx);
        [PreserveSig] int GetMasterVolume(out float v);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool m, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool m);
    }
}
