// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

namespace ZRList
{
    /// <summary>Geometry supplied by the list. ItemSize excludes list padding and spacing.</summary>
    public readonly struct ScrollItemLayoutContext
    {
        public readonly ScrollOrientation Orientation;
        public readonly float AvailableCrossSize;
        public readonly float ItemCrossSize;
        public readonly int Revision;
        public ScrollItemLayoutContext(ScrollOrientation orientation, float availableCrossSize, float itemCrossSize, int revision)
        {
            Orientation = orientation;
            AvailableCrossSize = availableCrossSize;
            ItemCrossSize = itemCrossSize;
            Revision = revision;
        }
    }
}
