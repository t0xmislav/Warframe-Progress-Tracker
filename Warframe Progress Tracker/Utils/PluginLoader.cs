using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
        public static void LoadSnapshotWindow(string pluginsFolder, User user)
        {
            var dllPath = Path.Combine(pluginsFolder, "CodexSnapshotPlugin.dll");
            if (!File.Exists(dllPath))
            {
                MessageBox.Show((string)Application.Current.Resources["SnapshotPluginMissingStr"], (string)Application.Current.Resources["ErrorStr"], MessageBoxButton.OK, MessageBoxImage.Error);
                LoggerService.Log(dllPath, $"{user.Name}: Failed to load snapshot plugin | Reason: DLL not found");
                return;
            }

            try
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
                var type = asm.GetType("Warframe.Tracker.CodexSnapshotPlugin.CodexSnapshotPlugin");
                if (type is null)
                {
                    MessageBox.Show((string)Application.Current.Resources["SnapshotPluginLoadFailedStr"],
                        (string)Application.Current.Resources["ErrorStr"], MessageBoxButton.OK, MessageBoxImage.Error);
                    LoggerService.Log(dllPath, $"{user.Name}: Failed to load snapshot plugin | Reason: Plugin class not found");
                    return;
                }

                var mi = type.GetMethod("CreateDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (mi is null)
                {
                    MessageBox.Show((string)Application.Current.Resources["SnapshotPluginLoadFailedStr"],
                        (string)Application.Current.Resources["ErrorStr"], MessageBoxButton.OK, MessageBoxImage.Error);
                    LoggerService.Log(dllPath, $"{user.Name}: Failed to load snapshot plugin | Reason: Initialize method not found");
                    return;
                }

                var win = mi.Invoke(null, new object[] { }) as Window;
                if (win is null)
                {
                    MessageBox.Show((string)Application.Current.Resources["SnapshotPluginLoadFailedStr"],
                        (string)Application.Current.Resources["ErrorStr"], MessageBoxButton.OK, MessageBoxImage.Error);
                    LoggerService.Log(dllPath, $"{user.Name}: Failed to load snapshot plugin | Reason: Initialize method did not return a Window");
                    return;
                }

                win.Owner = Application.Current.MainWindow;
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{(string)Application.Current.Resources["SnapshotPluginLoadFailedStr"]} {ex.Message}",
                    (string)Application.Current.Resources["ErrorStr"], MessageBoxButton.OK, MessageBoxImage.Error);
                LoggerService.Log("Snapshot Plugin Load Failed", $"{user.Name}: Failed to load snapshot plugin | Exception: {ex}");
            }
        }
    }
}
