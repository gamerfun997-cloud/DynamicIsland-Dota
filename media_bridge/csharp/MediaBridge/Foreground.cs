using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace MediaBridge;

internal static class Foreground
{
    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private static readonly string[] KnownBrowsers = { "chrome", "msedge", "firefox", "opera", "brave", "yandex", "vivaldi", "browser", "arc", "zen", "librewolf", "waterfox", "thorium" };

    private static string ClassOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string TitleOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string ProcessOf(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        try { return Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); } catch { return ""; }
    }

    private static List<IntPtr> TopWindows()
    {
        var list = new List<IntPtr>();
        EnumWindows((h, _) =>
        {
            if (IsWindowVisible(h) && GetWindow(h, 4) == IntPtr.Zero) list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static void Bring(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        IntPtr fg = GetForegroundWindow();
        uint fgThread = GetWindowThreadProcessId(fg, out _);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        try
        {
            if (IsIconic(hwnd)) ShowWindow(hwnd, 9);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(me, fgThread, false);
        }
        if (GetForegroundWindow() != hwnd)
        {
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            SetForegroundWindow(hwnd);
            keybd_event(0x12, 0, 2, UIntPtr.Zero);
        }
    }

    public static void BringFolder(string dir, HashSet<IntPtr> before)
    {
        string name = Path.GetFileName(dir.TrimEnd('\\', '/'));
        _ = Task.Run(async () =>
        {
            IntPtr fallback = IntPtr.Zero;
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(100);
                foreach (var h in TopWindows())
                {
                    if (ClassOf(h) != "CabinetWClass") continue;
                    if (!before.Contains(h))
                    {
                        Bring(h);
                        return;
                    }
                    if (fallback == IntPtr.Zero && string.Equals(TitleOf(h), name, StringComparison.OrdinalIgnoreCase)) fallback = h;
                }
                if (i >= 8 && fallback != IntPtr.Zero)
                {
                    Bring(fallback);
                    return;
                }
            }
        });
    }

    public static HashSet<IntPtr> Snapshot()
    {
        return new HashSet<IntPtr>(TopWindows());
    }

    private static string DefaultBrowser()
    {
        try
        {
            using var choice = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
            string? progId = choice?.GetValue("ProgId") as string;
            if (string.IsNullOrEmpty(progId)) return "";
            using var cmd = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command");
            string? line = cmd?.GetValue(null) as string;
            if (string.IsNullOrEmpty(line)) return "";
            string exe = line.StartsWith("\"") ? line.Substring(1, Math.Max(0, line.IndexOf('"', 1) - 1)) : line.Split(' ')[0];
            return Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
        }
        catch
        {
            return "";
        }
    }

    public static void BringBrowser()
    {
        _ = Task.Run(async () =>
        {
            string browser = DefaultBrowser();
            for (int i = 0; i < 25; i++)
            {
                await Task.Delay(i == 0 ? 350 : 120);
                foreach (var h in TopWindows())
                {
                    if (TitleOf(h).Length == 0) continue;
                    string proc = ProcessOf(h);
                    if (proc.Length == 0) continue;
                    bool match = browser.Length > 0 ? proc == browser : Array.IndexOf(KnownBrowsers, proc) >= 0;
                    if (!match) continue;
                    Bring(h);
                    return;
                }
            }
        });
    }
}
