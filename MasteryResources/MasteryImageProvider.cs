using System.Diagnostics;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace MasteryResources
{
    public static class MasteryImageProvider
    {
        private static readonly Assembly Assembly = typeof(MasteryImageProvider).Assembly;
        public static BitmapImage? GetMasteryImage(int rank)
        {
            try
            {
                string resourceName;
                if (rank < 1)
                {
                    resourceName = "Unranked.png";
                }
                else
                {
                    resourceName = $"IconRank{rank}.png";
                }
                foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                {
                    Console.WriteLine(name);
                }
                using var stream = Assembly.GetManifestResourceStream($"MasteryResources.Resources.{resourceName}");
                Debug.WriteLine(stream != null
                    ? $"Found resource stream for {resourceName}."
                    : $"Resource stream for {resourceName} not found.");
                if (stream == null) return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading mastery image: {ex.Message}");
            }
            return null;
        }
    }
}
