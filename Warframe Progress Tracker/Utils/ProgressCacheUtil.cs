using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Warframe_Progress_Tracker.Model;
using Warframe_Progress_Tracker.Services;

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


            var itemProgressList = await Task.Run(() => DbService.GetItemProgressForUser(user), cancellationToken).ConfigureAwait(false);
            var itemSnapshot = new Dictionary<int, ItemProgress>();
            foreach (var item in itemProgressList)
            {
                itemSnapshot[item.Item.Id] = item;
            }

            var nodeProgresses = await Task.Run(() => DbService.GetNodeProgressForUser(user), cancellationToken).ConfigureAwait(false);
            var nodeSnapshot = new Dictionary<int, NodeProgress>();
            foreach (var node in nodeProgresses)
            {
                nodeSnapshot[node.Node.Id] = node;
            }

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
