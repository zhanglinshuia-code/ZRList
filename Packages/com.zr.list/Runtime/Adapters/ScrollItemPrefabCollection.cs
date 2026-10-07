// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    internal static class ScrollItemPrefabCollection
    {
        internal static void Validate(IReadOnlyList<RectTransform> prefabs, Func<int, int> selectPrefabIndex)
        {
            if (prefabs == null) {
                throw new ArgumentNullException(nameof(prefabs));
            }

            if (prefabs.Count == 0) {
                throw new ArgumentException("Supply at least one item prefab.", nameof(prefabs));
            }

            if (prefabs.Count > 1 && selectPrefabIndex == null) {
                throw new ArgumentNullException(nameof(selectPrefabIndex), "Select a prefab index when using multiple item prefabs.");
            }

            for (var index = 0; index < prefabs.Count; ++index) {
                if (prefabs[index] == null) {
                    throw new ArgumentException("Item prefabs cannot contain null entries.", nameof(prefabs));
                }
            }

        }

        internal static RectTransform Select(IReadOnlyList<RectTransform> prefabs, Func<int, int> selectPrefabIndex, int dataIndex)
        {
            if (prefabs.Count > 1 && selectPrefabIndex == null) {
                throw new InvalidOperationException("Select a prefab index when expanding the prefab collection.");
            }

            int prefabIndex = selectPrefabIndex != null ? selectPrefabIndex(dataIndex) : 0;
            if (prefabIndex < 0 || prefabIndex >= prefabs.Count) {
                throw new ArgumentOutOfRangeException(nameof(prefabIndex), prefabIndex, "The selected index is outside the item prefab collection.");
            }

            RectTransform prefab = prefabs[prefabIndex];
            if (prefab == null) {
                throw new InvalidOperationException("The selected item prefab is null.");
            }

            return prefab;
        }
    }
}
