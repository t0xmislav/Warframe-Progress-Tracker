using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Warframe_Progress_Tracker.Utils
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string theme)
        {
            string themeFile = theme.ToLower() == "dark"
                ? "/Resources/Theme/DarkTheme.xaml"
                : "/Resources/Theme/LightTheme.xaml";

            var themeDict = new ResourceDictionary
            {
                Source = new Uri(themeFile, UriKind.Relative)
            };

            var existingTheme = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source is not null && d.Source.OriginalString.Contains("Theme"));
            if (existingTheme is not null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existingTheme);
            }
            Application.Current.Resources.MergedDictionaries.Add(themeDict);
        }
    }
}
