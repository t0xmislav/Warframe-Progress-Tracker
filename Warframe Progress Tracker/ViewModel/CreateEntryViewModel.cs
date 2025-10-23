using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;

namespace Warframe_Progress_Tracker.ViewModel
{
    public class CreateEntryViewModel : INotifyPropertyChanged
    {
        private string _name;
        private Model.Category _selectedCategory;
        private int _masteryPoints;
        private string _planet;
        private byte[] _image;
        private string _uniqueName;
        private BitmapImage _imagePreview;

        private User _currentUser;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public Model.Category SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNode));
            }
        }

        public int MasteryPoints
        {
            get => _masteryPoints;
            set { _masteryPoints = value; OnPropertyChanged(); }
        }

        public string Planet
        {
            get => _planet;
            set { _planet = value; OnPropertyChanged(); }
        }

        public string UniqueName
        {
            get => _uniqueName;
            set { _uniqueName = value; OnPropertyChanged(); }
        }
        public byte[] Image
        {
            get => _image;
            set { _image = value; OnPropertyChanged(); }
        }
        public BitmapImage ImagePreview
        {
            get => _imagePreview;
            set { _imagePreview = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Model.Category> Categories { get; } = new();

        public bool IsNode => SelectedCategory?.DisplayName == "Node";

        public ICommand ChangeImageCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ExitCommand { get; }

        public event Action<bool> RequestClose;
        public CreateEntryViewModel(User currentUser)
        {
            _currentUser = currentUser;
            LoadCategories();
            ChangeImageCommand = new Utils.RelayCommand(_ => ChangeImage());
            SaveCommand = new Utils.RelayCommand(_ => Save(), _ => CanSave());
            ExitCommand = new Utils.RelayCommand(_ => Exit());
        }
        private bool CanSave() {

            if (SelectedCategory.DisplayName == "Node") return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Planet);
            return !string.IsNullOrWhiteSpace(Name);
        
        }
        private void LoadCategories()
        {
            var categories = DbService.GetCategories();
            Categories.Clear();
            foreach (var category in categories) Categories.Add(category);

            SelectedCategory = Categories.FirstOrDefault();
        }

        private void ChangeImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.jfif"
            };
            if(dialog.ShowDialog() == true)
            {
                var bytes = File.ReadAllBytes(dialog.FileName);
                Image = bytes;
                
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();

                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                ImagePreview = bmp;
            }
        }

        private void Save()
        {
            if(MasteryPoints < 0)
            {
                MessageBox.Show("Mastery Points must be a positive number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (IsNode)
            {
                var node = new Node
                {
                    Name = Name,
                    Planet = Planet,
                    MasteryPoints = MasteryPoints,
                    Image = Image,
                };
                DbService.AddNode(node);
            }
            else
            {
                var category = SelectedCategory.DisplayName;
                var uniqueName = string.IsNullOrWhiteSpace(UniqueName) ? GenerateUniqueName(category, Name) : UniqueName;
                if (DbService.IsUniqueNameTaken(uniqueName))
                {
                    MessageBox.Show("Unique Name already exists in the database, please choose a different unique name.", 
                        "Duplicate Unique Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var item = new Item
                {
                    Name = Name,
                    UniqueName = uniqueName,
                    Category = SelectedCategory,
                    Image = Image
                };
                DbService.AddItem(item);
            }

            Exit();
        }
        private string GenerateUniqueName(string category,  string name)
        {
            string baseName = $"{category}/{name}";
            string uniqueName = baseName;
            int counter = 1;
            while (DbService.IsUniqueNameTaken(uniqueName)) 
            {
                uniqueName = $"{uniqueName}{counter}";
                counter++;
            }
            return uniqueName;
        }
        private void Exit() => RequestClose?.Invoke(true);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) 
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    
}
