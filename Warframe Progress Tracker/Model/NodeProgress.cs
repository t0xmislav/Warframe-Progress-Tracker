using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Model
{
    public class NodeProgress
    {
        public User User { get; set; }
        public Node Node { get; set; }
        public bool ClearedNormal { get; set; }
        public bool ClearedSteelPath { get; set; }
        public DateTime? DateNormalClear { get; set; }
        public DateTime? DateSteelPathClear { get; set; }

        public DateTime GetClearedDate() => (DateSteelPathClear.GetValueOrDefault() > DateTime.MinValue ? DateSteelPathClear.GetValueOrDefault() :
            DateNormalClear.GetValueOrDefault() > DateTime.MinValue ? DateNormalClear.GetValueOrDefault() :
            DateTime.MinValue);
    }
}
