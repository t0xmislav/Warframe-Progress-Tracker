using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Warframe_Progress_Tracker.Utils;

namespace Warframe_Progress_Tracker.Model
{
    public class Node : CodexEntry
    {
        public string Planet { get; set; }
        
        public byte[] Image { get; set; }
        public BitmapImage ImageBitmap => ImageUtil.BytesToImage(Image);
        public bool IsNormalCleared(User user)
        {
            var progress = ProgressCacheUtil.GetNodeProgress(user.Id, Id);
            return progress?.ClearedNormal ?? false;
        }
        public bool IsSpCleared(User user)
        {
            var progress = ProgressCacheUtil.GetNodeProgress(user.Id, Id);
            return progress?.ClearedSteelPath ?? false;
        }
        public string GetDisplayName()
        {
            return $"{Planet}/{Name}";
        }
        public Node Clone()
        {
            return new Node
            {
                Id = this.Id,
                Planet = this.Planet,
                Name = this.Name,
                Category = this.Category,
                Image = this.Image,
                MasteryPoints = this.MasteryPoints
            };
        }
    }
}
