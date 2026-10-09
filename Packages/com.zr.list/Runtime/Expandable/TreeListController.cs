// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace ZRList
{
    /// <summary>
    /// Projects a multi-level tree onto one exclusively owned VirtualScrollView.
    /// Main-thread only. Submit reads and sorts the full tree; expansion uses the snapshot.
    /// Traversal is iterative and buffers are reused. No per-node internal containers.
    /// </summary>
    public sealed partial class TreeListController<TNode, TKey>: IDisposable
    {
        private readonly VirtualScrollView m_view;
        private TreeListAdapter<TNode, TKey> m_adapter;
        private Func<TNode, TKey> m_getKey;
        private Func<TNode, IReadOnlyList<TNode>> m_getChildren;
        private readonly Action m_commit;
        private readonly Comparison<NodeEntry> m_compareSiblings;
        private readonly List<NodeEntry> m_siblings = new List<NodeEntry>();
        private readonly HashSet<int> m_expansionChanges = new HashSet<int>();
        private readonly List<ScrollItemUpdate> m_updates = new List<ScrollItemUpdate>();
        private Snapshot m_data = new Snapshot();
        private Snapshot m_nextData = new Snapshot();
        private List<TreeListRow<TNode, TKey>> m_rows = new List<TreeListRow<TNode, TKey>>();
        private List<TreeListRow<TNode, TKey>> m_nextRows = new List<TreeListRow<TNode, TKey>>();
        private Dictionary<TKey, int> m_rowIndices = new Dictionary<TKey, int>();
        private Dictionary<TKey, int> m_nextRowIndices = new Dictionary<TKey, int>();
        private int m_sortParent = -1;
        private bool m_busy;
        private bool m_disposed;
        private bool m_replaceData;
        private bool m_expandAll;
        private bool m_expansionValue;

        /// <summary>Initial expansion preference for keys first encountered by Submit.</summary>
        public bool DefaultExpanded { get; set; }
        /// <summary>Optional comparison within every sibling set, including roots. Null preserves input order.</summary>
        public Comparison<TNode> NodeComparison { get; set; }
        /// <summary>Optional (parent, left, right) override for non-root siblings.</summary>
        public Func<TNode, TNode, TNode, int> ChildComparison { get; set; }
        /// <summary>One event after an explicit expansion operation, including navigation through hidden ancestors.</summary>
        public event Action ExpansionChanged;
        public int NodeCount { get { return m_data.Nodes.Count; } }
        public int RootCount { get { return m_data.RootCount; } }
        public int RowCount { get { return m_rows.Count; } }

        /// <summary>Callback-based entry point. Accessors return stable keys and existing child collections.</summary>
        public TreeListController(VirtualScrollView view, Func<TNode, TKey> getKey,
            Func<TNode, IReadOnlyList<TNode>> getChildren): this(view)
        {
            m_getKey = getKey ?? throw new ArgumentNullException(nameof(getKey));
            m_getChildren = getChildren ?? throw new ArgumentNullException(nameof(getChildren));
        }

        /// <summary>通过一个 Adapter 接入整套绑定逻辑；不要同时配置控制器的显示回调。</summary>
        public TreeListController(VirtualScrollView view, TreeListAdapter<TNode, TKey> adapter): this(view)
        {
            m_adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            // 只在构造时缓存访问委托，提交与滚动期间不创建委托。
            m_getKey = adapter.GetKey;
            m_getChildren = adapter.GetChildren;
        }

        private TreeListController(VirtualScrollView view)
        {
            if (view == null) {
                throw new ArgumentNullException(nameof(view));
            }
            if (view.IsInitialized || view.FirstDataIndex != 0) {
                throw new InvalidOperationException("Attach to an uninitialized list with FirstDataIndex = 0.");
            }
            m_view = view;
            m_commit = CommitPrepared;
            m_compareSiblings = CompareSiblings;
        }

        /// <summary>
        /// Validates all nodes, including hidden descendants, before committing one layout update.
        /// Retains expansion by key, including across parent changes. Payloads remain business-owned.
        /// Duplicate keys (including cycles/shared occurrences) and accessor/sort errors leave the snapshot intact.
        /// </summary>
        public void Submit(IReadOnlyList<TNode> roots, ScrollUpdatePosition position = ScrollUpdatePosition.KeepVisibleItem)
        {
            if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            }
            EnterMutation();
            try {
                ReadSnapshot(roots);
                m_replaceData = true;
                BuildRows(m_nextData);
                ApplyPrepared(position);
            }
            finally {
                FinishMutation();
            }
        }

        public bool IsExpanded(TKey key)
        {
            return m_data.Indices.TryGetValue(key, out int index) && m_data.Nodes[index].ChildCount > 0 && m_data.Nodes[index].Expanded;
        }

        public bool Toggle(TKey key)
        {
            return SetExpanded(key, !IsExpanded(key));
        }

        /// <summary>Leaves/unknown keys return false. Hidden branches update state without rebuilding the visible list.</summary>
        public bool SetExpanded(TKey key, bool expanded)
        {
            EnterMutation();
            try {
                if (!m_data.Indices.TryGetValue(key, out int index)) {
                    return false;
                }
                NodeEntry entry = m_data.Nodes[index];
                if (entry.ChildCount == 0 || entry.Expanded == expanded) {
                    return false;
                }
                if (m_rowIndices.TryGetValue(key, out int rowIndex)) {
                    m_expansionChanges.Add(index);
                    m_expansionValue = expanded;
                    BuildRows(m_data);
                    ApplyPrepared(ScrollUpdatePosition.KeepVisibleItem, rowIndex);
                }
                else {
                    entry.Expanded = expanded;
                    m_data.Nodes[index] = entry;
                }
                ExpansionChanged?.Invoke();
                return true;
            }
            finally {
                FinishMutation();
            }
        }

        /// <summary>Changes every branch, including hidden descendants, in one transaction.</summary>
        public void SetAllExpanded(bool expanded)
        {
            EnterMutation();
            try {
                bool changed = false;
                for (int i = 0; i < m_data.Nodes.Count; ++i) {
                    NodeEntry entry = m_data.Nodes[i];
                    changed |= entry.ChildCount > 0 && entry.Expanded != expanded;
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

        /// <summary>Reads a submitted node even if its ancestors are collapsed.</summary>
        public bool TryGetNode(TKey key, out TreeListRow<TNode, TKey> node)
        {
            if (m_data.Indices.TryGetValue(key, out int index)) {
                node = CreateRow(m_data, index, m_data.Nodes[index].Expanded);
                return true;
            }
            node = default;
            return false;
        }

        public bool TryGetRow(int rowIndex, out TreeListRow<TNode, TKey> row)
        {
            if (rowIndex >= 0 && rowIndex < m_rows.Count) {
                row = m_rows[rowIndex];
                return true;
            }
            row = default;
            return false;
        }

        /// <summary>Returns false for hidden nodes. Row indices are valid only until the next update.</summary>
        public bool TryGetRowIndex(TKey key, out int rowIndex)
        {
            return m_rowIndices.TryGetValue(key, out rowIndex);
        }

        /// <summary>Optionally opens all hidden ancestors in one transaction, then uses normal list navigation.</summary>
        public bool ScrollToNode(TKey key, bool expandIfNeeded = true, float duration = 0f)
        {
            CheckAvailable();
            if (float.IsNaN(duration) || float.IsInfinity(duration) || !m_data.Indices.TryGetValue(key, out int index)) {
                return false;
            }
            if (!m_rowIndices.ContainsKey(key)) {
                if (!expandIfNeeded) {
                    return false;
                }
                EnterMutation();
                try {
                    for (int parent = m_data.Nodes[index].Parent; parent >= 0; parent = m_data.Nodes[parent].Parent) {
                        if (!m_data.Nodes[parent].Expanded) {
                            m_expansionChanges.Add(parent);
                        }
                    }
                    m_expansionValue = true;
                    BuildRows(m_data);
                    ApplyPrepared(ScrollUpdatePosition.KeepVisibleItem);
                    ExpansionChanged?.Invoke();
                }
                finally {
                    FinishMutation();
                }
            }
            return TryGetRowIndex(key, out int rowIndex) && m_view.JumpToDataItem(rowIndex, duration);
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
                m_rows.Clear();
                m_rowIndices.Clear();
                NodeComparison = null;
                ChildComparison = null;
                ExpansionChanged = null;
                ClearRenderingCallbacks();
                m_getKey = null;
                m_getChildren = null;
                m_adapter = null;
                m_disposed = true;
                FinishMutation();
            }
        }

        private void CheckAvailable()
        {
            if (m_disposed || m_view == null) {
                throw new ObjectDisposedException(nameof(TreeListController<TNode, TKey>));
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
            // Release old/staged payload references while retaining reusable capacities.
            m_nextData.Clear();
            m_nextRows.Clear();
            m_nextRowIndices.Clear();
            m_siblings.Clear();
            m_updates.Clear();
            m_expansionChanges.Clear();
            m_replaceData = false;
            m_expandAll = false;
            m_sortParent = -1;
            m_busy = false;
        }
    }
}
