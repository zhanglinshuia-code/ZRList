// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    public partial class VirtualScrollView
    {
        private readonly Dictionary<GameObject, ScrollItemView> m_visibleItemsByRoot = new Dictionary<GameObject, ScrollItemView>();

        internal bool CanQueryViewport
        {
            get
            {
                return m_initialized && isActiveAndEnabled && !m_rebuilding && !m_flushing && !m_preparingLayout;
            }
        }

        /// <summary>Find a rendered, bound view by data index. Off-screen data is not instantiated by this query.</summary>
        public bool TryGetVisibleItem(int dataIndex, out ScrollItemView holder)
        {
            EnsureLayoutCurrent();
            holder = null;
            if (!IsValidDataIndex(dataIndex) || !VisibleItemsByViewIndex.TryGetValue(GetViewIndex(dataIndex), out ScrollItemView candidate) || !IsBoundItem(candidate) || candidate.ItemIndex != dataIndex) {
                return false;
            }

            holder = candidate;
            return true;
        }

        /// <summary>Resolve the current data index of a view owned by this list; returns -1 on failure.</summary>
        public bool TryGetItemIndex(ScrollItemView item, out int dataIndex)
        {
            EnsureLayoutCurrent();
            dataIndex = -1;
            if (!IsBoundItem(item)) {
                return false;
            }

            dataIndex = item.ItemIndex;
            return true;
        }

        /// <summary>Resolve an item root GameObject. Child objects and recycled or foreign roots return -1.</summary>
        public bool TryGetItemIndex(GameObject item, out int dataIndex)
        {
            EnsureLayoutCurrent();
            dataIndex = -1;
            if (!TryGetBoundItem(item, out ScrollItemView holder)) {
                return false;
            }

            dataIndex = holder.ItemIndex;
            return true;
        }

        /// <summary>Replace results with active views intersecting the viewport, in layout order. Partial visibility counts by default.</summary>
        public int GetVisibleItems(List<ScrollItemView> results, bool fullyVisible = false)
        {
            if (results == null) {
                throw new ArgumentNullException(nameof(results));
            }

            if (ReferenceEquals(results, VisibleItemViews)) {
                throw new ArgumentException("Use a separate results list.", nameof(results));
            }

            EnsureLayoutCurrent();
            results.Clear();
            if (!CanQueryViewport) {
                return 0;
            }

            var viewport = new ScrollViewportGeometry(Viewport);
            for (var index = 0; index < VisibleItemViews.Count; ++index) {
                ScrollItemView item = VisibleItemViews[index];
                if (IsBoundItem(item) && viewport.IsVisible(item.Root, fullyVisible)) {
                    results.Add(item);
                }
            }

            return results.Count;
        }

        /// <summary>Test the actual viewport rectangle without creating an off-screen view.</summary>
        public bool IsItemVisible(int dataIndex, bool fullyVisible = false)
        {
            return TryGetVisibleItem(dataIndex, out ScrollItemView item) && CanQueryViewport && new ScrollViewportGeometry(Viewport).IsVisible(item.Root, fullyVisible);
        }

        /// <summary>Map a layout-order index to a data index, including FirstDataIndex rotation. Returns -1 on failure.</summary>
        public bool TryGetDataIndex(int viewIndex, out int dataIndex)
        {
            EnsureLayoutCurrent();
            dataIndex = -1;
            if (!IsValidDataIndex(viewIndex)) {
                return false;
            }

            dataIndex = GetDataIndex(viewIndex);
            return true;
        }

        /// <summary>Map a data index to a layout-order index, including FirstDataIndex rotation. Returns -1 on failure.</summary>
        public bool TryGetViewIndex(int dataIndex, out int viewIndex)
        {
            EnsureLayoutCurrent();
            viewIndex = -1;
            if (!IsValidDataIndex(dataIndex)) {
                return false;
            }

            viewIndex = GetViewIndex(dataIndex);
            return true;
        }

        private bool IsBoundItem(ScrollItemView item)
        {
            return item != null && item.Root != null && item.IsBound && IsValidDataIndex(item.ItemIndex) && VisibleItemsByViewIndex.TryGetValue(item.ViewIndex, out ScrollItemView current) && ReferenceEquals(item, current);
        }

        private bool TryGetBoundItem(GameObject root, out ScrollItemView item)
        {
            item = null;
            if (root == null || !m_visibleItemsByRoot.TryGetValue(root, out ScrollItemView candidate) || !IsBoundItem(candidate) || candidate.Root.gameObject != root) {
                return false;
            }

            item = candidate;
            return true;
        }
    }
}
