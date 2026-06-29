using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Utils;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.ViewModel
{
    public class LogRowViewModel : INotifyPropertyChanged
    {
        private readonly User _currentUser;
        private readonly RelayCommand _saveCommand;
        private readonly RelayCommand _deleteCommand;
        private readonly Action<Guid, string>? _onSave;
        private readonly Action<Guid>? _onDelete;
        public LogEntry Entry { get; }
        public Guid Id => Entry.Id;
        public int Index { get; }

        private string _adminNote = string.Empty;
        public string AdminNote
        {
            get => _adminNote;
            set
            {
                if (_adminNote == value) return;
                _adminNote = value;
                OnPropertyChanged();
                IsDirty = !string.Equals(OriginalAdminNote, _adminNote, StringComparison.Ordinal);
                _saveCommand.RaiseCanExecuteChanged();
            }
        }

        public string OriginalAdminNote { get; private set; }

        private bool _isDirty = false;
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value) return;
                _isDirty = value;
                OnPropertyChanged();
                _saveCommand.RaiseCanExecuteChanged();
            }
        }

        public ICommand SaveCommand => _saveCommand;
        public ICommand DeleteCommand => _deleteCommand;
        public DateTime Timestamp => Entry.Timestamp;
        public string Action => Entry.Action;
        public string Details => Entry.Details;
        public DateTime? LastModified => Entry.LastModified;
        public event EventHandler<Guid>? Deleted;

        public LogRowViewModel(LogEntry entry, int index, User currentUser)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Index = index;
            _currentUser = currentUser;
            OriginalAdminNote = entry.AdminNote ?? string.Empty;
            _adminNote = OriginalAdminNote;
            _saveCommand = new Utils.RelayCommand(_ => Save(), _ => IsDirty && _currentUser.IsAdmin);
            _deleteCommand = new Utils.RelayCommand(_ => Delete(), _ => _currentUser.IsAdmin);
        }

        private void Save()
        {
            try
            {
                bool success = LoggerService.EditLog(Entry.Id, AdminNote);
                if (success)
                {
                    Entry.AdminNote = AdminNote;
                    Entry.LastModified = DateTime.Now;
                    OriginalAdminNote = AdminNote;
                    IsDirty = false;
                    OnPropertyChanged(nameof(LastModified));

                }
                else
                {
                    MessageBox.Show("Failed to save admin note.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Delete()
        {
            try
            {
                bool success = LoggerService.DeleteLog(Entry.Id);
                
                if (success)
                {
                    Deleted?.Invoke(this, Entry.Id);
                    MessageBox.Show("Log entry deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to delete log entry.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
