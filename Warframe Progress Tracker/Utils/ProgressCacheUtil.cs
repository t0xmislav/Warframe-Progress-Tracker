using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;
using Warframe_Progress_Tracker.Utils.Logger;

namespace Warframe_Progress_Tracker.Utils
{
    public static class ProgressCacheUtil
    {
        private static readonly Dictionary<(int userId, int itemId), ItemProgress> _itemProgressCache = new Dictionary<(int, int), Model.ItemProgress>();
        private static readonly Dictionary<(int userId, int nodeId), NodeProgress> _nodeProgressCache = new Dictionary<(int, int), Model.NodeProgress>();
        private static readonly ReaderWriterLockSlim _lock = new();

        private static CancellationTokenSource? _autoRefreshCts;
        private static readonly object _autoRefreshLock = new();

        public static void StartAutoRefresh(User user, TimeSpan interval)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            lock (_autoRefreshLock)
            {
                StopAutoRefresh();
                _autoRefreshCts = new CancellationTokenSource();
                var token = _autoRefreshCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                await LoadUserProgressAsync(user, token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) { break; }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error refreshing progress cache: {ex}");
                            }

                            try
                            {
                                await Task.Delay(interval, token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) { break; }
                        }
                    }
                    finally
                    {
                        lock (_autoRefreshLock)
                        {
                            _autoRefreshCts?.Dispose();
                            _autoRefreshCts = null;
                        }
                    }
                }, token);
            }
        }
        public static void StopAutoRefresh()
        {
            lock (_autoRefreshLock)
            {
                _autoRefreshCts?.Cancel();
                _autoRefreshCts?.Dispose();
                _autoRefreshCts= null;
            }
        }
        public static async Task LoadUserProgressAsync(User user, CancellationToken cancellationToken = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            /*var sw = Stopwatch.StartNew();
            var itemsTask = await Task.Run(() => DbService.GetItemProgressForUser(user), cancellationToken);
            var nodesTask = await Task.Run(() => DbService.GetNodeProgressForUser(user), cancellationToken);
            sw.Stop();

            LoggerService.Log("Perf", $"Sequential LoadUserProgressAsync: {sw.ElapsedMilliseconds}ms");
            */

            
            var sw = Stopwatch.StartNew();

            var itemsTask = Task.Run(() => DbService.GetItemProgressForUser(user), cancellationToken);
            var nodesTask = Task.Run(() => DbService.GetNodeProgressForUser(user), cancellationToken);
            await Task.WhenAll(itemsTask, nodesTask);
            sw.Stop();
            //LoggerService.Log("Perf", $"Parallel LoadUserProgressAsync: {sw.ElapsedMilliseconds}ms");
            
            var itemSnapshot = itemsTask.Result.ToDictionary(i => i.Item.Id);
            var nodeSnapshot = nodesTask.Result.ToDictionary(n => n.Node.Id);

            _lock.EnterWriteLock();
            try
            {
                var itemKeysToRemove = _itemProgressCache.Keys.Where(k => k.userId == user.Id).ToList();
                foreach (var k in itemKeysToRemove) _itemProgressCache.Remove(k);
                foreach (var kv in itemSnapshot)
                {
                    _itemProgressCache[(user.Id, kv.Key)] = kv.Value;
                }

                var nodeKeysToRemove = _nodeProgressCache.Keys.Where(k => k.userId == user.Id).ToList();
                foreach (var k in nodeKeysToRemove) _nodeProgressCache.Remove(k);
                foreach (var kv in nodeSnapshot)
                {
                    _nodeProgressCache[(user.Id, kv.Key)] = kv.Value;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public static void StoreItemProgress(int userId, int itemId, ItemProgress itemProgress) {
            if (itemProgress == null) return;
            _lock.EnterWriteLock();
            try
            {
                _itemProgressCache[(userId, itemId)] = itemProgress;
            }
            finally 
            {
                _lock.ExitWriteLock(); 
            }
        }

        public static ItemProgress? GetItemProgress(int userId, int itemId) 
        {
            _lock.EnterReadLock();
            try
            {
                return _itemProgressCache.TryGetValue((userId, itemId), out var itemProgress) ? itemProgress : null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public static void StoreNodeProgress(int userId, int nodeId, NodeProgress nodeProgress) {
            if (nodeProgress == null) return;
            _lock.EnterWriteLock();
            try
            {
                _nodeProgressCache[(userId, nodeId)] = nodeProgress;
            }
            finally 
            { 
                _lock.ExitWriteLock(); 
            }
        }

        public static NodeProgress? GetNodeProgress(int userId, int nodeId) 
        {
            _lock.EnterReadLock();
            try
            {
                return _nodeProgressCache.TryGetValue((userId, nodeId), out var nodeProgress) ? nodeProgress : null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
    }
}
