using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MediaBridge;

internal static class KeyPress
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static readonly HashSet<int> Extended = new() { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E, 0x5D };

    public static bool Allowed(int vk)
    {
        return vk == 0x13 || (vk >= 0x21 && vk <= 0x2E) || (vk >= 0x30 && vk <= 0x39) || (vk >= 0x41 && vk <= 0x5A)
            || vk == 0x5D || (vk >= 0x60 && vk <= 0x69) || (vk >= 0x70 && vk <= 0x87) || vk == 0x91 || vk == 0xC0;
    }

    private static IntPtr DotaWindow()
    {
        foreach (var p in Process.GetProcessesByName("dota2"))
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle;
            }
            catch { }
        }
        return IntPtr.Zero;
    }

    public static string Tap(int vk, bool global)
    {
        uint scan = MapVirtualKey((uint)vk, 0);
        bool ext = Extended.Contains(vk);
        IntPtr hwnd = global ? IntPtr.Zero : DotaWindow();
        if (hwnd != IntPtr.Zero)
        {
            long baseParam = 1 | (scan << 16) | (ext ? 1L << 24 : 0);
            PostMessage(hwnd, 0x0100, (IntPtr)vk, (IntPtr)baseParam);
            Thread.Sleep(30);
            PostMessage(hwnd, 0x0101, (IntPtr)vk, (IntPtr)(baseParam | (1L << 30) | (1L << 31)));
            return "dota";
        }
        uint flags = ext ? 1u : 0u;
        keybd_event((byte)vk, (byte)scan, flags, UIntPtr.Zero);
        Thread.Sleep(30);
        keybd_event((byte)vk, (byte)scan, flags | 2u, UIntPtr.Zero);
        return "global";
    }
}
