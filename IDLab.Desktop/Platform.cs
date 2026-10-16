using System;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// What differs between the Windows app and the web edition (WebAssembly in a browser). The web host fills in the
    /// browser hooks at start-up; on the desktop they stay null and the native behaviour is used.
    /// </summary>
    internal static class Platform
    {
        public static bool IsWeb => OperatingSystem.IsBrowser();

#pragma warning disable CS0649 // assigned by the web host only
        /// <summary>Browser: toggles full screen for the page.</summary>
        public static Action ToggleBrowserFullscreen;

        /// <summary>Browser: whether the page is full screen.</summary>
        public static Func<bool> BrowserIsFullscreen;

        /// <summary>Browser: offers a text file for download (file name, contents).</summary>
        public static Action<string, string> DownloadText;

        /// <summary>
        /// Browser: pointer (mouse, finger or pen) since the previous frame as [x, y, down, presses, releases, pressX, pressY]
        /// in canvas pixels. A quick tap's press and release often both fall between two frames.
        /// </summary>
        public static Func<double[]> PollPointer;

        /// <summary>Browser: settings text kept in localStorage.</summary>
        public static Func<string> LoadSettingsText;
        public static Action<string> SaveSettingsText;
#pragma warning restore CS0649

        public static bool IsFullscreen =>
            IsWeb ? BrowserIsFullscreen?.Invoke() ?? false : IsWindowState(ConfigFlags.BorderlessWindowMode);

        public static void ToggleFullscreen()
        {
            if (IsWeb) ToggleBrowserFullscreen?.Invoke();
            else
            {
                ToggleBorderlessWindowed();
                PointerCursor.Ensure();
            }
        }

        /// <summary>Desktop shaders are GLSL 3.30; WebGL 2 wants GLSL ES 3.00 with a default precision.</summary>
        public static string Shader(string source) =>
            source == null || !IsWeb ? source : source.Replace("#version 330", "#version 300 es\nprecision highp float;");
    }
}
