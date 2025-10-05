using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Warframe_Progress_Tracker.Utils;

namespace Warframe_Progress_Tracker.Model
{
    internal class Node
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int MasteryPoints { get; set; }
        public byte[] Image { get; set; }
        public BitmapImage ImageBitmap => ImageUtil.BytesToImage(Image);
    }
}
