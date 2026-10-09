// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace ZRList
{
    public sealed partial class TreeListController<TNode, TKey>
    {
        private struct NodeEntry
        {
            public TKey Key;
            public TNode Data;
            public int Parent;
            public int Depth;
            public int FirstChild;
            public int ChildCount;
            public int NextSibling;
            public int SourceIndex;
            public bool Expanded;
        }

        private sealed class Snapshot
        {
            // Siblings occupy contiguous ranges; parent and sibling links support iterative DFS.
            public readonly List<NodeEntry> Nodes = new List<NodeEntry>();
            public readonly Dictionary<TKey, int> Indices = new Dictionary<TKey, int>();
            public int RootCount;

            public void Clear()
            {
                Nodes.Clear();
                Indices.Clear();
                RootCount = 0;
            }
        }

        private void ReadSnapshot(IReadOnlyList<TNode> roots)
        {
            m_nextData.RootCount = roots.Count;
            AppendSiblings(roots, -1);
            // Breadth-first ingestion requires neither recursion nor per-depth enumeration objects.
            for (int i = 0; i < m_nextData.Nodes.Count; ++i) {
                NodeEntry entry = m_nextData.Nodes[i];
                IReadOnlyList<TNode> children = m_getChildren(entry.Data);
                entry.FirstChild = m_nextData.Nodes.Count;
                entry.ChildCount = children == null ? 0 : children.Count;
                if (entry.ChildCount > 0) {
                    AppendSiblings(children, i);
                }
                m_nextData.Nodes[i] = entry;
            }
        }

        private void AppendSiblings(IReadOnlyList<TNode> nodes, int parent)
        {
            int start = m_nextData.Nodes.Count;
            int depth = parent < 0 ? 0 : m_nextData.Nodes[parent].Depth + 1;
            bool sort = nodes.Count > 1 && (NodeComparison != null || (parent >= 0 && ChildComparison != null));
            List<NodeEntry> destination = sort ? m_siblings : m_nextData.Nodes;
            for (int i = 0; i < nodes.Count; ++i) {
                TNode node = nodes[i];
                TKey key = m_getKey(node);
                if (m_nextData.Indices.ContainsKey(key)) {
                    throw new ArgumentException("Node keys must be globally unique. Cycles and repeated occurrences are not supported.", nameof(nodes));
                }
                m_nextData.Indices.Add(key, -1);
                destination.Add(new NodeEntry
                {
                    Key = key, Data = node, Parent = parent, Depth = depth, SourceIndex = i,
                    Expanded = m_data.Indices.TryGetValue(key, out int previous) ? m_data.Nodes[previous].Expanded : DefaultExpanded
                });
            }
            if (sort) {
                m_sortParent = parent;
                // Cached Comparison avoids Unity's allocating range/IComparer overload.
                m_siblings.Sort(m_compareSiblings);
                m_nextData.Nodes.AddRange(m_siblings);
                m_siblings.Clear();
            }
            for (int i = start; i < m_nextData.Nodes.Count; ++i) {
                NodeEntry entry = m_nextData.Nodes[i];
                entry.NextSibling = i + 1 < m_nextData.Nodes.Count ? i + 1 : -1;
                m_nextData.Nodes[i] = entry;
                m_nextData.Indices[entry.Key] = i;
            }
        }

        private int CompareSiblings(NodeEntry left, NodeEntry right)
        {
            int result = m_sortParent >= 0 && ChildComparison != null
                ? ChildComparison(m_nextData.Nodes[m_sortParent].Data, left.Data, right.Data)
                : NodeComparison(left.Data, right.Data);
            return result != 0 ? result : left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private static TreeListRow<TNode, TKey> CreateRow(Snapshot data, int index, bool expanded)
        {
            NodeEntry entry = data.Nodes[index];
            return new TreeListRow<TNode, TKey>(entry.Key, entry.Data, entry.Parent >= 0,
                entry.Parent >= 0 ? data.Nodes[entry.Parent].Key : default, entry.Depth, entry.ChildCount, expanded);
        }

        private void BuildRows(Snapshot data)
        {
            int index = data.RootCount > 0 ? 0 : -1;
            while (index >= 0) {
                NodeEntry entry = data.Nodes[index];
                bool expanded = m_expandAll || m_expansionChanges.Contains(index) ? m_expansionValue : entry.Expanded;
                TreeListRow<TNode, TKey> row = CreateRow(data, index, expanded);
                int previous = m_rowIndices.TryGetValue(entry.Key, out int oldIndex) ? oldIndex : -1;
                bool preserveSize = !m_replaceData && previous >= 0 && m_rows[previous].IsExpanded == row.IsExpanded;
                m_nextRowIndices.Add(entry.Key, m_nextRows.Count);
                m_nextRows.Add(row);
                m_updates.Add(new ScrollItemUpdate(previous, preserveSize));
                if (row.IsExpanded) {
                    index = entry.FirstChild;
                    continue;
                }
                // Skip collapsed subtrees entirely, then ascend to the next pending sibling.
                while (index >= 0 && data.Nodes[index].NextSibling < 0) {
                    index = data.Nodes[index].Parent;
                }
                if (index >= 0) {
                    index = data.Nodes[index].NextSibling;
                }
            }
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
            if (oldAnchor >= 0) {
                TKey key = m_rows[oldAnchor].Key;
                // Reparented anchors can now live under a different collapsed ancestor.
                Snapshot target = m_replaceData ? m_nextData : m_data;
                if (target.Indices.TryGetValue(key, out int targetIndex)) {
                    fallback = FindVisibleAncestor(target, targetIndex);
                }
                // Deleted anchors fall back to their closest surviving visible old ancestor.
                if (!fallback.HasValue && m_data.Indices.TryGetValue(key, out int oldIndex)) {
                    fallback = FindVisibleAncestor(m_data, oldIndex);
                }
            }
            var options = new ScrollDataUpdateOptions(position, preferredIndex >= 0 ? preferredIndex : (int?)null, fallback);
            m_view.ApplyData(m_updates, m_commit, options);
        }

        private int? FindVisibleAncestor(Snapshot data, int index)
        {
            for (int parent = data.Nodes[index].Parent; parent >= 0; parent = data.Nodes[parent].Parent) {
                if (m_nextRowIndices.TryGetValue(data.Nodes[parent].Key, out int rowIndex)) {
                    return rowIndex;
                }
            }
            return null;
        }

        private void CommitPrepared()
        {
            if (m_replaceData) {
                Snapshot old = m_data;
                m_data = m_nextData;
                m_nextData = old;
            }
            else if (m_expandAll) {
                for (int i = 0; i < m_data.Nodes.Count; ++i) {
                    if (m_data.Nodes[i].ChildCount > 0) {
                        CommitExpansion(i);
                    }
                }
            }
            else {
                foreach (int index in m_expansionChanges) {
                    CommitExpansion(index);
                }
            }
            List<TreeListRow<TNode, TKey>> oldRows = m_rows;
            m_rows = m_nextRows;
            m_nextRows = oldRows;
            Dictionary<TKey, int> oldIndices = m_rowIndices;
            m_rowIndices = m_nextRowIndices;
            m_nextRowIndices = oldIndices;
        }

        private void CommitExpansion(int index)
        {
            NodeEntry entry = m_data.Nodes[index];
            entry.Expanded = m_expansionValue;
            m_data.Nodes[index] = entry;
        }
    }
}
