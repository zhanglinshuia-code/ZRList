// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    public sealed partial class VirtualGridView
    {
        private readonly Dictionary<GameObject, ScrollItemView> m_boundCellsByRoot = new Dictionary<GameObject, ScrollItemView>();

        /// <summary>Find a rendered, bound cell by data index. Off-screen data is not instantiated by this query.</summary>
        public bool TryGetVisibleItem(int dataIndex, out ScrollItemView cell)
        {
            EnsureLayoutCurrent();
            return TryGetVisibleItemCore(dataIndex, out cell);
        }

        /// <summary>Resolve the current data index of a cell owned by this grid; returns -1 on failure.</summary>
        public bool TryGetItemIndex(ScrollItemView item, out int dataIndex)
        {
            EnsureLayoutCurrent();
            return TryGetItemIndexCore(item, out dataIndex);
        }

        /// <summary>Resolve a cell root GameObject. Child objects and recycled or foreign roots return -1.</summary>
        public bool TryGetItemIndex(GameObject item, out int dataIndex)
        {
            EnsureLayoutCurrent();
            dataIndex = -1;
            return item != null && m_boundCellsByRoot.TryGetValue(item, out ScrollItemView cell) && cell.Root != null && cell.Root.gameObject == item && TryGetItemIndexCore(cell, out dataIndex);
        }

        /// <summary>Replace results with active cells intersecting the viewport, in row/column order. Partial visibility counts by default.</summary>
        public int GetVisibleItems(List<ScrollItemView> results, bool fullyVisible = false)
        {
            if (results == null) {
                throw new ArgumentNullException(nameof(results));
            }

            EnsureLayoutCurrent();
            results.Clear();
            if (!IsInitialized) {
                return 0;
            }

            RowScrollView.EnsureLayoutCurrent();
            if (!isActiveAndEnabled || !RowScrollView.CanQueryViewport || m_rebuildingRows || m_callbackDepth > 0) {
                return 0;
            }

            var viewport = new ScrollViewportGeometry(RowScrollView.Viewport);
            IReadOnlyList<ScrollItemView> rows = RowScrollView.VisibleItems;
            for (var rowIndex = 0; rowIndex < rows.Count; ++rowIndex) {
                var row = (RowView)rows[rowIndex];
                if (!row.IsBound) {
                    continue;
                }

                for (var column = 0; column < row.Cells.Count; ++column) {
                    ScrollItemView cell = row.Cells[column];
                    if (cell != null && cell.IsBound && cell.ItemIndex >= 0 && cell.ItemIndex < m_itemCount && viewport.IsVisible(cell.Root, fullyVisible)) {
                        results.Add(cell);
                    }
                }
            }

            return results.Count;
        }

        /// <summary>Test a cell against both viewport axes, including grids wider than their viewport.</summary>
        public bool IsItemVisible(int dataIndex, bool fullyVisible = false)
        {
            return TryGetVisibleItem(dataIndex, out ScrollItemView cell) && isActiveAndEnabled && RowScrollView.CanQueryViewport && !m_rebuildingRows && m_callbackDepth == 0 && new ScrollViewportGeometry(RowScrollView.Viewport).IsVisible(cell.Root, fullyVisible);
        }

        private bool TryGetVisibleItemCore(int dataIndex, out ScrollItemView cell)
        {
            cell = null;
            if (!IsInitialized || dataIndex < 0 || dataIndex >= m_itemCount || !RowScrollView.TryGetVisibleItem(dataIndex / m_columns, out ScrollItemView item) || item is not RowView row) {
                return false;
            }

            int column = dataIndex % m_columns;
            if (column >= row.Cells.Count) {
                return false;
            }

            ScrollItemView candidate = row.Cells[column];
            if (candidate == null || candidate.Root == null || !candidate.IsBound || candidate.ItemIndex != dataIndex) {
                return false;
            }

            cell = candidate;
            return true;
        }

        private bool TryGetItemIndexCore(ScrollItemView item, out int dataIndex)
        {
            dataIndex = -1;
            if (item == null || !TryGetVisibleItemCore(item.ItemIndex, out ScrollItemView current) || !ReferenceEquals(item, current)) {
                return false;
            }

            dataIndex = item.ItemIndex;
            return true;
        }
    }
}
