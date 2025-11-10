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

            Application.Current.Resources.MergedDictionaries.Clear();
            var resDict = new ResourceDictionary()
            {
                Source = new Uri($"/Resources/Language/Dictionary-{lang}.xaml", UriKind.Relative)
            };
            Application.Current.Resources.MergedDictionaries.Add(resDict);

            LanguageChanged?.Invoke();
        }
    }
}
