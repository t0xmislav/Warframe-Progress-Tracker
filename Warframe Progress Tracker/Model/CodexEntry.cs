using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Model
{
    abstract class CodexEntry
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int MasteryPoints { get; set; }

        public Category Category { get; set; }


    }
}
