using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Warframe_Progress_Tracker.Services;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for ItemEditWindow.xaml
    /// </summary>
    public partial class ItemEditWindow : Window
    {
        private Model.Item _item;
        public ItemEditWindow(Model.Item item)
        {
            InitializeComponent();
            _item = item;
            PreviewImage.Source = _item.ImageBitmap;
        }

        private void ChangeImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg:*.jpeg;*.jfif"
            };
            if (dialog.ShowDialog() == true)
            {
                var bytes = File.ReadAllBytes(dialog.FileName);
                _item.Image = bytes;

                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                PreviewImage.Source = bmp;
            }

        }
        private void SaveItem_Click(object sender, RoutedEventArgs e)
        {
            DbService.UpdateItem(_item);
            DialogResult = true;
            Close();

        }
        private void Exit_Click(object sender, RoutedEventArgs e) 
        {
            DialogResult = false;
            Close();
        }
    }
}
