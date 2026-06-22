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
        private const string LogMutexName = "WfTracker_Log_Mutex";
        static LoggerService()
        {
            try
            {
                var existing = LoadLogs();
                lock (_lock)
                {
                    if (existing?.Count > 0)
                        _entries.AddRange(existing);
                }
            }
            catch
            {
            }
        }

        public static void Log(string action, string? details = null)
        {
            Task.Run(() =>
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
                }

                SaveToFile();
                
            });
        }
        public static bool SaveToFile()
        {
            using var mutex = new System.Threading.Mutex(false, LogMutexName);
            bool acquired = false;
            try
            {
                try
                {
                    acquired = mutex.WaitOne(5000);
                }catch(AbandonedMutexException)
                {
                    acquired = true;
                }

                if(!acquired)
                    return false;

                lock (_lock)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(_entries, options);
                    var tempPath = logFilePath + ".tmp";
                    File.WriteAllText(tempPath, json, Encoding.UTF8);
                    File.Copy(tempPath, logFilePath, true);
                    File.Delete(tempPath);
                    return true;
                }
            }
            finally
            {
                if (acquired) try { mutex.ReleaseMutex();  } catch { }
            }
        }
        
        public static List<LogEntry> LoadLogs() 
        {
            using var mutex = new System.Threading.Mutex(false, LogMutexName);
            bool acquired = false;
            try
            {
                try
                {
                    acquired = mutex.WaitOne(5000);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }
                if (!acquired)
                    return new List<LogEntry>();

                if (!File.Exists(logFilePath))
                    return new List<LogEntry>();

                var json = File.ReadAllText(logFilePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new List<LogEntry>();
            }
            finally
            {
                if (acquired) try { mutex.ReleaseMutex(); } catch { }
            }

        }

        public static bool EditLog(Guid id, string adminNote)
        {
            using var mutex = new System.Threading.Mutex(false, LogMutexName);
            bool acquired = false;
            try
            {
                try
                {
                    acquired = mutex.WaitOne(5000);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                if (!acquired) return false;
                lock (_lock)
                {
                    var logs = LoadLogs();
                    var idx = logs.FindIndex(l => l.Id == id);
                    if (idx < 0) return false;

                    logs[idx].AdminNote = adminNote;
                    logs[idx].LastModified = DateTime.Now;

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(logFilePath, JsonSerializer.Serialize(logs, options));

                    _entries.Clear();
                    _entries.AddRange(logs);
                    return true;
                    
                }
            }
            finally
            {
                if (acquired) try { mutex.ReleaseMutex(); } catch { }

            }
        }

        public static bool DeleteLog(Guid id)
        {
            using var mutex = new System.Threading.Mutex(false, LogMutexName);
            bool acquired = false;
            try
            {
                try
                {
                    acquired = mutex.WaitOne(5000);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }
                if (!acquired) return false;
                lock (_lock)
                {
                    var logs = LoadLogs();
                    var idx = logs.FindIndex(l => l.Id == id);

                    if (idx < 0) return false;

                    logs.RemoveAt(idx);

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(logFilePath, JsonSerializer.Serialize(logs, options));

                    _entries.Clear();
                    _entries.AddRange(logs);
                    return true;
                    
                }
            }
            finally
            {
                if (acquired) try { mutex.ReleaseMutex(); } catch { }
            }
        }
        public static bool DeleteAllLogs()
        {
            using var mutex = new System.Threading.Mutex(false, LogMutexName);
            bool acquired = false;
            try
            {
                try
                {
                    acquired = mutex.WaitOne(5000);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }
                if (!acquired) return false;
                lock (_lock)
                {
                    _entries.Clear();
                    File.WriteAllText(logFilePath, JsonSerializer.Serialize(new List<LogEntry>(), new JsonSerializerOptions { WriteIndented = true }));
                    return true;
                }
            }
            finally
            {
                if (acquired) try { mutex.ReleaseMutex(); } catch { }
            }
            
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
