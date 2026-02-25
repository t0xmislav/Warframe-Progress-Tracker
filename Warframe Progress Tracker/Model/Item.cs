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
        public byte[]? Image { get; set; }
        private BitmapImage? _cachedImage;
        public BitmapImage ImageBitmap
        {
            get
            {
                if (_cachedImage == null && Image != null)
                    _cachedImage = ImageUtil.BytesToImage(Image);
                return _cachedImage;
            }
        }
        public string CategoryName => Category?.DisplayName ?? string.Empty;
        public string GetDisplayName() => Category != null ? $"{Category.DisplayName}/{Name}" : Name;
        
        public ItemProgress? getItemProgress(User user)
        {
            return ProgressCacheUtil.GetItemProgress(user.Id, Id);
        }
        public bool IsMastered(User user)
        {
            var progress = ProgressCacheUtil.GetItemProgress(user.Id, Id);
            return progress?.Mastered ?? false;
        }
        public bool IsOwned(User user)
        {
            var progress = ProgressCacheUtil.GetItemProgress(user.Id, Id);
            return progress?.Owned ?? false;
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

        public override bool Equals(object? obj)
        {
            return obj is Item item &&
                   Id == item.Id &&
                   Name == item.Name &&
                   MasteryPoints == item.MasteryPoints &&
                   EqualityComparer<Category>.Default.Equals(Category, item.Category) &&
                   UniqueName == item.UniqueName &&
                   EqualityComparer<byte[]>.Default.Equals(Image, item.Image) &&
                   EqualityComparer<BitmapImage>.Default.Equals(ImageBitmap, item.ImageBitmap) &&
                   CategoryName == item.CategoryName;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name, MasteryPoints, Category, UniqueName, Image, ImageBitmap, CategoryName);
        }
    }
}
