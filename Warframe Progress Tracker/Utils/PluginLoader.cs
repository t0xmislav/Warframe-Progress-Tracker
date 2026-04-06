using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.Utils
{
    public static class PluginLoader
    {
        public delegate bool GrantsMasteryDelegate(string uniqueName, string category, int masteryReq, bool excludeFromCodex);

        public delegate int GetMasteryPointsDelegate(string category, string uniqueName);

        public static GrantsMasteryDelegate? GrantsMastery;

        public static GetMasteryPointsDelegate? GetMasteryPoints;
        public static void LoadPlugins(string pluginsFolder)
        {
            if (!Directory.Exists(pluginsFolder)) return;
            Debug.WriteLine($"Loading plugins");
            foreach (var dll in Directory.GetFiles(pluginsFolder, "*.dll"))
            {
                Debug.WriteLine(dll);
                try
                {
                    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dll));
                    var itemFilterType = assembly.GetExportedTypes().FirstOrDefault(t => t.FullName == "Warframe.Tracker.Filters.ItemFilter");

                    if (itemFilterType != null)
                    {
                        var grantsMasteryMethod = itemFilterType.GetMethod("GrantsMastery", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if(grantsMasteryMethod != null)
                        {
                            var del = (GrantsMasteryDelegate?)grantsMasteryMethod.CreateDelegate(typeof(GrantsMasteryDelegate));
                            if (del != null)
                            {
                                GrantsMastery = del;
                            }
                        }
                    }
                    var masteryAssignerType = assembly.GetExportedTypes().FirstOrDefault(t => t.FullName == "Warframe.Tracker.Filters.MasteryAssigner");

                    if (masteryAssignerType != null)
                    {
                        var getMpMethod = masteryAssignerType.GetMethod("GetMasteryPoints", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (getMpMethod != null)
                        {
                            var del = (GetMasteryPointsDelegate?)getMpMethod.CreateDelegate(typeof(GetMasteryPointsDelegate));
                            if (del != null)
                            {
                                GetMasteryPoints = del;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load plugin {dll}: {ex.Message}");
                    LoggerService.Log("PluginLoadFailed", $"Failed to load plugin {dll}: {ex.Message}");
                }
            }
        }
    }
}
