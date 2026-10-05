using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MediaBridge;

internal static class RawAudio
{
    private delegate int QueryInterfaceFn(IntPtr self, ref Guid iid, out IntPtr ppv);
    private delegate int GetDefaultEndpointFn(IntPtr self, int flow, int role, out IntPtr dev);
    private delegate int ActivateFn(IntPtr self, ref Guid iid, int ctx, IntPtr p, out IntPtr ppv);
    private delegate int GetPtrFn(IntPtr self, out IntPtr value);
    private delegate int GetIntFn(IntPtr self, out int value);
    private delegate int GetSessionFn(IntPtr self, int index, out IntPtr session);
    private delegate int GetPidFn(IntPtr self, out uint pid);
    private delegate int GetPeakFn(IntPtr self, out float peak);

    private static Guid ClsidEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static Guid IidEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static Guid IidManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private static Guid IidControl = new("F4B1A599-7266-4319-A8CA-E70ACB11E8CD");
    private static Guid IidControl2 = new("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D");
    private static Guid IidMeter = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint coInit);

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint ctx, ref Guid iid, out IntPtr ppv);

    private static T Fn<T>(IntPtr obj, int slot) where T : Delegate
    {
        return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), slot * IntPtr.Size));
    }

    private static string TakeString(IntPtr p)
    {
        if (p == IntPtr.Zero) return "";
        string s = Marshal.PtrToStringUni(p) ?? "";
        Marshal.FreeCoTaskMem(p);
        return s;
    }

    private static string ProcessName(uint pid)
    {
        return AppAudioControl.GetProcessName(pid);
    }

    public static IntPtr EndpointMeter()
    {
        CoInitializeEx(IntPtr.Zero, 0);
        IntPtr en = IntPtr.Zero, dev = IntPtr.Zero;
        try
        {
            if (CoCreateInstance(ref ClsidEnumerator, IntPtr.Zero, 1, ref IidEnumerator, out en) != 0 || en == IntPtr.Zero) return IntPtr.Zero;
            if (Fn<GetDefaultEndpointFn>(en, 4)(en, 0, 1, out dev) != 0 || dev == IntPtr.Zero) return IntPtr.Zero;
            if (Fn<ActivateFn>(dev, 3)(dev, ref IidMeter, 1, IntPtr.Zero, out IntPtr meter) != 0) return IntPtr.Zero;
            return meter;
        }
        finally
        {
            if (dev != IntPtr.Zero) Marshal.Release(dev);
            if (en != IntPtr.Zero) Marshal.Release(en);
        }
    }

    public delegate void SessionVisitor(IntPtr session, int index, uint pid, string display, string icon, int state);

    public static void ForEachSession(SessionVisitor visit)
    {
        CoInitializeEx(IntPtr.Zero, 0);
        IntPtr en = IntPtr.Zero, dev = IntPtr.Zero, mgr = IntPtr.Zero, senum = IntPtr.Zero;
        try
        {
            if (CoCreateInstance(ref ClsidEnumerator, IntPtr.Zero, 1, ref IidEnumerator, out en) != 0 || en == IntPtr.Zero) return;
            if (Fn<GetDefaultEndpointFn>(en, 4)(en, 0, 1, out dev) != 0 || dev == IntPtr.Zero) return;
            if (Fn<ActivateFn>(dev, 3)(dev, ref IidManager2, 1, IntPtr.Zero, out mgr) != 0 || mgr == IntPtr.Zero) return;
            if (Fn<GetPtrFn>(mgr, 5)(mgr, out senum) != 0 || senum == IntPtr.Zero) return;
            Fn<GetIntFn>(senum, 3)(senum, out int count);
            for (int i = 0; i < count; i++)
            {
                if (Fn<GetSessionFn>(senum, 4)(senum, i, out IntPtr ses) != 0 || ses == IntPtr.Zero) continue;
                try
                {
                    var qi = Fn<QueryInterfaceFn>(ses, 0);
                    uint pid = 0;
                    if (qi(ses, ref IidControl2, out IntPtr c2) == 0 && c2 != IntPtr.Zero)
                    {
                        Fn<GetPidFn>(c2, 14)(c2, out pid);
                        Marshal.Release(c2);
                    }
                    string display = "", icon = "";
                    int state = 0;
                    if (qi(ses, ref IidControl, out IntPtr c1) == 0 && c1 != IntPtr.Zero)
                    {
                        Fn<GetIntFn>(c1, 3)(c1, out state);
                        if (Fn<GetPtrFn>(c1, 4)(c1, out IntPtr dn) == 0) display = TakeString(dn);
                        if (Fn<GetPtrFn>(c1, 6)(c1, out IntPtr ic) == 0) icon = TakeString(ic);
                        Marshal.Release(c1);
                    }
                    visit(ses, i, pid, display, icon, state);
                }
                finally
                {
                    Marshal.Release(ses);
                }
            }
        }
        finally
        {
            if (senum != IntPtr.Zero) Marshal.Release(senum);
            if (mgr != IntPtr.Zero) Marshal.Release(mgr);
            if (dev != IntPtr.Zero) Marshal.Release(dev);
            if (en != IntPtr.Zero) Marshal.Release(en);
        }
    }

    public static Dictionary<uint, float> SessionPeaks()
    {
        var res = new Dictionary<uint, float>();
        ForEachSession((ses, index, pid, display, icon, state) =>
        {
            var qi = Fn<QueryInterfaceFn>(ses, 0);
            if (qi(ses, ref IidMeter, out IntPtr meter) != 0 || meter == IntPtr.Zero) return;
            try
            {
                var get = Fn<GetPeakFn>(meter, 3);
                if (get(meter, out float v) == 0)
                {
                    res.TryGetValue(pid, out float cur);
                    if (v > cur) res[pid] = v;
                    else if (!res.ContainsKey(pid)) res[pid] = v;
                }
            }
            catch { }
            finally { Marshal.Release(meter); }
        });
        return res;
    }

    public static List<IntPtr> FamilyMeters(string family)
    {
        var list = new List<IntPtr>();
        if (string.IsNullOrEmpty(family)) return list;
        CoInitializeEx(IntPtr.Zero, 0);
        IntPtr en = IntPtr.Zero, dev = IntPtr.Zero, mgr = IntPtr.Zero, senum = IntPtr.Zero;
        try
        {
            if (CoCreateInstance(ref ClsidEnumerator, IntPtr.Zero, 1, ref IidEnumerator, out en) != 0 || en == IntPtr.Zero) return list;
            if (Fn<GetDefaultEndpointFn>(en, 4)(en, 0, 1, out dev) != 0 || dev == IntPtr.Zero) return list;
            if (Fn<ActivateFn>(dev, 3)(dev, ref IidManager2, 1, IntPtr.Zero, out mgr) != 0 || mgr == IntPtr.Zero) return list;
            if (Fn<GetPtrFn>(mgr, 5)(mgr, out senum) != 0 || senum == IntPtr.Zero) return list;
            Fn<GetIntFn>(senum, 3)(senum, out int count);
            for (int i = 0; i < count; i++)
            {
                if (Fn<GetSessionFn>(senum, 4)(senum, i, out IntPtr ses) != 0 || ses == IntPtr.Zero) continue;
                var qi = Fn<QueryInterfaceFn>(ses, 0);
                uint pid = 0;
                if (qi(ses, ref IidControl2, out IntPtr c2) == 0 && c2 != IntPtr.Zero)
                {
                    Fn<GetPidFn>(c2, 14)(c2, out pid);
                    Marshal.Release(c2);
                }
                string display = "", icon = "";
                if (qi(ses, ref IidControl, out IntPtr c1) == 0 && c1 != IntPtr.Zero)
                {
                    if (Fn<GetPtrFn>(c1, 4)(c1, out IntPtr dn) == 0) display = TakeString(dn);
                    if (Fn<GetPtrFn>(c1, 6)(c1, out IntPtr ic) == 0) icon = TakeString(ic);
                    Marshal.Release(c1);
                }
                string fam = AppAudioControl.GetSessionFamily(ProcessName(pid), display, icon);
                if (fam == family && qi(ses, ref IidMeter, out IntPtr meter) == 0 && meter != IntPtr.Zero)
                {
                    list.Add(meter);
                }
                Marshal.Release(ses);
            }
        }
        catch { }
        finally
        {
            if (senum != IntPtr.Zero) Marshal.Release(senum);
            if (mgr != IntPtr.Zero) Marshal.Release(mgr);
            if (dev != IntPtr.Zero) Marshal.Release(dev);
            if (en != IntPtr.Zero) Marshal.Release(en);
        }
        return list;
    }
}

