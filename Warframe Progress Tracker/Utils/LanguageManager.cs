using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Warframe_Progress_Tracker.Utils
{
    public class LanguageManager
    {
        public static event Action? LanguageChanged;
        public static void SetLanguage(string lang)
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(lang);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(lang);

            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            var existing = mergedDicts.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains("Dictionary-") == true);

            var newDict = new ResourceDictionary
            {
                Source = new Uri($"/Resources/Language/Dictionary-{lang}.xaml", UriKind.Relative)
            };

            if (existing is not null)
                mergedDicts[mergedDicts.IndexOf(existing)] = newDict;
            else
                mergedDicts.Insert(0, newDict);

            LanguageChanged?.Invoke();
        }
    }
}
