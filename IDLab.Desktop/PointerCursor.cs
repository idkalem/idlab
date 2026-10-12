using System;
using System.Runtime.InteropServices;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// Keeps the Windows mouse pointer visible. On touch smart boards Windows may start with the pointer hidden (the
    /// cursor display count begins at −1 when no mouse was present at boot), so a mouse plugged in later shows
    /// nothing. We switch the system pointer back on; the app never draws a pointer of its own.
    /// </summary>
    internal static class PointerCursor
    {
        private const int CursorShowing = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct CursorInfo
        {
            public int Size, Flags;
            public IntPtr Handle;
            public int X, Y;
        }

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool show);

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(ref CursorInfo info);

        private static float _checkTimer;

        /// <summary>Call after the window is created and after switching full screen.</summary>
        public static void Ensure()
        {
            EnableCursor();
            SetMouseCursor(MouseCursor.Arrow);
            ShowSystemPointer();
        }

        /// <summary>Once per frame: if a real mouse is moving but Windows hides the pointer, switch it back on.</summary>
        public static void Update(float dt)
        {
            if (!OperatingSystem.IsWindows()) return;
            _checkTimer -= dt;
            if (_checkTimer > 0) return;
            _checkTimer = 0.5f;
            if (TouchInput.SecondsSinceMouseMove > 2f) return;
            try
            {
                var info = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
                if (GetCursorInfo(ref info) && (info.Flags & CursorShowing) == 0) ShowSystemPointer();
            }
            catch (Exception)
            {
                // Not fatal: the pointer simply stays as Windows decided.
            }
        }

        private static void ShowSystemPointer()
        {
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                // ShowCursor only changes a counter; the pointer is drawn while it is ≥ 0.
                for (int i = 0; i < 16 && ShowCursor(true) < 0; i++)
                {
                }
            }
            catch (Exception)
            {
                // Not fatal.
            }
        }
    }
}
