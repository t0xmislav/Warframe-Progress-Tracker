using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe.Tracker.Filters
{
    public class MasteryCalculator
    {
        //Automatically assigns mastery points based on category and unique name.
        public static int GetMasteryPoints(string category, string uniqueName)
        {
            category = (category ?? "").ToLowerInvariant();
            uniqueName = (uniqueName ?? "").ToLowerInvariant();
            //All weapons with max level of 40
            if (uniqueName.Contains("kuva") || uniqueName.Contains("tenet") || uniqueName.Contains("coda") || uniqueName.Contains("paracesis"))
                return 4000;
            //Necramechs grant 200 mp per level with a max level of 40
            else if (uniqueName.Contains("necra"))
                return 8000;
            //Warframes/Companions/Archwings grant 200 mp per level
            else if (category.Contains("warframe") || category.Contains("pets") || category.Contains("archwing") || category.Contains("sentinel"))
                return 6000;
            //Everything else grants 100 mp per level with a max level of 30
            else 
                return 3000;

        }
    }
}
