using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe.Tracker.Filters
{
    public class MasteryCalculator
    {
        public static int GetMasteryPoints(string category, string uniqueName)
        {
            category = (category ?? "").ToLowerInvariant();
            uniqueName = (uniqueName ?? "").ToLowerInvariant();
            //All weapons with max level 40
            if (uniqueName.Contains("kuva") || uniqueName.Contains("tenet") || uniqueName.Contains("coda") || uniqueName.Contains("paracesis"))
                return 4000;
            //Necramechs
            else if (uniqueName.Contains("necra"))
                return 8000;
            //Warframes/Companions/Archwings
            else if (category.Contains("warframe") || category.Contains("pets") || category.Contains("archwing") || category.Contains("sentinel"))
                return 6000;
            //Everything else... I hope
            else 
                return 3000;

        }
    }
}
