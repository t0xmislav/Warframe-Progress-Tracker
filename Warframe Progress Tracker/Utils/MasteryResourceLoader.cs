using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Warframe_Progress_Tracker.Utils
{
    public static class MasteryResourceLoader
    {
        private static Assembly? _masteryAssembly;
        private static Type? _imageProviderType;

        public static BitmapImage? GetMasteryRankImage(int rank)
        {
            try
            {
                if (_masteryAssembly == null)
                {
                    _masteryAssembly = LoadMasteryResourcesAssembly();
                    if (_masteryAssembly == null){
                        System.Diagnostics.Debug.WriteLine("MasteryResources assembly could not be loaded.");
                        return null; 
                    }

                    _imageProviderType = _masteryAssembly.GetType("MasteryResources.MasteryImageProvider");
                    if (_imageProviderType == null) { 
                        System.Diagnostics.Debug.WriteLine("MasteryImageProvider type not found in MasteryResources assembly.");
                        return null; 

                    }
                }

                var method = _imageProviderType.GetMethod("GetMasteryImage",
                    BindingFlags.Public | BindingFlags.Static);

                if (method == null) return null;

                var result = method.Invoke(null, new object[] { rank }) as BitmapImage;
                System.Diagnostics.Debug.WriteLine(result != null
                    ? $"Successfully loaded mastery image for rank {rank}."
                    : $"Failed to load mastery image for rank {rank}.");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load mastery image: {ex.Message}");
                return null;
            }
        }

        private static Assembly? LoadMasteryResourcesAssembly()
        {
            try
            {
                var pluginsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                var dllPath = Path.Combine(
                    pluginsFolder,
                    "MasteryResources.dll"
                );

                if (!File.Exists(dllPath))
                {
                    System.Diagnostics.Debug.WriteLine($"MasteryResources DLL not found at {dllPath}");
                    return null;
                }

                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
                return asm;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load MasteryResources assembly: {ex.Message}");
                return null;
            }
        }
    }
}
