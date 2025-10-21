using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Utils
{
    public static class ProgressCacheUtil
    {
        private static readonly Dictionary<(int userId, int itemId), Model.ItemProgress> _itemProgressCache = new Dictionary<(int, int), Model.ItemProgress>();
        private static readonly Dictionary<(int userId, int nodeId), Model.NodeProgress> _nodeProgressCache = new Dictionary<(int, int), Model.NodeProgress?>();
        private static readonly object _lock = new();
        public static void StoreItemProgress(int userId, int itemId, Model.ItemProgress itemProgress) {
            if (itemProgress == null) return;
            lock (_lock)
            {
                _itemProgressCache[(userId, itemId)] = itemProgress;
            }
        }

        public static Model.ItemProgress? GetItemProgress(int userId, int itemId) => _itemProgressCache.TryGetValue((userId,  itemId), out var itemProgress) ? itemProgress : null;

        public static void StoreNodeProgress(int userId, int nodeId, Model.NodeProgress nodeProgress) {
            if (nodeProgress == null) return;
            lock (_lock)
            {
                _nodeProgressCache[(userId, nodeId)] = nodeProgress;
            }
        }

        public static Model.NodeProgress? GetNodeProgress(int userId, int nodeId) => _nodeProgressCache.TryGetValue((userId, nodeId), out var nodeProgress) ? nodeProgress : null;
    }
}
