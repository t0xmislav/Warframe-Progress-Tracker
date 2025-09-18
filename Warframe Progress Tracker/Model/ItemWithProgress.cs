using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Model
{
    class ItemWithProgress : INotifyPropertyChanged
    {
        public Item Item { get; set; }
        private UserProgress _progress;
        public UserProgress UserProgress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Owned));
                OnPropertyChanged(nameof(Mastered));
            }
        }

        public bool Owned
        {
            get => UserProgress?.Owned ?? false;
            set
            {
                if (UserProgress == null) return;
                UserProgress.Owned = value;
                UserProgress.DateOwned = value ? DateTime.UtcNow : (DateTime?)null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DateOwned));
            }
        }

        public bool Mastered
        {
            get => UserProgress?.Mastered ?? false;
            set
            {
                if (UserProgress == null) return;
                UserProgress.Mastered = value;
                UserProgress.DateMastered = value ? DateTime.UtcNow : (DateTime?)null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DateMastered));
            }
        }

        public DateTime? DateOwned => UserProgress?.DateOwned;
        public DateTime? DateMastered => UserProgress?.DateMastered;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
