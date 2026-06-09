using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.Utils
{
    public class ReportGeneratorUtil
    {
        public static string SaveReportXml(User user)
        {
            var categories = DbService.GetCategories();
            var progress = new List<XElement>();
            foreach (var category in categories) {
                if(category.DisplayName.Equals("Node")) continue;
                var items = DbService.GetItemByCategory(category);
                var mastered = items.Count(i => DbService.GetProgressForItem(user, i)?.Mastered == true);
                progress.Add(new XElement("Category",
                    new XAttribute("Name", category.DisplayName),
                    new XAttribute("Total", items.Count),
                    new XAttribute("Mastered", mastered)));
            }
            progress.Add(new XElement("Category",
                new XAttribute("Name", "Normal Nodes"),
                new XAttribute("Total", DbService.GetAllNodes().Count),
                new XAttribute("Mastered", DbService.GetAllNodes().Count(n => DbService.GetProgressForNode(user, n)?.ClearedNormal == true))));
            progress.Add(new XElement("Category",
                new XAttribute("Name", "Steel Path Nodes"),
                new XAttribute("Total", DbService.GetAllNodes().Count),
                new XAttribute("Mastered", DbService.GetAllNodes().Count(n => DbService.GetProgressForNode(user, n)?.ClearedSteelPath == true))));
            var doc = new XDocument(
                new XElement("Report",
                new XAttribute("User", user.Name),
                new XElement("Categories", progress)
            ));
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "progress.xml");
            doc.Save(path);
            LoggerService.Log("Progress Xml created", $"{user.Name} created Xml progress file");
            return path;
        }
        public static async Task<(bool Success, string? pdfPath)> GenerateReportAsync(string xmlPath, string pdfPath)
        {
            string workerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReportWorker", "ReportWorker.exe");
           
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = workerPath,
                    Arguments = $"\"{xmlPath}\" \"{pdfPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Debug.WriteLine($"ReportWorker Output: {output}");
            Debug.WriteLine($"ReportWorker Errors: {errors}");
            if (process.ExitCode == 0)
            {
                LoggerService.Log("Report generated", errors);
                return (true, output.Trim());
            }
            return (false, errors);
        }
    }
}
