using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;

namespace Warframe_Progress_Tracker.Utils
{
    public static class XmlUtil
    {
        public static string GenerateUserProgressXml(User user)
        {
            var items = DbService.GetAllItems();
            var nodes = DbService.GetAllNodes();

            var doc = new XDocument(new XElement("CodexProgressSnapshot",
                new XAttribute("Generated", DateTime.UtcNow.ToString("o")),
                new XElement("User",
                    new XAttribute("Id", user.Id),
                    new XAttribute("Name", user.Name)
                ),
                new XElement("ItemProgress",
                items.Select(it =>
                {
                    var prog = it.getItemProgress(user);
                    return new XElement("Item",
                        new XAttribute("id", it.Id),
                        new XAttribute("UniqueName", it.UniqueName),
                        new XAttribute("Owned", prog?.Owned.ToString().ToLower() ?? "false"),
                        new XAttribute("Mastered", prog?.Mastered.ToString().ToLower() ?? "false"),
                        new XAttribute("DateOwned", prog?.DateOwned?.ToString("o") ?? ""),
                        new XAttribute("DateMastered", prog?.DateMastered?.ToString("o") ?? "")
                    );
                })
                ),
                new XElement("NodeProgress",
                nodes.Select(n =>
                {
                    var prog = DbService.GetProgressForNode(user, n);
                    return new XElement("Node",
                        new XAttribute("id", n.Id),
                        new XAttribute("Name", n.Name),
                        new XAttribute("ClearedNormal", prog?.ClearedNormal.ToString().ToLower() ?? "false"),
                        new XAttribute("ClearedSteelPath", prog?.ClearedSteelPath.ToString().ToLower() ?? "false"),
                        new XAttribute("DateClearedNormal", prog?.DateNormalClear?.ToString("o") ?? ""),
                        new XAttribute("DateClearedSteelPath", prog?.DateSteelPathClear?.ToString("o") ?? "")
                    );
                })
                )
                ));
            return doc.ToString(SaveOptions.DisableFormatting);
        }
    }
}
