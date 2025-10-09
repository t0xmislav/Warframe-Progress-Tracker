using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Model
{
    class ItemProgress
    {
        public Item Item { get; set; }
        public User User { get; set; }
        public bool Owned { get; set; }
        public bool Mastered { get; set; }
        public DateTime? DateOwned { get; set; }
        public DateTime? DateMastered { get; set; }
    }
}
