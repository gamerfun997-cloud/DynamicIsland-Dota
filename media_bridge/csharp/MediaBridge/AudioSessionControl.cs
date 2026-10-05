using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MediaBridge;

public sealed class SessionEntry
{
    public int Index;
    public uint Pid;
    public string ProcessName = "";
    public string DisplayName = "";
    public string IconPath = "";
    public string Family = "";
    public int State;
    public float Volume;
}

public static class AppAudioControl
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr handle, int flags, StringBuilder name, ref int size);

    public static string LastTarget = "none";
    public static string LastError = "";

    private static volatile bool _isDotaFocused;
    private static Thread? _watcherThread;
    private static bool _started;
    private static readonly object _focusLock = new();

    public static void StartFocusWatcher()
    {
        lock (_focusLock)
        {
            if (_started) return;
            _started = true;
            _watcherThread = new Thread(FocusLoop)
            {
                IsBackground = true,
                Name = "DotaFocusWatcher"
            };
            _watcherThread.SetApartmentState(ApartmentState.STA);
            _watcherThread.Start();
        }
    }

    private static void FocusLoop()
    {
        try
        {
            IntPtr hDesk = OpenDesktop("default", 0, false, 0x01FF);
            if (hDesk != IntPtr.Zero)
            {
                SetThreadDesktop(hDesk);
            }
        }
        catch { }

        uint lastPid = 0;
        bool lastDota = false;
        while (true)
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(fg, out uint pid);
                    if (pid > 0)
                    {
                        if (pid != lastPid)
                        {
                            lastPid = pid;
                            using var p = Process.GetProcessById((int)pid);
                            lastDota = p.ProcessName.Contains("dota2", StringComparison.OrdinalIgnoreCase);
                        }
                        _isDotaFocused = lastDota;
                    }
                    else
                    {
                        _isDotaFocused = false;
                    }
                }
                else
                {
                    _isDotaFocused = false;
                }
            }
            catch
            {
                lastPid = 0;
                _isDotaFocused = false;
            }
            Thread.Sleep(150);
        }
    }

    public static bool IsDotaFocused()
    {
        StartFocusWatcher();
        return _isDotaFocused;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterfaceDelegate(IntPtr thisPtr, ref Guid riid, out IntPtr ppv);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetMasterVolumeDelegate(IntPtr thisPtr, out float level);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetMasterVolumeDelegate(IntPtr thisPtr, float level, ref Guid eventContext);

    private static Guid IID_ISimpleAudioVolume = new("87CE5498-68D6-44E5-9215-6DA47EF883D8");

    private static readonly Dictionary<uint, string> _pidCache = new();
    public static string GetProcessName(uint pid)
    {
        if (pid == 0) return "";
        lock (_pidCache)
        {
            if (_pidCache.TryGetValue(pid, out string? name)) return name;
            name = "";
            try { name = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); } catch { }
            string folder = ImageFolder(pid);
            if (folder != "") name = name + " " + folder;
            _pidCache[pid] = name;
            return name;
        }
    }

    private static string ImageFolder(uint pid)
    {
        IntPtr h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return "";
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (!QueryFullProcessImageName(h, 0, sb, ref size)) return "";
            string dir = Path.GetDirectoryName(sb.ToString()) ?? "";
            return Path.GetFileName(dir).ToLowerInvariant();
        }
        catch
        {
            return "";
        }
        finally
        {
            CloseHandle(h);
        }
    }

    public static string GetSessionFamily(string pName, string displayName, string iconPath)
    {
        string combined = (pName + " " + displayName + " " + iconPath).ToLowerInvariant();
        if (combined.Contains("dotify")) return "dotify";
        if (combined.Contains("cider")) return "cider";
        if (combined.Contains("musicbee")) return "musicbee";
        if (combined.Contains("winamp")) return "winamp";
        if (combined.Contains("qobuz")) return "qobuz";
        if (combined.Contains("amazon music") || combined.Contains("amazonmusic")) return "amazon";
        if (combined.Contains("ytmdesktop") || combined.Contains("youtube music")) return "ytmusic";
        if (combined.Contains("spotify")) return "spotify";
        if (combined.Contains("yandex") || combined.Contains("яндекс") || combined.Contains("Яндекс")) return "yandex";
        if (combined.Contains("aimp")) return "aimp";
        if (combined.Contains("foobar")) return "foobar";
        if (combined.Contains("apple") || combined.Contains("itunes")) return "apple";
        if (combined.Contains("tidal")) return "tidal";
        if (combined.Contains("deezer")) return "deezer";
        if (combined.Contains("vlc")) return "vlc";
        if (combined.Contains("zen")) return "zen";
        if (combined.Contains("chrome")) return "chrome";
        if (combined.Contains("firefox")) return "firefox";
        if (combined.Contains("msedge")) return "msedge";
        if (combined.Contains("opera")) return "opera";
        if (combined.Contains("brave")) return "brave";
        if (combined.Contains("vivaldi")) return "vivaldi";
        return "";
    }

    public static bool IsMusicPlayerFamily(string fam)
    {
        return fam is "dotify" or "spotify" or "yandex" or "aimp" or "foobar" or "apple" or "tidal" or "deezer" or "vlc" or "cider" or "musicbee" or "winamp" or "qobuz" or "amazon" or "ytmusic";
    }

    private static bool TryGetVolume(IntPtr session, out float vol)
    {
        vol = 1.0f;
        var qi = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(session), 0));
        var iidVol = IID_ISimpleAudioVolume;
        if (qi(session, ref iidVol, out IntPtr pVol) != 0 || pVol == IntPtr.Zero) return false;
        try
        {
            var getVol = Marshal.GetDelegateForFunctionPointer<GetMasterVolumeDelegate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pVol), 4 * IntPtr.Size));
            return getVol(pVol, out vol) == 0;
        }
        finally
        {
            Marshal.Release(pVol);
        }
    }

    private static bool TrySetVolume(IntPtr session, float vol)
    {
        var qi = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(session), 0));
        var iidVol = IID_ISimpleAudioVolume;
        if (qi(session, ref iidVol, out IntPtr pVol) != 0 || pVol == IntPtr.Zero) return false;
        try
        {
            var setVol = Marshal.GetDelegateForFunctionPointer<SetMasterVolumeDelegate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pVol), 3 * IntPtr.Size));
            Guid g = Guid.Empty;
            return setVol(pVol, vol, ref g) == 0;
        }
        finally
        {
            Marshal.Release(pVol);
        }
    }

    public static List<SessionEntry> EnumerateAllSessions(bool all = false)
    {
        var list = new List<SessionEntry>();
        try
        {
            RawAudio.ForEachSession((ses, i, pid, displayName, iconPath, state) =>
            {
                string pName = GetProcessName(pid);
                string fam = GetSessionFamily(pName, displayName, iconPath);
                if (!all && string.IsNullOrEmpty(fam)) return;
                TryGetVolume(ses, out float vol);
                list.Add(new SessionEntry
                {
                    Index = i,
                    Pid = pid,
                    ProcessName = pName,
                    DisplayName = displayName,
                    IconPath = iconPath,
                    Family = fam,
                    State = state,
                    Volume = vol
                });
            });
            LastError = "";
        }
        catch (Exception ex)
        {
            LastError = ex.GetType().Name + ": " + ex.Message;
        }
        return list;
    }

    private static volatile List<SessionEntry> _sessionCache = new();
    private static long _sessionCacheAt;
    private static int _sessionBusy;
    private const int SessionCacheMs = 700;

    public static List<SessionEntry> CachedSessions()
    {
        long now = Environment.TickCount64;
        if (Interlocked.Read(ref _sessionCacheAt) == 0)
        {
            var first = EnumerateAllSessions();
            _sessionCache = first;
            Interlocked.Exchange(ref _sessionCacheAt, Math.Max(1, Environment.TickCount64));
            return first;
        }
        if (now - Interlocked.Read(ref _sessionCacheAt) > SessionCacheMs && Interlocked.CompareExchange(ref _sessionBusy, 1, 0) == 0)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var fresh = EnumerateAllSessions();
                    _sessionCache = fresh;
                    Interlocked.Exchange(ref _sessionCacheAt, Math.Max(1, Environment.TickCount64));
                }
                catch { }
                finally { Volatile.Write(ref _sessionBusy, 0); }
            });
        }
        return _sessionCache;
    }

    private static void PatchCachedVolume(string family, float vol)
    {
        foreach (var s in _sessionCache)
        {
            if (s.Family == family) s.Volume = vol;
        }
    }

    public static string ResolveTargetFamily(string preferredFamily)
    {
        var sessions = CachedSessions();
        if (!string.IsNullOrEmpty(preferredFamily))
        {
            foreach (var s in sessions)
            {
                if (s.Family == preferredFamily) return preferredFamily;
            }
            return "";
        }
        foreach (var s in sessions)
        {
            if (s.State == 1 && IsMusicPlayerFamily(s.Family)) return s.Family;
        }
        foreach (var s in sessions)
        {
            if (IsMusicPlayerFamily(s.Family)) return s.Family;
        }
        foreach (var s in sessions)
        {
            if (s.State == 1) return s.Family;
        }
        return sessions.Count > 0 ? sessions[0].Family : "";
    }

    public static int GetFamilyAudioState(string preferredFamily)
    {
        if (string.IsNullOrEmpty(preferredFamily)) return -1;
        var sessions = CachedSessions();
        bool found = false;
        foreach (var s in sessions)
        {
            if (s.Family == preferredFamily)
            {
                found = true;
                if (s.State == 1) return 1;
            }
        }
        return found ? 0 : -1;
    }

    public static float GetAppVolume(string preferredFamily = "")
    {
        string targetFam = ResolveTargetFamily(preferredFamily);
        if (string.IsNullOrEmpty(targetFam)) return -1.0f;

        foreach (var s in CachedSessions())
        {
            if (s.Family == targetFam) return s.Volume;
        }
        return -1.0f;
    }

    public static string DescribeSessions()
    {
        var parts = new List<string>();
        var peaks = RawAudio.SessionPeaks();
        foreach (var s in EnumerateAllSessions(true))
        {
            string state = s.State == 1 ? "active" : s.State == 0 ? "inactive" : "expired";
            string peak = peaks.TryGetValue((uint)s.Pid, out float pv) ? " peak " + pv.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "";
            parts.Add((s.Family == "" ? "?" : s.Family) + " = " + (s.ProcessName == "" ? "system" : s.ProcessName) + " pid " + s.Pid + " " + state + " " + (int)Math.Round(s.Volume * 100) + "%" + peak);
        }
        return parts.Count > 0 ? string.Join("; ", parts) : "none" + (LastError != "" ? " (" + LastError + ")" : "");
    }

    public static float StepAppVolume(float delta, string preferredFamily = "")
    {
        string targetFam = ResolveTargetFamily(preferredFamily);
        if (string.IsNullOrEmpty(targetFam))
        {
            LastTarget = "no audio session for " + (preferredFamily == "" ? "the player" : preferredFamily);
            return -1.0f;
        }

        float curVol = -1.0f;
        foreach (var s in EnumerateAllSessions())
        {
            if (s.Family == targetFam)
            {
                curVol = s.Volume;
                break;
            }
        }
        if (curVol < 0.0f) curVol = 1.0f;
        float newVol = Math.Min(1.0f, Math.Max(0.0f, curVol + delta));
        PatchCachedVolume(targetFam, newVol);

        try
        {
            RawAudio.ForEachSession((ses, i, pid, displayName, iconPath, state) =>
            {
                string pName = GetProcessName(pid);
                if (GetSessionFamily(pName, displayName, iconPath) != targetFam) return;
                LastTarget = targetFam + " = " + pName + " pid " + pid;
                if (!TrySetVolume(ses, newVol)) return;
                lock (_duckLock)
                {
                    if (_isDucked)
                    {
                        string sessionKey = pid + "_" + i;
                        float duckMultiplier = 1.0f - _targetDuckPercent;
                        float restoredEquivalent = duckMultiplier > 0.001f ? (newVol / duckMultiplier) : newVol;
                        _savedSessionVolumes[sessionKey] = Math.Min(1.0f, Math.Max(0.0f, restoredEquivalent));
                    }
                }
            });
        }
        catch (Exception ex)
        {
            LastError = ex.GetType().Name + ": " + ex.Message;
        }
        return newVol;
    }

    private static readonly object _duckLock = new();
    private static volatile bool _isDucked;
    private static volatile bool _duckResetRequested;
    private static long _duckEndTime;
    private static float _targetDuckPercent = 0.5f;
    private static readonly Dictionary<string, float> _savedSessionVolumes = new();
    private static Thread? _workerThread;

    public static void DuckAllAudio(float duckPercent, int holdMs)
    {
        if (duckPercent <= 0.001f) return;
        lock (_duckLock)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long newEnd = now + holdMs;
            if (newEnd > _duckEndTime) _duckEndTime = newEnd;
            _targetDuckPercent = duckPercent;

            if (_isDucked)
            {
                _duckResetRequested = true;
                return;
            }

            _isDucked = true;
            _duckResetRequested = false;

            _workerThread = new Thread(EnvelopeWorker)
            {
                IsBackground = true,
                Name = "AudioDuckEnvelope"
            };
            _workerThread.Start();
        }
    }

    private sealed class DuckTarget
    {
        public IntPtr Vol;
        public string Key = "";
    }

    private static List<DuckTarget> _duckTargets = new();

    private static void AcquireDuckTargets()
    {
        ReleaseDuckTargets();
        var list = new List<DuckTarget>();
        try
        {
            uint currentPid = (uint)Environment.ProcessId;
            RawAudio.ForEachSession((ses, i, pid, displayName, iconPath, state) =>
            {
                if (pid != 0 && pid == currentPid) return;
                var qi = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(ses), 0));
                var iidVol = IID_ISimpleAudioVolume;
                if (qi(ses, ref iidVol, out IntPtr pVol) != 0 || pVol == IntPtr.Zero) return;
                list.Add(new DuckTarget { Vol = pVol, Key = pid + "_" + i });
            });
        }
        catch { }
        _duckTargets = list;
    }

    private static void ReleaseDuckTargets()
    {
        var old = _duckTargets;
        _duckTargets = new List<DuckTarget>();
        foreach (var t in old)
        {
            try { Marshal.Release(t.Vol); } catch { }
        }
    }

    private static void ApplyDuckLevel(float duckFrac)
    {
        try
        {
            foreach (var t in _duckTargets)
            {
                var vtbl = Marshal.ReadIntPtr(t.Vol);
                if (!_savedSessionVolumes.TryGetValue(t.Key, out float origVol))
                {
                    var getVol = Marshal.GetDelegateForFunctionPointer<GetMasterVolumeDelegate>(Marshal.ReadIntPtr(vtbl, 4 * IntPtr.Size));
                    if (getVol(t.Vol, out float cur) != 0) continue;
                    _savedSessionVolumes[t.Key] = cur;
                    origVol = cur;
                }
                var setVol = Marshal.GetDelegateForFunctionPointer<SetMasterVolumeDelegate>(Marshal.ReadIntPtr(vtbl, 3 * IntPtr.Size));
                Guid g = Guid.Empty;
                setVol(t.Vol, Math.Max(0.0f, Math.Min(1.0f, origVol * (1.0f - duckFrac))), ref g);
            }
        }
        catch { }
    }

    private static void EnvelopeWorker()
    {
        try
        {
            AcquireDuckTargets();
            for (int step = 1; step <= 3; step++)
            {
                float t = step / 3.0f;
                float duckFrac = _targetDuckPercent * (t * t);
                ApplyDuckLevel(duckFrac);
                Thread.Sleep(20);
            }
            ApplyDuckLevel(_targetDuckPercent);

            while (true)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now >= _duckEndTime) break;
                if (_duckResetRequested)
                {
                    _duckResetRequested = false;
                    ApplyDuckLevel(_targetDuckPercent);
                }
                Thread.Sleep(30);
            }

            int releaseSteps = 8;
            for (int step = 1; step <= releaseSteps; step++)
            {
                if (_duckResetRequested)
                {
                    _duckResetRequested = false;
                    ApplyDuckLevel(_targetDuckPercent);
                    while (true)
                    {
                        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        if (now >= _duckEndTime) break;
                        Thread.Sleep(30);
                    }
                    step = 0;
                    continue;
                }
                float t = step / (float)releaseSteps;
                float smoothT = t * t * (3.0f - 2.0f * t);
                float duckFrac = _targetDuckPercent * (1.0f - smoothT);
                ApplyDuckLevel(duckFrac);
                Thread.Sleep(44);
            }

            ApplyDuckLevel(0.0f);
        }
        catch { }
        finally
        {
            lock (_duckLock)
            {
                _savedSessionVolumes.Clear();
                _isDucked = false;
                _duckResetRequested = false;
                _workerThread = null;
            }
            ReleaseDuckTargets();
        }
    }

    public static void RestoreAudioDucking()
    {
        lock (_duckLock)
        {
            if (!_isDucked) return;
            ApplyDuckLevel(0.0f);
            _savedSessionVolumes.Clear();
            _isDucked = false;
            _duckResetRequested = false;
            _workerThread = null;
        }
    }
}

