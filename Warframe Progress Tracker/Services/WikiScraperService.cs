using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using HtmlAgilityPack;

namespace Warframe_Progress_Tracker.Services
{
    internal class WikiScraperService
    {
        private static string wikiUrl = "https://wiki.warframe.com";
        public static async Task<List<Model.Node>> ScrapeNodesAsync()
        {
            
            var web = new HtmlWeb();
            var chartDoc = await web.LoadFromWebAsync($"{wikiUrl}/w/Star_Chart");

            var planetLinks = chartDoc.DocumentNode.SelectNodes("//table[contains@class,'wikitable')]//a")
                .Select(a => wikiUrl + a.GetAttributeValue("href", ""))
                .Distinct()
                .ToList();
            var nodes = new List<Model.Node>();

            foreach (var planetUrl in planetLinks)
            {
                var planetDoc = await web.LoadFromWebAsync(planetUrl);
                var planetName = planetDoc.DocumentNode.SelectSingleNode("//h1").InnerText.Trim();

                var nodeTables = planetDoc.DocumentNode.SelectNodes("//table[contains@class,'wikitable')]");
                if (nodeTables == null) continue;

                foreach(var table in nodeTables)
                {
                    var rows = table.SelectNodes(".//tr[td]");
                    if(rows == null) continue;

                    foreach(var row in rows)
                    {
                        var cells = row.SelectNodes("./td");
                        if (cells == null || cells.Count < 3) continue;
                  
                        var nodeName = cells[1].InnerText.Trim();
                        var masteryXp = int.Parse(cells[7].InnerText.Trim());

                        nodes.Add(new Model.Node
                        {
                            Name = nodeName,
                            MasteryPoints = masteryXp
                        });
                    }
                }
            }
            return nodes;
        }
    }
}
