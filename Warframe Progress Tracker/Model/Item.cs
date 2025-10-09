using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;

namespace Warframe_Progress_Tracker.Model
{
    class Item : CodexEntry
    {
        public string UniqueName { get; set; }
        public int MasteryPoint { get; set; }
        public bool Owned { get; set; }
        public DateTime? DateOwned { get; set; }
        public bool Mastered { get; set; }
        public DateTime? DateMastered { get; set; }
        public byte[] Image { get; set; }
        public BitmapImage ImageBitmap => ImageUtil.BytesToImage(Image);
    }
}