public static class LevelMeter
{
    private delegate int GetPeakDelegate(IntPtr self, out float peak);

    private static readonly object Sync = new();
    private static readonly float[] History = new float[64];
    private static readonly int[] Lags = { 7, 3, 0, 4, 9 };
    private static Thread? _thread;
    private static long _lastRequest;
    private static int _head;
    private static float _fast;
    private static float _slow;

    public static string Source { get; private set; } = "";
    public static int Sessions { get; private set; }
    public static float Peak { get; private set; }

    public static double[] Snapshot()
    {
        Interlocked.Exchange(ref _lastRequest, Environment.TickCount64);
        lock (Sync)
        {
            if (_thread == null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "LevelMeter" };
                _thread.SetApartmentState(ApartmentState.MTA);
                _thread.Start();
            }
            var res = new double[Lags.Length];
            for (int i = 0; i < Lags.Length; i++)
            {
                float v = History[(_head - Lags[i] + History.Length) % History.Length];
                res[i] = Math.Round(Math.Clamp(v, 0f, 1f), 3);
            }
            return res;
        }
    }

    private static void ReleaseAll(List<(IntPtr ptr, GetPeakDelegate get)> meters)
    {
        foreach (var m in meters) Marshal.Release(m.ptr);
        meters.Clear();
    }

    private static void Run()
    {
        var meters = new List<(IntPtr ptr, GetPeakDelegate get)>();
        long acquiredAt = 0;
        string family = "";
        IntPtr endpoint = IntPtr.Zero;
        GetPeakDelegate? endpointGet = null;
        int stuck = 0;
        while (true)
        {
            long now = Environment.TickCount64;
            if (now - Interlocked.Read(ref _lastRequest) > 3000)
            {
                ReleaseAll(meters);
                if (endpoint != IntPtr.Zero) Marshal.Release(endpoint);
                lock (Sync)
                {
                    _thread = null;
                    Array.Clear(History);
                }
                return;
            }
            string fam = MediaSessionService.CurrentFamily;
            if (fam != family || now - acquiredAt > (meters.Count == 0 ? 1500 : 6000))
            {
                ReleaseAll(meters);
                if (endpoint != IntPtr.Zero)
                {
                    Marshal.Release(endpoint);
                    endpoint = IntPtr.Zero;
                }
                foreach (var p in RawAudio.FamilyMeters(fam))
                {
                    var get = Marshal.GetDelegateForFunctionPointer<GetPeakDelegate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(p), 3 * IntPtr.Size));
                    meters.Add((p, get));
                }
                family = fam;
                acquiredAt = now;
                Source = fam;
                Sessions = meters.Count;
            }
            float peak = 0f;
            foreach (var m in meters)
            {
                try
                {
                    if (m.get(m.ptr, out float v) == 0 && v > peak) peak = v;
                }
                catch { }
            }
            if (peak >= 0.98f) stuck = Math.Min(stuck + 1, 90); else stuck = Math.Max(0, stuck - 3);
            if (stuck > 20)
            {
                if (stuck == 21)
                {
                    _fast = 0f;
                    _slow = 0f;
                }
                if (endpoint == IntPtr.Zero)
                {
                    endpoint = RawAudio.EndpointMeter();
                    if (endpoint != IntPtr.Zero)
                        endpointGet = Marshal.GetDelegateForFunctionPointer<GetPeakDelegate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(endpoint), 3 * IntPtr.Size));
                }
                if (endpoint != IntPtr.Zero && endpointGet != null)
                {
                    try
                    {
                        if (endpointGet(endpoint, out float ep) == 0) peak = ep;
                    }
                    catch { }
                }
                Source = fam + "+device";
            }
            else
            {
                Source = fam;
            }
            Peak = peak;
            if (_slow < 0.004f && peak > 0.004f)
            {
                _slow = peak;
                _fast = peak;
            }
            _fast += (peak - _fast) * (peak > _fast ? 0.6f : 0.18f);
            _slow += (peak - _slow) * (peak > _slow ? 0.07f : 0.012f);
            float level = _slow < 0.004f ? 0f : Math.Clamp(0.42f + (_fast / _slow - 1f) * (stuck > 20 ? 2.4f : 5.5f), 0f, 1f);
            lock (Sync)
            {
                _head = (_head + 1) % History.Length;
                History[_head] = level;
            }
            Thread.Sleep(16);
        }
    }
}
