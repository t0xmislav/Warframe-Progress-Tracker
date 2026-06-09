using Microsoft.Playwright;
using PdfSharp.Fonts;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var pepper = Environment.GetEnvironmentVariable("WPT_PEPPER");
            if (string.IsNullOrEmpty(pepper))
            {
                var bytes = RandomNumberGenerator.GetBytes(32);
                var demoPepper = Convert.ToBase64String(bytes);
                Environment.SetEnvironmentVariable("WPT_PEPPER", demoPepper, EnvironmentVariableTarget.User);

                Debug.WriteLine("[Startup] WPT_PEPPER not found - creating new pepper.");
                LoggerService.Log("Security", "WPT_PEPPER not set; creating new pepper.");

            }
            else
            {
                Debug.WriteLine("[Startup] WPT_PEPPER found in environment variables.");
            }

            AesKeyManager.Initialize();

            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            if (!IniFileService.Exists)
            {
                IniFileService.Write("Account", "Language", "en");
                IniFileService.Write("Account", "Theme", "Light");
                IniFileService.Write("Account", "SpeedLimit", "0");
            }
            string lang = IniFileService.Read("Account", "Language", "en");
            string theme = IniFileService.Read("Account", "Theme", "Light");
            LanguageManager.SetLanguage(lang);
            ThemeManager.ApplyTheme(theme);
            DbService.InitializeDatabase();
            //Default admin account for testing
            AuthService.Register("admin", "admin", true);

            var pluginsFolder = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            Console.WriteLine($"Before loading plugin");
            PluginLoader.LoadPlugins(pluginsFolder);
        }
    }

}
