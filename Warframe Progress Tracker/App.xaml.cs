using Microsoft.Playwright;
using System.Configuration;
using System.Data;
using System.Windows;
using Warframe_Progress_Tracker.Utils;

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

            if (!IniFileService.Exists)
            {
                IniFileService.Write("Account", "Language", "en");
                IniFileService.Write("Account", "Theme", "Light");
            }
            string lang = IniFileService.Read("Account", "Language", "en");
            string theme = IniFileService.Read("Account", "Theme", "Light");
            LanguageManager.SetLanguage(lang);
            ThemeManager.ApplyTheme(theme);
        }
    }

}
