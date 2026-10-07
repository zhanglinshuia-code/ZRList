// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;

namespace ZRList
{
    /// <summary>
    /// Owns data binding and size calculation. TryGetItemSize returns a freshly
    /// calculated size without a view, or false to measure the bound view instead.
    /// Every binding obtains a new size. Call RefreshItem when bound content changes.
    /// </summary>
    public interface IScrollItemAdapter
    {
        float EstimatedItemSize { get; }

        ScrollItemView CreateViewsHolder(RectTransform root);
        void Bind(ScrollItemView holder, int dataIndex, ScrollItemLayoutContext context);
        bool TryGetItemSize(int dataIndex, ScrollItemLayoutContext context, out float size);
        float MeasureItem(ScrollItemView holder, int dataIndex, ScrollItemLayoutContext context);
    }
}
