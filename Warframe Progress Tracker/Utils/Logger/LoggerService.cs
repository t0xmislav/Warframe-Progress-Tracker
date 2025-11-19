using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Warframe_Progress_Tracker.Model;

namespace Warframe_Progress_Tracker.Utils.Logger
{
    public static class LoggerService
    {
        private static readonly string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs.json");

        private static readonly object _lock = new();

        private static readonly List<LogEntry> _entries = new();

        public static void Log(string action, string? details = null)
        {
            Task.Run(() =>
            {
                lock (_lock)
                {
                    var entry = new LogEntry
                    {
                        Timestamp = DateTime.Now,
                        Action = action,
                        Details = details
                    };
                    _entries.Add(entry);
                    SaveToFile();
                }
            });
        }
        public static void SaveToFile() 
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.AppendAllText(logFilePath, JsonSerializer.Serialize(_entries, options));
        }
        public static List<LogEntry> LoadLogs() 
        {
            if(!File.Exists(logFilePath)) return new List<LogEntry>();

            var json = File.ReadAllText(logFilePath);
            return JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new List<LogEntry>();
        }

        public static void LogItemChanges(Item oldItem, Item newItem, User user)
        {
            List<string> changes = new();
            if (oldItem.Name != newItem.Name) changes.Add($"Name: {oldItem.Name} -> {newItem.Name}");
            if (oldItem.MasteryPoints != newItem.MasteryPoints) changes.Add($"Mastery Points: {oldItem.MasteryPoints} -> {newItem.MasteryPoints}");
            if (oldItem.Category != newItem.Category) changes.Add($"Category: { oldItem.CategoryName} -> { newItem.CategoryName}");
            if (changes.Count > 0)
            {
                Log("Edited Item", $"{user.Name} edited item: {newItem.Name} | Changes: {string.Join(",", changes)}");
            }
        }

        public static void LogNodeChanges(Node oldNode, Node newNode, User user)
        {
            List<string> changes = new();
            if (oldNode.Name != newNode.Name) changes.Add($"Name: {oldNode.Name} -> {newNode.Name}");
            if (oldNode.MasteryPoints != newNode.MasteryPoints) changes.Add($"Mastery Points: {oldNode.MasteryPoints} -> {newNode.MasteryPoints}");
            if (oldNode.Planet != newNode.Planet) changes.Add($"Planet: {oldNode.Planet} -> {newNode.Planet}");
            if (changes.Count > 0)
            {
                Log("Edited Node", $"{user.Name} edited node: {newNode.Name} | Changes: {string.Join(",", changes)}");
            }
        }
    }
}
