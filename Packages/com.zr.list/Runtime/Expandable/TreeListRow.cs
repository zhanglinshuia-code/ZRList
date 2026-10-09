// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

namespace ZRList
{
    /// <summary>Submitted node identity, hierarchy and expansion state. Payloads are not deep-copied.</summary>
    public readonly struct TreeListRow<TNode, TKey>
    {
        public readonly TKey Key;
        public readonly TNode Node;
        public readonly bool HasParent;
        // Only meaningful when HasParent is true. Root depth is zero.
        public readonly TKey ParentKey;
        public readonly int Depth;
        public readonly int ChildCount;
        public readonly bool IsExpanded;
        public bool HasChildren { get { return ChildCount > 0; } }

        internal TreeListRow(TKey key, TNode node, bool hasParent, TKey parentKey, int depth, int childCount, bool expanded)
        {
            Key = key;
            Node = node;
            HasParent = hasParent;
            ParentKey = parentKey;
            Depth = depth;
            ChildCount = childCount;
            IsExpanded = childCount > 0 && expanded;
        }
    }
}
