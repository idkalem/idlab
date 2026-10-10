using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// Keeps the mouse pointer visible. On touch smart boards Windows often starts with the pointer hidden (the
    /// cursor display count begins at −1 when no mouse was present at boot) or suppresses it after touch input, so a
    /// mouse plugged in later shows nothing. We force the system cursor on, and if Windows still does not draw it while
    /// a real mouse is moving, the app draws its own arrow.
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

        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursorW(IntPtr instance, IntPtr name);

        [DllImport("user32.dll")]
        private static extern IntPtr SetCursor(IntPtr cursor);

        private static readonly IntPtr IdcArrow = new IntPtr(32512);
        private static bool _software;
        private static readonly bool Forced = Environment.GetEnvironmentVariable("IDLAB_SOFTCURSOR") == "1"; // testing / always draw
        private static float _checkTimer;

        /// <summary>Call after the window is created and after switching full screen.</summary>
        public static void Ensure()
        {
            EnableCursor();
            SetMouseCursor(MouseCursor.Arrow);
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                // ShowCursor only changes a counter; the pointer is drawn while it is ≥ 0.
                for (int i = 0; i < 16 && ShowCursor(true) < 0; i++)
                {
                }

                SetCursor(LoadCursorW(IntPtr.Zero, IdcArrow));
            }
            catch (Exception)
            {
                // Not fatal: the software pointer below still works.
            }
        }

        /// <summary>Once per frame: decides whether the app must draw the pointer itself.</summary>
        public static void Update(float dt)
        {
            if (Forced) _software = true;
            if (!OperatingSystem.IsWindows() || Forced) return;
            _checkTimer -= dt;
            if (_checkTimer > 0) return;
            _checkTimer = 0.25f;
            bool mouseActive = TouchInput.SecondsSinceMouseMove < 4f;
            if (!mouseActive)
            {
                _software = false;
                return;
            }

            try
            {
                var info = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
                if (!GetCursorInfo(ref info)) return;
                if ((info.Flags & CursorShowing) == 0)
                {
                    Ensure();
                    info.Size = Marshal.SizeOf<CursorInfo>();
                    GetCursorInfo(ref info);
                }

                _software = (info.Flags & CursorShowing) == 0;
            }
            catch (Exception)
            {
                _software = false;
            }
        }

        /// <summary>Draws the fallback arrow on top of everything (inside BeginDrawing).</summary>
        public static void Draw()
        {
            if (!_software || !IsCursorOnScreen()) return;
            var p = GetMousePosition();
            float k = Math.Clamp(GetScreenHeight() / 1080f, 1f, 2.5f);
            Vector2 P(float x, float y) => new Vector2(p.X + x * k, p.Y + y * k);
            var outline = new[] { P(0, 0), P(0, 24), P(6, 18.5f), P(10, 27.5f), P(14, 25.5f), P(10, 17), P(17.5f, 17) };
            // Black border, then white body (two triangle fans, counter-clockwise for raylib).
            DrawFan(outline, Color.Black, 1.6f * k, p);
            DrawFan(outline, Color.White, 0, p);
        }

        private static void DrawFan(Vector2[] pts, Color color, float grow, Vector2 origin)
        {
            var c = new Vector2(origin.X + 5, origin.Y + 14);
            var q = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var d = pts[i] - c;
                q[i] = pts[i] + (d.LengthSquared() > 0 ? Vector2.Normalize(d) * grow : Vector2.Zero);
            }

            for (int i = 0; i < q.Length; i++)
            {
                var a = q[i];
                var b = q[(i + 1) % q.Length];
                DrawTriangle(c, b, a, color);
                DrawTriangle(c, a, b, color);
            }
        }
    }
}
