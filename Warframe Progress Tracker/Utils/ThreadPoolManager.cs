using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Utils
{
    public static class ThreadPoolManager
    {
        private static readonly SemaphoreSlim _dbSemaphore = new(3);
        private static readonly ConcurrentQueue<Func<Task>> _taskQueue = new();
        private static bool _isRunning = false;
        private static readonly object _processLock = new();

        public static void QueueDatabaseTask(Func<Task> task)
        {
            _taskQueue.Enqueue(task);
            StartProcessingQueue();
        }

        private static void StartProcessingQueue()
        {
            lock (_processLock)
            {
                if (_isRunning) return;

                _isRunning = true;
                _ = Task.Run(ProcessQueueAsync);
            }
        }
        private static async Task ProcessQueueAsync()
        {
            while(_taskQueue.TryDequeue(out var task))
            {
                await _dbSemaphore.WaitAsync();
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[ThreadPoolManager] Running {task.GetType}.");
                    await task();
                }
                catch (Exception ex) 
                {
                    System.Diagnostics.Debug.WriteLine($"[ThreadPoolManager] Error. {ex}");
                }
                finally
                {
                    _dbSemaphore.Release();
                }

            }
            lock(_processLock) 
            {
                _isRunning = false;
            }
        }
    }
}
