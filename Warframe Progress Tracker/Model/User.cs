using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Warframe_Progress_Tracker.Utils;

namespace Warframe_Progress_Tracker.Model
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string PasswordHash { get; set; }
        public string? WarframeDisplayName { get; set; }
        public string? WarframeAccountId { get; set; }
        public string? Platform {  get; set; }
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
        public bool IsAdmin { get; set; } = false;


    }
}
