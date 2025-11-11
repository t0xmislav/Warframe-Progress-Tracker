using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Utils
{
    public static class IniFileService
    {
        private static readonly string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string section, string key, string value, string filePath);
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder retVal, int size, string filePath);

        public static void Write(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, iniPath);
        }

        public static string Read(string section, string key, string defaultValue = "") 
        {
            var temp = new StringBuilder(255);
            GetPrivateProfileString(section, key, defaultValue, temp, 255, iniPath);
            return temp.ToString();
        }

        public static bool Exists => File.Exists(iniPath);

    }
}
