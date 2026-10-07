// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;

namespace ZRList
{
    /// <summary>
    /// Optional adapter contract for heterogeneous lists. Select a stable prefab
    /// for each data index without changing list state. Refresh or invalidate an
    /// item when its prefab selection changes. Views are recycled by prefab identity.
    /// </summary>
    public interface IScrollItemPrefabProvider
    {
        RectTransform GetItemPrefab(int dataIndex);
    }
}
