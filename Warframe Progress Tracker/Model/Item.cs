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
    public class Item : CodexEntry
    {
        public string UniqueName { get; set; }
        public byte[] Image { get; set; }
        public BitmapImage ImageBitmap => ImageUtil.BytesToImage(Image);
        public string CategoryName => Category?.DisplayName ?? string.Empty;
        public string GetDisplayName() => Category != null ? $"{Category.DisplayName}/{Name}" : Name;
        

        public bool IsMastered(User user)
        {
            var progress = ProgressCacheUtil.GetItemProgress(user.Id, Id);
            return progress?.Mastered ?? false;
        }
        public Item Clone()
        {
            return new Item
            {
                Id = this.Id,
                UniqueName = this.UniqueName,
                Name = this.Name,
                Category = this.Category,
                Image = this.Image,
                MasteryPoints = this.MasteryPoints
            };
        }
    }
}
