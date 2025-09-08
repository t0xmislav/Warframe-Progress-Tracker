using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Model
{
    class UserProgress
    {
        public int ItemId { get; set; }
        public bool Owned { get; set; }
        public bool Mastered { get; set; }
        public DateTime DateOwned { get; set; }
        public DateTime DateMastered { get; set; }
    }
}
