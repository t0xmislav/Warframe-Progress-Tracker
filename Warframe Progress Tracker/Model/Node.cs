using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.Model
{
    public class Node : CodexEntry
    {
        public string Planet { get; set; }
        
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
        public NodeProgress? GetNodeProgress(User user)
        {
            return ProgressCacheUtil.GetNodeProgress(user.Id, Id);
        }
        public async Task<NodeProgress> UpdateProgressAsync(User user, bool clearedNormal, bool clearedSp)
        {
            return await Task.Run(() =>
            {
                DbService.UpdateNodeProgress(
                    user.Id,
                    this.Id,
                    clearedNormal,
                    clearedSp
                );

                var progress = DbService.GetProgressForNode(user, this) ?? new NodeProgress { User = user, Node = this };

                ProgressCacheUtil.StoreNodeProgress(user.Id, this.Id, progress);

                LoggerService.Log("Changed Node Progress", $"{user.Name}: Changed progress for: {this.Name} | Normal clear: {progress.ClearedNormal.ToString().ToLower()} | Steel Path clear: {progress.DateSteelPathClear.ToString().ToLower()}.");

                return progress;
            });
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

        public override bool Equals(object? obj)
        {
            return obj is Node node &&
                   Id == node.Id &&
                   Name == node.Name &&
                   MasteryPoints == node.MasteryPoints &&
                   EqualityComparer<Category>.Default.Equals(Category, node.Category) &&
                   Planet == node.Planet &&
                   EqualityComparer<byte[]>.Default.Equals(Image, node.Image) &&
                   EqualityComparer<BitmapImage>.Default.Equals(ImageBitmap, node.ImageBitmap);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name, MasteryPoints, Category, Planet, Image, ImageBitmap);
        }
    }
}
