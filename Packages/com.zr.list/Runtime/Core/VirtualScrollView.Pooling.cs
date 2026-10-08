// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    public partial class VirtualScrollView
    {
        private readonly Dictionary<RectTransform, List<ScrollItemView>> m_poolsByPrefab = new Dictionary<RectTransform, List<ScrollItemView>>();
        private readonly List<RectTransform> m_emptyPrefabPools = new List<RectTransform>();

        private void PruneEmptyPrefabPools()
        {
            // Keep empty buckets during scrolling to avoid allocating them on the
            // next recycle. Explicit trimming releases obsolete template references.
            if (RecycledItemViews.Count == 0) {
                m_poolsByPrefab.Clear();
                return;
            }
            foreach (var pair in m_poolsByPrefab) {
                if (pair.Value.Count == 0) {
                    m_emptyPrefabPools.Add(pair.Key);
                }
            }
            foreach (RectTransform prefab in m_emptyPrefabPools) {
                m_poolsByPrefab.Remove(prefab);
            }
            m_emptyPrefabPools.Clear();
        }

        private void AddPooledHolder(ScrollItemView holder)
        {
            if (!m_poolsByPrefab.TryGetValue(holder.SourcePrefab, out List<ScrollItemView> pool)) {
                pool = new List<ScrollItemView>();
                m_poolsByPrefab.Add(holder.SourcePrefab, pool);
            }

            holder.PoolIndex = RecycledItemViews.Count;
            holder.PrefabPoolIndex = pool.Count;
            RecycledItemViews.Add(holder);
            pool.Add(holder);
        }

        // Both collections are unordered. Swap removal keeps acquisition and
        // explicit trimming constant-time without allocating per-view pool nodes.
        private ScrollItemView RemovePooledHolder(int index)
        {
            ScrollItemView holder = RecycledItemViews[index];
            List<ScrollItemView> pool = m_poolsByPrefab[holder.SourcePrefab];
            int last = pool.Count - 1;
            ScrollItemView moved = pool[last];
            pool[holder.PrefabPoolIndex] = moved;
            moved.PrefabPoolIndex = holder.PrefabPoolIndex;
            pool.RemoveAt(last);

            last = RecycledItemViews.Count - 1;
            moved = RecycledItemViews[last];
            RecycledItemViews[index] = moved;
            moved.PoolIndex = index;
            RecycledItemViews.RemoveAt(last);
            holder.PoolIndex = holder.PrefabPoolIndex = -1;
            return holder;
        }

        private ScrollItemView ExtractRecyclableViewsHolderOrCreateNew(RectTransform prefab)
        {
            if (m_poolsByPrefab.TryGetValue(prefab, out List<ScrollItemView> pool)) {
                while (pool.Count > 0) {
                    ScrollItemView holder = RemovePooledHolder(pool[pool.Count - 1].PoolIndex);
                    // A scene object can have been destroyed externally while pooled.
                    if (holder.Root != null) {
                        return holder;
                    }
                }
            }

            return CreateViewsHolder(prefab);
        }
    }
}
