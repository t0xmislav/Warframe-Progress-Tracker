using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Models
{
    class Item
    {
        public string UniqueName { get; set; }
        public string Name { get; set; }
        public Category Category { get; set; }
        public string ImageUrl { get; set; }
    }
}
