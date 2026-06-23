using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static readonly string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs.json");
        private static readonly object _lock = new();
        private static readonly List<LogEntry> _entries = new();

        static LoggerService()
        {
            try
            {
                var existing = ReadFromDisk();
                if (existing.Count > 0)
                    _entries.AddRange(existing);
            }
            catch { }
        }

        public static void Log(string action, string? details = null)
        {
            var entry = new LogEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.Now,
                Action = action,
                Details = details
            };

            lock (_lock)
            {
                _entries.Add(entry);
                WriteToDisk(_entries);
            }
        }

        public static List<LogEntry> LoadLogs()
        {
            lock (_lock) return _entries.ToList();
        }

        public static bool EditLog(Guid id, string adminNote)
        {
            lock (_lock)
            {
                var entry = _entries.FirstOrDefault(e => e.Id == id);
                if (entry is null) return false;

                entry.AdminNote = adminNote;
                entry.LastModified = DateTime.Now;
                WriteToDisk(_entries);
                return true;
            }
        }

        public static bool DeleteLog(Guid id)
        {
            lock (_lock)
            {
                var removed = _entries.RemoveAll(e => e.Id == id) > 0;
                if (removed) WriteToDisk(_entries);
                return removed;
            }
        }

        public static bool DeleteAllLogs()
        {
            lock (_lock)
            {
                _entries.Clear();
                WriteToDisk(_entries);
                return true;
            }
        }

        public static void LogItemChanges(Item oldItem, Item newItem, User user)
        {
            var changes = new List<string>();
            if (oldItem.Name != newItem.Name)
                changes.Add($"Name: {oldItem.Name} -> {newItem.Name}");
            if (oldItem.MasteryPoints != newItem.MasteryPoints)
                changes.Add($"Mastery Points: {oldItem.MasteryPoints} -> {newItem.MasteryPoints}");
            if (oldItem.Category != newItem.Category)
                changes.Add($"Category: {oldItem.CategoryName} -> {newItem.CategoryName}");

            if (changes.Count > 0)
                Log("Edited Item", $"{user.Name} edited item: {newItem.Name} | Changes: {string.Join(", ", changes)}");
        }

        public static void LogNodeChanges(Node oldNode, Node newNode, User user)
        {
            var changes = new List<string>();
            if (oldNode.Name != newNode.Name)
                changes.Add($"Name: {oldNode.Name} -> {newNode.Name}");
            if (oldNode.MasteryPoints != newNode.MasteryPoints)
                changes.Add($"Mastery Points: {oldNode.MasteryPoints} -> {newNode.MasteryPoints}");
            if (oldNode.Planet != newNode.Planet)
                changes.Add($"Planet: {oldNode.Planet} -> {newNode.Planet}");

            if (changes.Count > 0)
                Log("Edited Node", $"{user.Name} edited node: {newNode.Name} | Changes: {string.Join(", ", changes)}");
        }

        private static void WriteToDisk(List<LogEntry> entries)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(entries, options);
            var tempPath = _logFilePath + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Copy(tempPath, _logFilePath, true);
            File.Delete(tempPath);
        }

        private static List<LogEntry> ReadFromDisk()
        {
            if (!File.Exists(_logFilePath)) return new();
            var json = File.ReadAllText(_logFilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new();
        }
    }
}
