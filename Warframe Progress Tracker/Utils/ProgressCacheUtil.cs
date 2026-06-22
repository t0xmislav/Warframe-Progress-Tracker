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
                            if (_autoRefreshCts?.IsCancellationRequested == true) _autoRefreshCts = null;
                        }
                    }
                }, token);
            }
        }
        public static void StopAutoRefresh()
        {
            lock (_autoRefreshLock)
            {
                try
                {
                    _autoRefreshCts?.Cancel();
                    _autoRefreshCts?.Dispose();
                }
                catch { }
                finally
                {
                    _autoRefreshCts = null;
                }
            }
        }
        public static async Task LoadUserProgressAsync(User user, CancellationToken cancellationToken = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var items = await Task.Run(() => DbService.GetAllItems(), cancellationToken).ConfigureAwait(false);
            var nodes = await Task.Run(() => DbService.GetAllNodes(), cancellationToken).ConfigureAwait(false);

            var itemSnapshot = new Dictionary<int, ItemProgress>();
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var progress = await Task.Run(() => DbService.GetProgressForItem(user, item), cancellationToken).ConfigureAwait(false);
                if (progress != null) itemSnapshot[item.Id] = progress;
            }

            var nodeSnapshot = new Dictionary<int, NodeProgress>();
            foreach (var node in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var progress = await Task.Run(() => DbService.GetProgressForNode(user, node), cancellationToken).ConfigureAwait(false);
                if (progress != null) nodeSnapshot[node.Id] = progress;
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
        public static void LoadUserProgress(User user)
        {
            Debug.WriteLine("Caching user progress...");
            var items = DbService.GetAllItems();
            foreach (var item in items)
            {
                var progress = DbService.GetProgressForItem(user, item);
                if(progress is not null) StoreItemProgress(user.Id, item.Id, progress);
            }
            var nodes = DbService.GetAllNodes();
            foreach(var node in nodes)
            {
                var progress = DbService.GetProgressForNode(user, node);
                if (progress is not null) StoreNodeProgress(user.Id, node.Id, progress);
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
        public static Dictionary<int, ItemProgress> GetItemProgressSnapshot(int userId)
        {
            _lock.EnterReadLock();
            try
            {
                // Copy only entries for this user into a fresh dictionary (itemId -> ItemProgress)
                return _itemProgressCache
                    .Where(kvp => kvp.Key.Item1 == userId)
                    .ToDictionary(kvp => kvp.Key.Item2, kvp => kvp.Value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public static Dictionary<int, NodeProgress> GetNodeProgressSnapshot(int userId)
        {
            _lock.EnterReadLock();
            try
            {
                // Copy only entries for this user into a fresh dictionary (nodeId -> NodeProgress)
                return _nodeProgressCache
                    .Where(kvp => kvp.Key.Item1 == userId)
                    .ToDictionary(kvp => kvp.Key.Item2, kvp => kvp.Value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}
