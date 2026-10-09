// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

namespace ZRList
{
    /// <summary>Immutable binding identity and presentation state; payloads are not deep-copied.</summary>
    public readonly struct ExpandableListRow<TGroup, TItem, TKey>
    {
        public readonly bool IsHeader;
        public readonly bool IsExpanded;
        public readonly TKey GroupKey;
        // Only meaningful for child rows. Item keys must be unique across all groups.
        public readonly TKey ItemKey;
        public readonly TGroup Group;
        public readonly TItem Item;

        internal ExpandableListRow(TKey groupKey, TGroup group, bool expanded)
        {
            IsHeader = true;
            IsExpanded = expanded;
            GroupKey = groupKey;
            ItemKey = default;
            Group = group;
            Item = default;
        }

        internal ExpandableListRow(TKey groupKey, TGroup group, TKey itemKey, TItem item)
        {
            IsHeader = false;
            IsExpanded = false;
            GroupKey = groupKey;
            ItemKey = itemKey;
            Group = group;
            Item = item;
        }
    }
}
