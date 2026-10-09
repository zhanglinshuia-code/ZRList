// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace ZRList
{
    /// <summary>
    /// One-level grouped projection over an exclusively owned VirtualScrollView.
    /// Submit on the main thread after business changes. Sorting happens only at submission;
    /// toggling uses the submitted snapshot. Reentrant mutations from callbacks are rejected.
    /// Reuses two packed snapshots and row buffers; no per-group containers or per-bind delegates.
    /// </summary>
    public sealed partial class ExpandableListController<TGroup, TItem, TKey>: IDisposable
    {
        private readonly VirtualScrollView m_view;
        private ExpandableListAdapter<TGroup, TItem, TKey> m_adapter;
        private Func<TGroup, TKey> m_getGroupKey;
        private Func<TGroup, IReadOnlyList<TItem>> m_getItems;
        private Func<TItem, TKey> m_getItemKey;
        private readonly Action m_commit;
        private readonly Comparison<GroupEntry> m_compareGroups;
        private readonly Comparison<ItemEntry> m_compareItems;
        private readonly List<ItemEntry> m_itemSortBuffer = new List<ItemEntry>();
        private TGroup m_itemSortGroup;
        private Snapshot m_data = new Snapshot();
        private Snapshot m_nextData = new Snapshot();
        private List<ExpandableListRow<TGroup, TItem, TKey>> m_rows = new List<ExpandableListRow<TGroup, TItem, TKey>>();
        private List<ExpandableListRow<TGroup, TItem, TKey>> m_nextRows = new List<ExpandableListRow<TGroup, TItem, TKey>>();
        private Dictionary<RowKey, int> m_rowIndices = new Dictionary<RowKey, int>();
        private Dictionary<RowKey, int> m_nextRowIndices = new Dictionary<RowKey, int>();
        private readonly List<ScrollItemUpdate> m_updates = new List<ScrollItemUpdate>();
        private bool m_busy;
        private bool m_disposed;
        private bool m_replaceData;
        private int m_expansionGroup = -1;
        private bool m_expansionValue;
        private bool m_expandAll;

        /// <summary>Used for newly introduced groups. Existing groups keep their submitted expansion state.</summary>
        public bool DefaultExpanded { get; set; }
        /// <summary>Optional. Null preserves input group order. Read only during Submit.</summary>
        public Comparison<TGroup> GroupComparison { get; set; }
        /// <summary>Optional per-group item comparison. Null preserves input child order.</summary>
        public Func<TGroup, TItem, TItem, int> ItemComparison { get; set; }
        /// <summary>Raised once after an explicit expansion operation; read state here, mutate outside callbacks.</summary>
        public event Action ExpansionChanged;

        public int GroupCount { get { return m_data.Groups.Count; } }
        public int RowCount { get { return m_rows.Count; } }

        /// <summary>Callback-based entry point. Accessors return stable keys and existing item collections.</summary>
        public ExpandableListController(VirtualScrollView view, Func<TGroup, TKey> getGroupKey,
            Func<TGroup, IReadOnlyList<TItem>> getItems, Func<TItem, TKey> getItemKey): this(view)
        {
            m_getGroupKey = getGroupKey ?? throw new ArgumentNullException(nameof(getGroupKey));
            m_getItems = getItems ?? throw new ArgumentNullException(nameof(getItems));
            m_getItemKey = getItemKey ?? throw new ArgumentNullException(nameof(getItemKey));
        }

        /// <summary>通过一个 Adapter 接入整套绑定逻辑；不要同时配置控制器的显示回调。</summary>
        public ExpandableListController(VirtualScrollView view, ExpandableListAdapter<TGroup, TItem, TKey> adapter): this(view)
        {
            m_adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            // 只在构造时缓存访问委托，提交与滚动期间不创建委托。
            m_getGroupKey = adapter.GetGroupKey;
            m_getItems = adapter.GetItems;
            m_getItemKey = adapter.GetItemKey;
        }

        private ExpandableListController(VirtualScrollView view)
        {
            if (view == null) {
                throw new ArgumentNullException(nameof(view));
            }
            if (view.IsInitialized || view.FirstDataIndex != 0) {
                throw new InvalidOperationException("Attach to an uninitialized list with FirstDataIndex = 0.");
            }
            m_view = view;
            m_commit = CommitPrepared;
            m_compareGroups = CompareGroups;
            m_compareItems = CompareItems;
        }

        /// <summary>
        /// Reads a new structural snapshot, validates all keys (including collapsed items),
        /// then updates content and order in one transaction. Visible rows are rebound and
        /// unverified sizes reset to estimates. Payload objects remain owned by the caller.
        /// Validation/accessor/comparison errors leave the current snapshot intact.
        /// </summary>
        public void Submit(IReadOnlyList<TGroup> groups, ScrollUpdatePosition position = ScrollUpdatePosition.KeepVisibleItem)
        {
            if (groups == null) {
                throw new ArgumentNullException(nameof(groups));
            }
            EnterMutation();
            try {
                ReadSnapshot(groups);
                m_replaceData = true;
                BuildRows(m_nextData);
                ApplyPrepared(position);
            }
            finally {
                FinishMutation();
            }
        }

        public bool IsExpanded(TKey groupKey)
        {
            return m_data.GroupIndices.TryGetValue(groupKey, out int index) && m_data.Groups[index].Expanded;
        }

        public bool Toggle(TKey groupKey)
        {
            return SetExpanded(groupKey, !IsExpanded(groupKey));
        }

        public bool SetExpanded(TKey groupKey, bool expanded)
        {
            EnterMutation();
            try {
                if (!m_data.GroupIndices.TryGetValue(groupKey, out int index) || m_data.Groups[index].Expanded == expanded) {
                    return false;
                }
                m_expansionGroup = index;
                m_expansionValue = expanded;
                BuildRows(m_data);
                m_rowIndices.TryGetValue(new RowKey(true, groupKey), out int headerIndex);
                ApplyPrepared(ScrollUpdatePosition.KeepVisibleItem, headerIndex);
                ExpansionChanged?.Invoke();
                return true;
            }
            finally {
                FinishMutation();
            }
        }

        /// <summary>Changes all groups in one projection/layout transaction and raises one event.</summary>
        public void SetAllExpanded(bool expanded)
        {
            EnterMutation();
            try {
                bool changed = false;
                for (int i = 0; i < m_data.Groups.Count; ++i) {
                    changed |= m_data.Groups[i].Expanded != expanded;
                }
                if (!changed) {
                    return;
                }
                m_expandAll = true;
                m_expansionValue = expanded;
                BuildRows(m_data);
                ApplyPrepared(ScrollUpdatePosition.KeepVisibleItem);
                ExpansionChanged?.Invoke();
            }
            finally {
                FinishMutation();
            }
        }

        public bool TryGetRow(int rowIndex, out ExpandableListRow<TGroup, TItem, TKey> row)
        {
            if (rowIndex >= 0 && rowIndex < m_rows.Count) {
                row = m_rows[rowIndex];
                return true;
            }
            row = default;
            return false;
        }

        public bool TryGetHeaderIndex(TKey groupKey, out int rowIndex)
        {
            return m_rowIndices.TryGetValue(new RowKey(true, groupKey), out rowIndex);
        }

        public bool TryGetItemIndex(TKey itemKey, out int rowIndex)
        {
            return m_rowIndices.TryGetValue(new RowKey(false, itemKey), out rowIndex);
        }

        public bool ScrollToGroup(TKey groupKey, float duration = 0f)
        {
            CheckAvailable();
            return TryGetHeaderIndex(groupKey, out int index) && m_view.JumpToDataItem(index, duration);
        }

        public bool ScrollToItem(TKey itemKey, bool expandIfNeeded = true, float duration = 0f)
        {
            CheckAvailable();
            if (float.IsNaN(duration) || float.IsInfinity(duration) || !m_data.ItemIndices.TryGetValue(itemKey, out int itemIndex)) {
                return false;
            }
            TKey groupKey = m_data.Items[itemIndex].GroupKey;
            if (!IsExpanded(groupKey)) {
                if (!expandIfNeeded) {
                    return false;
                }
                SetExpanded(groupKey, true);
            }
            return TryGetItemIndex(itemKey, out int rowIndex) && m_view.JumpToDataItem(rowIndex, duration);
        }

        public void Dispose()
        {
            if (m_disposed) {
                return;
            }
            if (m_busy || (m_view != null && !m_view.CanChangeData)) {
                throw new InvalidOperationException("Dispose outside controller callbacks.");
            }
            m_busy = true;
            try {
                if (m_view != null && m_view.UsesAdapter(this)) {
                    m_view.Dispose();
                }
            }
            finally {
                m_data.Clear();
                m_nextData.Clear();
                m_rows.Clear();
                m_nextRows.Clear();
                m_rowIndices.Clear();
                m_nextRowIndices.Clear();
                m_updates.Clear();
                m_itemSortBuffer.Clear();
                m_itemSortGroup = default;
                GroupComparison = null;
                ItemComparison = null;
                ExpansionChanged = null;
                ClearRenderingCallbacks();
                m_getGroupKey = null;
                m_getItems = null;
                m_getItemKey = null;
                m_adapter = null;
                m_disposed = true;
                m_busy = false;
            }
        }

        private void CheckAvailable()
        {
            if (m_disposed || m_view == null) {
                throw new ObjectDisposedException(nameof(ExpandableListController<TGroup, TItem, TKey>));
            }
            if (m_busy || !m_view.CanChangeData || (m_view.IsInitialized && (!m_view.UsesAdapter(this) || m_view.ItemCount != m_rows.Count)) || m_view.FirstDataIndex != 0) {
                throw new InvalidOperationException("Use the exclusively owned list outside controller callbacks; FirstDataIndex must remain zero.");
            }
        }

        private void EnterMutation()
        {
            CheckAvailable();
            m_busy = true;
        }

        private void FinishMutation()
        {
            // Clear old/staged payload references but retain capacities for future submissions.
            m_nextData.Clear();
            m_nextRows.Clear();
            m_nextRowIndices.Clear();
            m_updates.Clear();
            m_replaceData = false;
            m_expansionGroup = -1;
            m_expandAll = false;
            m_itemSortGroup = default;
            m_itemSortBuffer.Clear();
            m_busy = false;
        }
    }
}
