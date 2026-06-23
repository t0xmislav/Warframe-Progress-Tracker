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
            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            var existingTheme = mergedDicts.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains("DarkTheme") == true ||
                d.Source?.OriginalString.Contains("LightTheme") == true);

            var themeDict = new ResourceDictionary
            {
                Source = new Uri(themeFile, UriKind.Relative)
            };
            
            if (existingTheme is not null)
            {
                int index = mergedDicts.IndexOf(existingTheme);
                mergedDicts[index] = themeDict;
            }
            else
            {
                mergedDicts.Insert(0, themeDict);
            }
        }
    }
}
