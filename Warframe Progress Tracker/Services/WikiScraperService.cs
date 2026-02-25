using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.Services
{
    internal class WikiScraperService
    {
        private static string wikiUrl = "https://wiki.warframe.com";
        public static async Task<List<Model.Node>> ScrapeNodesAsync(IProgress<(string key, object[])>? progress, CancellationToken cancellationToken = default)
        {



            /*
            var web = new HtmlWeb
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
            };
            var chartDoc = await web.LoadFromWebAsync($"{wikiUrl}/w/Star_Chart");
            System.IO.File.WriteAllText("dump.html", chartDoc.DocumentNode.OuterHtml);
            var planetLinks = chartDoc.DocumentNode.SelectNodes("//table[contains(@class,'wikitable')]//a[@href]")
                    .Select(a => wikiUrl + a.GetAttributeValue("href", ""))
                    .Where(href => href.StartsWith("/wiki/"))
                    .Select(href => wikiUrl + href)
                    .Distinct()
                    .ToList();
           
            */
            progress?.Report((("LoadingScrapingNodesStr", new object[] { })));
            var nodes = new List<Model.Node>();
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--lang=en-US" }
            });

            var page = await browser.NewPageAsync();
            await page.GotoAsync($"{wikiUrl}/w/Star_Chart");
            var html = await page.ContentAsync();

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            //Select the table of planets(2nd table on the page) and get all links in the first column
            var planetLinks = doc.DocumentNode.SelectNodes("//table[2]//tr/td[1]//a[@href]")
                .Select(a => wikiUrl + a.GetAttributeValue("href", ""))
                .Distinct()
                .ToList();

            foreach (var planetUrl in planetLinks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await page.GotoAsync(planetUrl);
                var planetHtml = await page.ContentAsync();
                var planetDoc = new HtmlAgilityPack.HtmlDocument();
                planetDoc.LoadHtml(planetHtml);
                //Get the planet name from the title tag, which is usually in the format "Planet - Warframe Wiki"
                var planetName = planetDoc.DocumentNode.SelectSingleNode("//title").InnerText.Trim();
                //Remove " - Warframe Wiki" suffix from title if present
                planetName = Regex.Replace(planetName, @"\s*-\s*Warframe Wiki\s*$", "", RegexOptions.IgnoreCase).Trim();
                IProgress<double>? imgProgress = new Progress<double>(p => progress?.Report(("DownloadingImageProgressStr", 
                    new object[] { planetName, (int)(p * 100) })));
                int speedLimitKB = 0;
                int.TryParse(IniFileService.Read("Download", "SpeedLimit", "0"), out speedLimitKB);
                int speedLimitBytes = Math.Max(0, speedLimitKB) * 1024;
                byte[]? planetImgData = null;
                //Select the image in the infobox, which is usually the first image in a span inside the infobox div
                var imgNode = planetDoc.DocumentNode.SelectSingleNode("//*[@id=\"mw-content-text\"]/div[contains(@class, 'mw-content-ltr')]/div[contains(@class, 'infobox')]/span//img");
                //Get the src attribute of the image node, which is the relative image url
                var planetImgUrl = imgNode != null ? imgNode.GetAttributeValue("src", null) : null;
                //Prepend wikiurl to the image url
                planetImgUrl = wikiUrl + planetImgUrl;
                Debug.WriteLine($"ImageUrl: {planetImgUrl}");
                try
                {
                    planetImgData = await PlaywrightDownloadUtil.DownloadDataAsync(planetImgUrl, page, 
                        imgProgress, speedLimitBytes, cancellationToken);
                }
                catch(OperationCanceledException)
                {
                    LoggerService.Log("Download Canceled", "Canceled image download while scraping nodes");
                }catch(Exception ex)
                {
                    Debug.WriteLine($"Downloading image failed for {planetImgUrl} with error {ex.Message}");
                    LoggerService.Log("Download Failed", $"Failed to download image for {planetName} with error {ex.Message}");
                }

                var nodeTables = planetDoc.DocumentNode.SelectNodes("//table[contains(@class,'wikitable')]");
                if (nodeTables == null) continue;
                progress?.Report(("LoadingPlanetNodesStr", new object[] { planetName }));
                foreach (var table in nodeTables)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var rows = table.SelectNodes(".//tr[td]");
                    if(rows == null) continue;
                    System.Diagnostics.Debug.WriteLine(planetName);
                    foreach(var row in rows)
                    {
                        var cells = row.SelectNodes("./td");
                        if (cells == null || cells.Count < 3) continue;

                        var nodeName = WebUtility.HtmlDecode(cells.ElementAtOrDefault(1)?.InnerText?.Trim() ?? "");

                        var masteryText = WebUtility.HtmlEncode(cells.ElementAtOrDefault(7)?.InnerText?.Trim() ?? "");
                        masteryText = Regex.Replace(masteryText, @"\[\d+\]", "");
                        var numMatch = Regex.Match(masteryText, @"[-\d,]+");

                        if (!numMatch.Success) continue;
                        var digits = numMatch.Value;

                        if (!int.TryParse(digits, System.Globalization.NumberStyles.AllowThousands | 
                            System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out int masteryXp))
                            continue;
                        System.Diagnostics.Debug.WriteLine(masteryXp);

                        nodes.Add(new Model.Node
                        {
                            Name = nodeName,
                            MasteryPoints = masteryXp,
                            Planet = planetName,
                            Image = planetImgData
                        });
                        System.Diagnostics.Debug.WriteLine("Node Created?");
                    }
                }
            }
            return nodes;
        }

        

    }
}
