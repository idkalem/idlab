using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using IDLab.Desktop;

namespace IDLab.Web
{
    /// <summary>
    /// Browser host: the page calls <see cref="Frame"/> from requestAnimationFrame. The first frames build the lab one
    /// step at a time behind the splash screen, then every call runs one frame of the app.
    /// </summary>
    public static partial class WebMain
    {
        private static AppHost _app;
        private static IEnumerator<(float progress, string what)> _loading;
        private static bool _ready, _failed;

        public static void Main(string[] args)
        {
            try
            {
                var tr = new CultureInfo("tr-TR");
                CultureInfo.DefaultThreadCurrentCulture = tr;
                CultureInfo.CurrentCulture = tr;
            }
            catch (CultureNotFoundException)
            {
            }

            Platform.ToggleBrowserFullscreen = ToggleFullscreen;
            Platform.BrowserIsFullscreen = IsFullscreen;
            Platform.DownloadText = DownloadText;
            Platform.LoadSettingsText = LoadSettings;
            Platform.PollPointer = PollPointer;
            Platform.SaveSettingsText = SaveSettings;

            _app = new AppHost(Program.Options.Parse(args));
            _app.OpenWindow();
            _loading = _app.Load().GetEnumerator();
        }

        [JSExport]
        public static void Frame()
        {
            if (_failed) return;
            try
            {
                if (!_ready)
                {
                    if (_loading.MoveNext())
                    {
                        _app.Splash(_loading.Current.progress, _loading.Current.what);
                    }
                    else
                    {
                        _ready = true;
                        Ready();
                    }

                    return;
                }

                _app.Frame();
            }
            catch (Exception ex)
            {
                _failed = true;
                ShowError(ex.ToString());
            }
        }

        [JSImport("toggleFullscreen", "main.js")]
        private static partial void ToggleFullscreen();

        [JSImport("isFullscreen", "main.js")]
        private static partial bool IsFullscreen();

        [JSImport("downloadText", "main.js")]
        private static partial void DownloadText(string name, string text);

        [JSImport("loadSettings", "main.js")]
        private static partial string LoadSettings();

        [JSImport("saveSettings", "main.js")]
        private static partial void SaveSettings(string text);

        [JSImport("pollPointer", "main.js")]
        [return: JSMarshalAs<JSType.Array<JSType.Number>>]
        private static partial double[] PollPointer();

        [JSImport("ready", "main.js")]
        private static partial void Ready();

        [JSImport("showError", "main.js")]
        private static partial void ShowError(string text);
    }
}
