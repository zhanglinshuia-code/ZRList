// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace ZRList
{
    public sealed partial class ExpandableListController<TGroup, TItem, TKey>
    {
        private struct GroupEntry
        {
            public TKey Key;
            public TGroup Data;
            public int FirstItem;
            public int ItemCount;
            public int SourceIndex;
            public bool Expanded;
        }

        private struct ItemEntry
        {
            public TKey Key;
            public TKey GroupKey;
            public TItem Data;
            public int SourceIndex;
        }

        private sealed class Snapshot
        {
            public readonly List<GroupEntry> Groups = new List<GroupEntry>();
            public readonly List<ItemEntry> Items = new List<ItemEntry>();
            public readonly Dictionary<TKey, int> GroupIndices = new Dictionary<TKey, int>();
            public readonly Dictionary<TKey, int> ItemIndices = new Dictionary<TKey, int>();

            public void Clear()
            {
                Groups.Clear();
                Items.Clear();
                GroupIndices.Clear();
                ItemIndices.Clear();
            }
        }

        private readonly struct RowKey: IEquatable<RowKey>
        {
            private readonly bool m_header;
            private readonly TKey m_key;

            public RowKey(bool header, TKey key)
            {
                m_header = header;
                m_key = key;
            }

            public bool Equals(RowKey other)
            {
                return m_header == other.m_header && EqualityComparer<TKey>.Default.Equals(m_key, other.m_key);
            }

            public override bool Equals(object obj)
            {
                return obj is RowKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return unchecked((EqualityComparer<TKey>.Default.GetHashCode(m_key) * 397) ^ (m_header ? 1 : 0));
            }
        }

        private int CompareGroups(GroupEntry left, GroupEntry right)
        {
            int result = GroupComparison(left.Data, right.Data);
            return result != 0 ? result : left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private int CompareItems(ItemEntry left, ItemEntry right)
        {
            int result = ItemComparison(m_itemSortGroup, left.Data, right.Data);
            return result != 0 ? result : left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private void ReadSnapshot(IReadOnlyList<TGroup> groups)
        {
            for (int i = 0; i < groups.Count; ++i) {
                TGroup group = groups[i];
                TKey groupKey = m_getGroupKey(group);
                if (m_nextData.GroupIndices.ContainsKey(groupKey)) {
                    throw new ArgumentException("Group keys must be unique.", nameof(groups));
                }
                m_nextData.GroupIndices.Add(groupKey, i);
                IReadOnlyList<TItem> items = m_getItems(group);
                if (items == null) {
                    throw new InvalidOperationException("GetItems must return a list; use an empty list for empty groups.");
                }
                var entry = new GroupEntry
                {
                    Key = groupKey,
                    Data = group,
                    SourceIndex = i,
                    FirstItem = m_nextData.Items.Count,
                    ItemCount = items.Count,
                    Expanded = m_data.GroupIndices.TryGetValue(groupKey, out int previous) ? m_data.Groups[previous].Expanded : DefaultExpanded
                };
                bool sortItems = ItemComparison != null && items.Count > 1;
                List<ItemEntry> destination = sortItems ? m_itemSortBuffer : m_nextData.Items;
                for (int child = 0; child < items.Count; ++child) {
                    TItem item = items[child];
                    TKey itemKey = m_getItemKey(item);
                    if (m_nextData.ItemIndices.ContainsKey(itemKey)) {
                        throw new ArgumentException("Item keys must be unique across all groups, including collapsed items.", nameof(groups));
                    }
                    m_nextData.ItemIndices.Add(itemKey, -1);
                    destination.Add(new ItemEntry { Key = itemKey, GroupKey = groupKey, Data = item, SourceIndex = child });
                }
                if (sortItems) {
                    m_itemSortGroup = group;
                    // Reuse one buffer and cached Comparison to avoid Unity's allocating range overload.
                    m_itemSortBuffer.Sort(m_compareItems);
                    m_nextData.Items.AddRange(m_itemSortBuffer);
                    m_itemSortBuffer.Clear();
                }
                m_nextData.Groups.Add(entry);
            }
            if (GroupComparison != null && m_nextData.Groups.Count > 1) {
                m_nextData.Groups.Sort(m_compareGroups);
            }
            for (int i = 0; i < m_nextData.Groups.Count; ++i) {
                m_nextData.GroupIndices[m_nextData.Groups[i].Key] = i;
            }
            for (int i = 0; i < m_nextData.Items.Count; ++i) {
                m_nextData.ItemIndices[m_nextData.Items[i].Key] = i;
            }
        }

        private void BuildRows(Snapshot data)
        {
            for (int groupIndex = 0; groupIndex < data.Groups.Count; ++groupIndex) {
                GroupEntry group = data.Groups[groupIndex];
                bool expanded = m_expandAll || m_expansionGroup == groupIndex ? m_expansionValue : group.Expanded;
                AddRow(new ExpandableListRow<TGroup, TItem, TKey>(group.Key, group.Data, expanded));
                if (!expanded) {
                    continue;
                }
                for (int i = group.FirstItem; i < group.FirstItem + group.ItemCount; ++i) {
                    ItemEntry item = data.Items[i];
                    AddRow(new ExpandableListRow<TGroup, TItem, TKey>(group.Key, group.Data, item.Key, item.Data));
                }
            }
        }

        private void AddRow(ExpandableListRow<TGroup, TItem, TKey> row)
        {
            var key = new RowKey(row.IsHeader, row.IsHeader ? row.GroupKey : row.ItemKey);
            int previous = m_rowIndices.TryGetValue(key, out int index) ? index : -1;
            bool preserveSize = !m_replaceData && previous >= 0 && m_rows[previous].IsExpanded == row.IsExpanded;
            m_nextRowIndices.Add(key, m_nextRows.Count);
            m_nextRows.Add(row);
            m_updates.Add(new ScrollItemUpdate(previous, preserveSize));
        }

        private void ApplyPrepared(ScrollUpdatePosition position, int preferredIndex = -1)
        {
            ValidateRenderingConfiguration();
            if (position < ScrollUpdatePosition.KeepVisibleItem || position > ScrollUpdatePosition.Start) {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            if (!m_view.IsInitialized) {
                CommitPrepared();
                m_view.Initialize(this, m_rows.Count);
                return;
            }
            int oldAnchor = m_view.GetUpdateAnchorIndex(preferredIndex);
            int? fallback = null;
            if (oldAnchor >= 0 && m_nextRowIndices.TryGetValue(new RowKey(true, m_rows[oldAnchor].GroupKey), out int header)) {
                fallback = header;
            }
            var options = new ScrollDataUpdateOptions(position, preferredIndex >= 0 ? preferredIndex : (int?)null, fallback);
            m_view.ApplyData(m_updates, m_commit, options);
        }

        private void CommitPrepared()
        {
            if (m_replaceData) {
                Snapshot old = m_data;
                m_data = m_nextData;
                m_nextData = old;
            }
            else if (m_expandAll || m_expansionGroup >= 0) {
                int first = m_expandAll ? 0 : m_expansionGroup;
                int end = m_expandAll ? m_data.Groups.Count : first + 1;
                for (int i = first; i < end; ++i) {
                    GroupEntry group = m_data.Groups[i];
                    group.Expanded = m_expansionValue;
                    m_data.Groups[i] = group;
                }
            }
            List<ExpandableListRow<TGroup, TItem, TKey>> oldRows = m_rows;
            m_rows = m_nextRows;
            m_nextRows = oldRows;
            Dictionary<RowKey, int> oldIndices = m_rowIndices;
            m_rowIndices = m_nextRowIndices;
            m_nextRowIndices = oldIndices;
        }
    }
}
