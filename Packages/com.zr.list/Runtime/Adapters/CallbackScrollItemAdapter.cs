// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    internal sealed class CallbackScrollItemAdapter: IScrollItemAdapter, IScrollItemPrefabProvider, IScrollItemViewLifecycle, IScrollItemRenderState
    {
        public RectTransform DefaultPrefab;
        public Func<int, RectTransform> PrefabSelector;
        public IReadOnlyList<RectTransform> Prefabs;
        public Func<int, int> PrefabIndexSelector;
        public Action<ScrollItemView, int> Render;
        public Action<ScrollItemView, int> Recycle;
        public float EstimatedItemSize { get; set; }
        public bool IsRendererReady { get { return Render != null; } }

        public void ClearPrefabSelection()
        {
            DefaultPrefab = null;
            PrefabSelector = null;
            Prefabs = null;
            PrefabIndexSelector = null;
        }

        public RectTransform GetItemPrefab(int dataIndex)
        {
            if (PrefabSelector != null) {
                return PrefabSelector(dataIndex);
            }

            return Prefabs != null ? ScrollItemPrefabCollection.Select(Prefabs, PrefabIndexSelector, dataIndex) : DefaultPrefab;
        }

        public ScrollItemView CreateViewsHolder(RectTransform root)
        {
            return new ScrollItemView(root, -1, -1);
        }

        public void Bind(ScrollItemView holder, int dataIndex, ScrollItemLayoutContext context)
        {
            Render?.Invoke(holder, dataIndex);
        }

        public bool TryGetItemSize(int dataIndex, ScrollItemLayoutContext context, out float size)
        {
            size = 0f;
            return false;
        }

        public float MeasureItem(ScrollItemView holder, int dataIndex, ScrollItemLayoutContext context)
        {
            return ScrollItemMeasurement.Measure(holder.Root, context.Orientation);
        }

        public void OnViewUnbound(ScrollItemView holder, int dataIndex)
        {
            Recycle?.Invoke(holder, dataIndex);
        }

        public void OnViewDestroyed(ScrollItemView holder)
        {
        }
    }
}