public static class Meter
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetPeakDelegate(IntPtr self, out float peak);

    private static readonly object Sync = new();
    private static IntPtr _meter;
    private static GetPeakDelegate? _get;
    private static long _retryAt;

    private static float[] Zero() => new float[] { 0f, 0f, 0f, 0f, 0f };

    public static float[] GetBars()
    {
        lock (Sync)
        {
            try
            {
                if (_meter == IntPtr.Zero)
                {
                    if (Environment.TickCount64 < _retryAt) return Zero();
                    _meter = RawAudio.EndpointMeter();
                    if (_meter == IntPtr.Zero)
                    {
                        _retryAt = Environment.TickCount64 + 2000;
                        return Zero();
                    }
                    _get = Marshal.GetDelegateForFunctionPointer<GetPeakDelegate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(_meter), 3 * IntPtr.Size));
                }
                if (_get == null || _get(_meter, out float peak) != 0)
                {
                    Marshal.Release(_meter);
                    _meter = IntPtr.Zero;
                    return Zero();
                }
                if (peak <= 0.001f) return Zero();
                float p = Math.Min(1.0f, Math.Max(0.0f, peak));
                return new float[]
                {
                    (float)Math.Round(p * 0.7f, 2),
                    (float)Math.Round(p * 0.85f, 2),
                    (float)Math.Round(p, 2),
                    (float)Math.Round(p * 0.85f, 2),
                    (float)Math.Round(p * 0.7f, 2)
                };
            }
            catch
            {
                return Zero();
            }
        }
    }
}
