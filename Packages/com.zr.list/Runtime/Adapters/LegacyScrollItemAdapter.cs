// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList
{
    /// <summary>Preserves Init(Action&lt;GameObject, int&gt;) for existing integrations.</summary>
    internal sealed class LegacyScrollItemAdapter: IScrollItemAdapter
    {
        private readonly Action<GameObject, int> m_bind;
        internal bool CanReuseViews(LegacyScrollItemAdapter other)
        {
            return m_bind == other.m_bind;
        }

        public float EstimatedItemSize { get; }

        public LegacyScrollItemAdapter(Action<GameObject, int> bind, float estimatedSize)
        {
            m_bind = bind;
            EstimatedItemSize = estimatedSize;
        }

        public ScrollItemView CreateViewsHolder(RectTransform root)
        {
            // The original implementation supplied a fitter even when absent.
            if (root.GetComponent<ContentSizeFitter>() == null) {
                root.gameObject.AddComponent<ContentSizeFitter>();
            }

            return new ScrollItemView(root, -1, -1);
        }

        public void Bind(ScrollItemView holder, int dataIndex, ScrollItemLayoutContext context)
        {
            m_bind(holder.Root.gameObject, dataIndex);
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
    }
}
