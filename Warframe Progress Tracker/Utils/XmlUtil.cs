using System.Diagnostics;
using System.Xml.Linq;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils.Logger;

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
                        new XAttribute("Id", it.Id),
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
                        new XAttribute("Id", n.Id),
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
        public static bool ApplyProgressSnapshot(string xmlContent)
        {
            try
            {
                var doc = XDocument.Parse(xmlContent);
                var root = doc.Root;
                if (root is null) return false;
                var userId = root.Element("User")?.Attribute("Id")?.Value;
                Debug.WriteLine($" UserId: {userId}");
                if (userId == null) return false;
                var itemProgresses = root.Element("ItemProgress")?.Elements("Item");
                foreach (var itemProgress in itemProgresses ?? Enumerable.Empty<XElement>())
                {
                    var id = itemProgress.Attribute("Id")?.Value;
                    var owned = itemProgress.Attribute("Owned")?.Value == "true";
                    var mastered = itemProgress.Attribute("Mastered")?.Value == "true";
                    var dateOwned = DateTime.TryParse(itemProgress.Attribute("DateOwned")?.Value, out var dow) ? dow : (DateTime?)null;
                    var dateMastered = DateTime.TryParse(itemProgress.Attribute("DateMastered")?.Value, out var dm) ? dm : (DateTime?)null;
                    if (id is not null)
                    {
                        Debug.WriteLine("Writing progress for item " + id);
                        DbService.SetItemProgress(int.Parse(userId), int.Parse(id), mastered, owned, dateOwned, dateMastered);
                    }
                }
                var nodeProgresses = root.Element("NodeProgress")?.Elements("Node");
                foreach (var nodeProgress in nodeProgresses ?? Enumerable.Empty<XElement>())
                {
                    var id = nodeProgress.Attribute("Id")?.Value;
                    var cleared = nodeProgress.Attribute("Cleared")?.Value == "true";
                    var clearedSteelPath = nodeProgress.Attribute("ClearedSteelPath")?.Value == "true";
                    var dateNormalCleared = DateTime.TryParse(nodeProgress.Attribute("DateClearedNormal")?.Value, out var dnc) ? dnc : (DateTime?)null;
                    var dateSteelPathCleared = DateTime.TryParse(nodeProgress.Attribute("DateClearedSteelPath")?.Value, out var dspc) ? dspc : (DateTime?)null;
                    if (id is not null)
                    {
                        Debug.WriteLine("Writing progress for item " + id);
                        DbService.SetNodeProgress(int.Parse(userId), int.Parse(id), cleared, clearedSteelPath, dateNormalCleared, dateSteelPathCleared);
                    }

                }
                return true;
            }
            catch
            {
                LoggerService.Log("Failed to apply progress snapshot", "Saving progress snapshot to database failed");
                return false;
            }
        }
    }
}
