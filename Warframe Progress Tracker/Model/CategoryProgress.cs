using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Model
{
    //Total progress for each category
    public class CategoryProgress
    {
        public Category Category { get; set; }
        public int TotalItems { get; set; }
        public int MasteredItems { get; set; }
        public string DisplayText => $"{MasteredItems}/{TotalItems} Cleared";
        public double Progress => TotalItems == 0 ? 0 : (double)MasteredItems / TotalItems * 100;
        
    }
}
