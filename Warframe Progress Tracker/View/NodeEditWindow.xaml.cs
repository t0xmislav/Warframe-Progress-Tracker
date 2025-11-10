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
using Warframe_Progress_Tracker.Utils;

namespace Warframe_Progress_Tracker.View
{
    /// <summary>
    /// Interaction logic for NodeEditWindow.xaml
    /// </summary>
    public partial class NodeEditWindow : Window
    {
        private Model.Node _node;

        public Model.Node EditableNode { get; }
        public NodeEditWindow(Model.Node node)
        {
            InitializeComponent();
            _node = node;
            EditableNode = _node.Clone();
            DataContext = EditableNode;

            if (_node.Image != null) 
            {
                PreviewImage.Source = _node.ImageBitmap;
            }

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
                _node.Image = bytes;

                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                PreviewImage.Source = bmp;
            }

        }
        private void SaveNode_Click(object sender, RoutedEventArgs e)
        {
            _node.Name = EditableNode.Name;
            _node.Planet = EditableNode.Planet;
            _node.MasteryPoints = EditableNode.MasteryPoints;
            _node.Image = EditableNode.Image;
            ThreadPoolManager.QueueDatabaseWrite(async() =>
            {
                DbService.UpdateNode(_node);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show((string)Application.Current.Resources["NodeUpdatedStr"]);
                });
            });
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
