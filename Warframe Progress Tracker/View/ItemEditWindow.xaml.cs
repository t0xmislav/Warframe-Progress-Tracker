using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for ItemEditWindow.xaml
    /// </summary>
    public partial class ItemEditWindow : Window
    {
        private Model.Item _item;
        private User _currentUser;
        //For logging purposes
        private Item _oldItem;
        public ObservableCollection<Category> Categories { get; } = new();
        private readonly Action<Model.Item>? _onSaved;
        public Category? SelectedCategory { get; set; }
        //Cloned as to not actively change the item in the codex view while editing
        public Item EditableItem { get; }

        public ItemEditWindow(Item item, User user, Action<Model.Item>? onSaved = null)
        {
            InitializeComponent();
            _item = item;
            _currentUser = user;
            _onSaved = onSaved;
            EditableItem = item.Clone();
            _oldItem = item.Clone();
            DataContext = EditableItem;
            if (_item.Image != null)
            {
                PreviewImage.Source = EditableItem.ImageBitmap;
            }
            LoadCategories();
            _currentUser = user;
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
                EditableItem.Image = bytes;

                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                PreviewImage.Source = bmp;
            }

        }
        private async void SaveItem_Click(object sender, RoutedEventArgs e)
        {
            _item.Name = EditableItem.Name;
            _item.Category = SelectedCategory;
            _item.MasteryPoints = EditableItem.MasteryPoints;
            _item.Image = EditableItem.Image;
            await Task.Run(async() =>
            {
                DbService.UpdateItem(_item);
                LoggerService.LogItemChanges(_oldItem, _item, _currentUser);
            });
            _onSaved?.Invoke(_item);
            MessageBox.Show((string)Application.Current.Resources["ItemUpdatedStr"]);
            DialogResult = true;
            Close();

        }
        private void Exit_Click(object sender, RoutedEventArgs e) 
        {
            DialogResult = false;
            Close();
        }
        private void LoadCategories()
        {
            var categories = DbService.GetCategories();

            Categories.Clear();
            foreach (var c in categories)
            {
                if(!c.DisplayName.Equals("Node"))
                    Categories.Add(c);
            }
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == _item.Category?.Id);
        }
    }
}
