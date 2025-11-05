namespace Warframe.Tracker.Filters
{
    public class ItemFilter
    {
        //Filters out items that can't actually get mastered or grant mastery points.
        public static bool GrantsMastery(string uniqueName, string category, int masteryReq, bool excludeFromCodex)
        {
            if (masteryReq < 0 || string.IsNullOrEmpty(category) || excludeFromCodex) return false;
            if(string.Equals(category, "Node", StringComparison.OrdinalIgnoreCase)) return false;

            var uname = (uniqueName ?? "").ToLowerInvariant();
            var catLower = category.ToLowerInvariant();
            //Filter for amp prisms
            if (uname.Contains("amps")) return uname.Contains("prism");
            //Filter for zaw strikes
            if (uname.Contains("modularmelee")) return uname.Contains("tip");
            //Filter for kitgun chambers
            if (uname.Contains("modularsecondary") || uname.Contains("infkitgun")) return uname.Contains("barrel");
            //Filter for moa pet heads
            if (uname.Contains("moapet")) return uname.Contains("moapethead");
            //Filter for k-drive boards
            if (uname.Contains("hoverboard")) return uname.Contains("deck");
            return true;
        }
    }
}
