using CodexSnapshotPlugin;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
namespace Warframe.Tracker.CodexSnapshotPlugin
{
    // Minimal model classes (avoid referencing main app models)
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = "";
        public string Details { get; set; } = "";
        public DateTime? LastModified { get; set; }
        public string? AdminNote { get; set; }
    }

    public class User
    {
        public string Name { get; set; } = "";
        public bool IsAdmin { get; set; }
    }

    // ========== ViewModels ==========

    public class LogRowViewModel : INotifyPropertyChanged
    {
        private readonly User _currentUser;
        private readonly RelayCommand _saveCommand;

        public LogEntry Entry { get; }
        public int Index { get; }

        private string _adminNote = string.Empty;
        public string AdminNote
        {
            get => _adminNote;
            set
            {
                if (_adminNote == value) return;
                _adminNote = value ?? string.Empty;
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

        public DateTime Timestamp => Entry.Timestamp;
        public string Action => Entry.Action;
        public string Details => Entry.Details;
        public DateTime? LastModified => Entry.LastModified;

        private Action<int, string>? _onSave;

        public LogRowViewModel(LogEntry entry, int index, User currentUser, Action<int, string>? onSave = null)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Index = index;
            _currentUser = currentUser;
            _onSave = onSave;
            OriginalAdminNote = entry.AdminNote ?? string.Empty;
            _adminNote = OriginalAdminNote;

            _saveCommand = new RelayCommand(_ => Save(), _ => IsDirty && _currentUser.IsAdmin);
        }

        private void Save()
        {
            try
            {
                _onSave?.Invoke(Index, AdminNote);
                Entry.AdminNote = AdminNote;
                Entry.LastModified = DateTime.Now;
                OriginalAdminNote = AdminNote;
                IsDirty = false;
                OnPropertyChanged(nameof(LastModified));
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

    public class LogViewModel
    {
        public ObservableCollection<LogRowViewModel> Logs { get; } = new();
        public User CurrentUser { get; }
        public bool IsAdmin => CurrentUser.IsAdmin;

        private Action<int, string>? _onSaveNote;

        public LogViewModel(User user, List<LogEntry> logEntries, Action<int, string>? onSaveNote = null)
        {
            CurrentUser = user;
            _onSaveNote = onSaveNote;

            int i = 0;
            foreach (var entry in logEntries)
            {
                Logs.Add(new LogRowViewModel(entry, i++, CurrentUser, _onSaveNote));
            }
        }
    }

    // Simplified RelayCommand for plugin use
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool> _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (_ => true);
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    // ========== Main Plugin API ==========

    public static class CodexSnapshotPlugin
    {
        public static Window CreateLogDialog(User user, List<LogEntry> logEntries, Action<int, string>? onSaveNote = null)
        {
            var vm = new LogViewModel(user, logEntries, onSaveNote);

            var window = new Window
            {
                Title = "Activity Log",
                Width = 1000,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var grid = new Grid();
            var control = new LogViewControl { DataContext = vm };
            grid.Children.Add(control);

            window.Content = grid;
            return window;
        }

    }

}