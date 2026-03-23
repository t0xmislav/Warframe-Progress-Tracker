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
        private static readonly Dictionary<(int userId, int itemId), Model.ItemProgress> _itemProgressCache = new Dictionary<(int, int), Model.ItemProgress>();
        private static readonly Dictionary<(int userId, int nodeId), Model.NodeProgress> _nodeProgressCache = new Dictionary<(int, int), Model.NodeProgress>();
        private static readonly ReaderWriterLockSlim _lock = new();

        public static void PreloadUserProgress(User user)
        {
            Debug.WriteLine("Preloading...");
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
        public static void StoreItemProgress(int userId, int itemId, Model.ItemProgress itemProgress) {
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

        public static void StoreNodeProgress(int userId, int nodeId, Model.NodeProgress nodeProgress) {
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
