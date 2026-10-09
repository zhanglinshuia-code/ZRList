// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

namespace ZRList
{
    /// <summary>Correspondence for one new data index. -1 introduces a new item.</summary>
    public readonly struct ScrollItemUpdate
    {
        public readonly int PreviousIndex;
        // Only retain geometry when the caller knows the content and template are unchanged.
        public readonly bool PreserveSize;

        public ScrollItemUpdate(int previousIndex, bool preserveSize = false)
        {
            PreviousIndex = previousIndex;
            PreserveSize = preserveSize;
        }
    }

    public enum ScrollUpdatePosition
    {
        KeepVisibleItem,
        KeepOffset,
        Start
    }

    /// <summary>Indices refer to the old preferred anchor and the new fallback, respectively.</summary>
    public readonly struct ScrollDataUpdateOptions
    {
        public readonly ScrollUpdatePosition Position;
        public readonly int? PreferredAnchorIndex;
        public readonly int? FallbackAnchorIndex;

        public ScrollDataUpdateOptions(ScrollUpdatePosition position = ScrollUpdatePosition.KeepVisibleItem,
            int? preferredAnchorIndex = null, int? fallbackAnchorIndex = null)
        {
            Position = position;
            PreferredAnchorIndex = preferredAnchorIndex;
            FallbackAnchorIndex = fallbackAnchorIndex;
        }
    }
}
