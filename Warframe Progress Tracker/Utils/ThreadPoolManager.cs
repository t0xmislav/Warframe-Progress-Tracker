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
        private static readonly SemaphoreSlim _readSemaphore = new(5);
        private static readonly SemaphoreSlim _writeSemaphore = new(1);
        private static readonly ConcurrentQueue<Func<Task>> _readQueue = new();
        private static readonly ConcurrentQueue<Func<Task>> _writeQueue = new();
        private static bool _isRunning = false;
        private static readonly object _processLock = new();

        public static void QueueDatabaseRead(Func<Task> task)
        {
            _readQueue.Enqueue(() => RunTaskAsync(task, _readSemaphore));
            StartProcessingQueue();
        }
        public static void QueueDatabaseWrite(Func<Task> task)
        {
            _writeQueue.Enqueue(() => RunTaskAsync(task, _writeSemaphore));
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
            try
            {
                while (_writeQueue.Count > 0 || _readQueue.Count > 0)
                {
                    if (_writeQueue.TryDequeue(out var writeTask))
                    {
                        await writeTask();
                        continue;
                    }
                    if (_readQueue.TryDequeue(out var readTask))
                    {
                        await readTask();
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThreadPoolManager] Error: {ex}");
            }
            finally
            {
                lock (_processLock)
                {
                    _isRunning = false;
                    if (_readQueue.Count > 0 || _writeQueue.Count > 0)
                        StartProcessingQueue();
                }
            }
        }
        private static async Task RunTaskAsync(Func<Task> task, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                await task();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThreadPoolManager] Task failed: {ex}");
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
