using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.ViewModel
{
    public class LogViewModel
    {
        public ObservableCollection<LogRowViewModel> Logs { get; set; } = new ObservableCollection<LogRowViewModel>();
        public User CurrentUser { get; }
        public bool IsAdmin => CurrentUser.IsAdmin;

        public LogViewModel(User user)
        {
            CurrentUser = user;
            var raw = LoggerService.LoadLogs();
            foreach (var entry in raw)
            {
                var rowVm = new LogRowViewModel(entry, Logs.Count, CurrentUser);
                rowVm.Deleted += (_, id) =>
                {
                    var vm = Logs.FirstOrDefault(x => x.Id == id);
                    if (vm != null) Logs.Remove(vm);
                };
                Logs.Add(rowVm);
            }

            if (CurrentUser.IsAdmin == false)
            {
                Debug.WriteLine("Filtering logs for non-admin user...");
            }
            else
            {
                Debug.WriteLine("Loading all logs for admin user...");
            }
        }
    }
}
