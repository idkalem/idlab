using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IDLab.Desktop
{
    /// <summary>
    /// Catches taps that the frame-based input polling loses. On a touch screen Windows promotes a tap to a mouse
    /// button-down immediately followed by a button-up; both arrive in the same message pump, so raylib (which only
    /// compares the button state between frames) sees nothing and the user has to tap again. A thread-local
    /// WH_MOUSE hook sees every button message as it is pumped; <see cref="Ui.BeginFrame"/> turns a lost tap into a
    /// normal press on one frame and release on the next.
    /// </summary>
    internal static class TouchInput
    {
        private const int WhMouse = 7, HcAction = 0, WmLButtonDown = 0x0201, WmLButtonUp = 0x0202;

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookExW(int idHook, HookProc proc, IntPtr hMod, uint threadId);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private static HookProc _proc; // kept alive: the native side holds only a function pointer
        private static IntPtr _hook;
        private static readonly List<bool> Events = new List<bool>(); // true = down, false = up (since last frame)

        public static bool Installed => _hook != IntPtr.Zero;

        public static void Install()
        {
            if (!OperatingSystem.IsWindows() || _hook != IntPtr.Zero) return;
            try
            {
                _proc = Hook;
                _hook = SetWindowsHookExW(WhMouse, _proc, IntPtr.Zero, GetCurrentThreadId());
            }
            catch (Exception)
            {
                _hook = IntPtr.Zero; // input simply falls back to polling
            }
        }

        public static void Uninstall()
        {
            if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        private static IntPtr Hook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code == HcAction)
            {
                int msg = wParam.ToInt32();
                if (msg == WmLButtonDown) Events.Add(true);
                else if (msg == WmLButtonUp) Events.Add(false);
            }

            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        /// <summary>True when a complete down→up happened since the previous frame (a tap the polling missed).</summary>
        public static bool TakeTap()
        {
            bool tap = false;
            for (int i = 0; i + 1 < Events.Count; i++)
            {
                if (Events[i] && !Events[i + 1]) tap = true;
            }

            Events.Clear();
            return tap;
        }
    }
}
