using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.Utils
{
    public class ReportGeneratorUtil
    {
        public static void GenerateUserProgressReport(User user, string outputPath)
        {
            PdfDocument document = new PdfDocument();
            document.Info.Title = "User Progress Report";
            PdfPage page = document.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(page);
            XFont titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
            XFont categoryFont = new XFont("Arial", 14, XFontStyleEx.Bold);
            XFont textFont = new XFont("Arial", 12, XFontStyleEx.Regular);

            double totalItems = 0;
            double totalMastered = 0;

            double y = 50;
            gfx.DrawString($"User Progress report for {user.Name}", titleFont, XBrushes.Black, new XPoint(50, y));
            y += 40;
            var categories = DbService.GetCategories();

            foreach (var category in categories) 
            {
                if (category.DisplayName == "Node") continue;
                var items = DbService.GetItemByCategory(category);
                var masteredCount = items.Count(i => DbService.GetProgressForItem(user, i)?.Mastered == true);

                totalItems += items.Count;
                totalMastered += masteredCount;

                double completionPercentage = items.Count == 0 ? 0 : (double)masteredCount / items.Count * 100;

                gfx.DrawString($"Category: {category.DisplayName}", categoryFont, XBrushes.Black, new XPoint(50, y));
                y += 15;
                gfx.DrawString($"Mastered: {masteredCount}", textFont, XBrushes.Black, new XPoint(50, y));
                y += 15;
                gfx.DrawString($"Completion: {completionPercentage:F2}%", textFont, XBrushes.Black, new XPoint(50, y));
                y += 25;

                if(y > page.Height - XUnit.FromPoint(100))
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = 50;
                }
            }
            var nodes = DbService.GetAllNodes();
            var normalCleared = nodes.Count(n => DbService.GetProgressForNode(user, n)?.ClearedNormal == true);
            var spCleared = nodes.Count(n => DbService.GetProgressForNode(user, n)?.ClearedSteelPath == true);

            totalItems += nodes.Count;
            totalMastered += normalCleared + spCleared;
            double completionNormalNodes = nodes.Count == 0 ? 0 : (double)normalCleared / nodes.Count * 100;
            gfx.DrawString("Category: Normal Nodes", categoryFont, XBrushes.Black, new XPoint(50, y));
            y += 15;
            gfx.DrawString($"Cleared: {normalCleared}", textFont, XBrushes.Black, new XPoint(50, y));
            y += 15;
            gfx.DrawString($"Completion: {completionNormalNodes:F2}%", textFont, XBrushes.Black, new XPoint(50, y));
            y += 25;


            if (y > page.Height - XUnit.FromPoint(100))
            {
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                y = 50;
            }

            double completionSteelPathNodes = nodes.Count == 0 ? 0 : (double)spCleared / nodes.Count * 100;
            gfx.DrawString("Category: Steel Path Nodes", categoryFont, XBrushes.Black, new XPoint(50, y));
            y += 15;
            gfx.DrawString($"Cleared: {spCleared}", textFont, XBrushes.Black, new XPoint(50, y));
            y += 15;
            gfx.DrawString($"Completion: {completionSteelPathNodes:F2}%", textFont, XBrushes.Black, new XPoint(50, y));
            y += 25;
            double overallCompletion = totalItems == 0 ? 0 : (totalMastered / totalItems * 100);
            gfx.DrawString($"Total mastery completion: {overallCompletion:F2}%", titleFont, XBrushes.Black, new XPoint(50, y));

            var timestamp = DateTime.Now.ToString("dd.MM.yyyy-HH:mm:ss");
            outputPath = Path.Combine(outputPath, $"Report {user.Name}-{timestamp}.pdf");
            document.Save(outputPath);
            LoggerService.Log("Report generated", $"{user.Name} generated report to folder: {outputPath}");
        }
        public static string SaveReportXml(User user)
        {
            var categories = DbService.GetCategories();
            var progress = new List<XElement>();
            foreach (var category in categories) {
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
                new XAttribute("User", user.Name)),
                new XElement("Categories", progress)
            );
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "progress.xml");
            doc.Save(path);
            return path;
        }
        public static async Task<(bool Success, string? pdfPath)> GenerateReportAsync(string xmlPath, string pdfPath)
        {
            string workerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExternalTools", "ReportWorker.exe");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ReportWorker.exe",
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
            if(process.ExitCode == 0)
            {
                LoggerService.Log("Report generated", errors);
                return (true, output.Trim());
            }
            return (false, errors);
        }
    }
}
