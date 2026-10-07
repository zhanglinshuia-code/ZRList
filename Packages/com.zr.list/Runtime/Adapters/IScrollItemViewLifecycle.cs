// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

namespace ZRList
{
    /// <summary>Optional hooks for canceling view work and releasing view-owned resources.</summary>
    public interface IScrollItemViewLifecycle
    {
        void OnViewUnbound(ScrollItemView holder, int dataIndex);
        void OnViewDestroyed(ScrollItemView holder);
    }
}
