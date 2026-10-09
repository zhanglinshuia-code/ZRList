// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

namespace ZRList
{
    /// <summary>Return a finite positive main-axis size, or false to measure the bound view.</summary>
    public delegate bool ScrollItemSizeProvider<TRow>(TRow row, ScrollItemLayoutContext context, out float size);
}
