// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    public partial class VirtualScrollView
    {
        private int[] m_dataOldToNew = Array.Empty<int>();
        private bool m_applyingData;

        internal bool CanChangeData
        {
            get { return !m_applyingData && !m_rebuilding && !m_flushing && !m_preparingLayout && m_updateDepth == 0 && m_lifecycleCallbackDepth == 0; }
        }

        internal bool UsesAdapter(IScrollItemAdapter adapter)
        {
            return m_initialized && ReferenceEquals(m_adapter, adapter);
        }

        internal int GetUpdateAnchorIndex(int preferredIndex = -1)
        {
            EnsureLayoutCurrent();
            ViewAnchor anchor = CaptureDataAnchor(preferredIndex);
            return anchor.Valid ? anchor.Index : -1;
        }

        /// <summary>
        /// Atomically replaces the displayed sequence. Keep the old adapter data available
        /// until commitData is called; that callback must only swap prepared data, without
        /// calling list APIs. Each entry maps a new index to a unique old index, or -1.
        /// All visible bindings are renewed. Requires FirstDataIndex = 0 and execution
        /// outside item callbacks/update batches. The map must remain unchanged until return.
        /// </summary>
        public void ApplyData(IReadOnlyList<ScrollItemUpdate> items, Action commitData,
            ScrollDataUpdateOptions options = default)
        {
            if (items == null || commitData == null) {
                throw new ArgumentNullException(items == null ? nameof(items) : nameof(commitData));
            }
            if (!m_initialized || m_applyingData || m_rebuilding || m_flushing || m_preparingLayout || m_updateDepth > 0 || m_lifecycleCallbackDepth > 0) {
                throw new InvalidOperationException("Apply data to an initialized list outside callbacks and update batches.");
            }

            EnsureLayoutCurrent();
            if (FirstDataIndex != 0) {
                throw new InvalidOperationException("Sequence updates require FirstDataIndex to be zero.");
            }
            if (options.Position < ScrollUpdatePosition.KeepVisibleItem || options.Position > ScrollUpdatePosition.Start ||
                (options.PreferredAnchorIndex.HasValue && (options.PreferredAnchorIndex.Value < 0 || options.PreferredAnchorIndex.Value >= ItemCount)) ||
                (options.FallbackAnchorIndex.HasValue && (options.FallbackAnchorIndex.Value < 0 || options.FallbackAnchorIndex.Value >= items.Count))) {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            int oldCount = ItemCount;
            if (m_dataOldToNew.Length < oldCount) {
                Array.Resize(ref m_dataOldToNew, (int)Math.Min(int.MaxValue - 1L, Math.Max(oldCount, Math.Max(16L, (long)m_dataOldToNew.Length * 2))));
            }
            for (int i = 0; i < oldCount; ++i) {
                m_dataOldToNew[i] = -1;
            }
            for (int i = 0; i < items.Count; ++i) {
                int previous = items[i].PreviousIndex;
                if (previous < -1 || previous >= oldCount || (previous >= 0 && m_dataOldToNew[previous] >= 0)) {
                    throw new ArgumentException("Previous indices must be unique, valid old indices or -1.", nameof(items));
                }
                if (previous >= 0) {
                    m_dataOldToNew[previous] = i;
                }
            }
            float estimate = m_adapter.EstimatedItemSize;
            if (!IsValidSize(estimate)) {
                throw new InvalidOperationException("Estimated size must be finite and positive.");
            }

            m_applyingData = true;
            try {
                FlushPendingUpdates();
                ViewAnchor anchor = CaptureDataAnchor(options.PreferredAnchorIndex ?? -1);
                float offset = Offset;
                int nextAnchor = ResolveDataAnchor(anchor, items.Count, options.FallbackAnchorIndex);
                // A deleted anchor falls back to the leading edge of a surviving row.
                float inset = anchor.Valid && m_dataOldToNew[anchor.Index] >= 0 ? anchor.Inset : StartPadding;
                CancelJump();
                m_rebuilding = true;
                try {
                    // Every old lifecycle callback sees the old snapshot, even for retained roots.
                    for (int i = 0; i < VisibleItemViews.Count; ++i) {
                        UnbindHolder(VisibleItemViews[i]);
                    }
                    commitData();
                    m_sizeIndex.ResetMapped(items, estimate, ContentSpacing);
                    ResetDataState(items.Count);
                    RemapVisibleData();
                    UpdateContentSize();
                    InvalidateRenderCache();
                }
                finally {
                    m_rebuilding = false;
                }

                if (options.Position == ScrollUpdatePosition.Start) {
                    offset = 0f;
                    ScrollRect.StopMovement();
                }
                else if (options.Position == ScrollUpdatePosition.KeepVisibleItem && nextAnchor >= 0) {
                    offset = GetEstimatedItemStart(nextAnchor) - inset;
                }

                if (isActiveAndEnabled) {
                    RenderAtOffset(ConstrainUpdatedOffset(offset));
                    FlushPendingUpdates();
                    // Binding can change sizes before a preferred anchor inside the viewport.
                    // Correct from the new prefix without measuring the whole data set.
                    for (int pass = 0; pass < 2 && options.Position == ScrollUpdatePosition.KeepVisibleItem && nextAnchor >= 0; ++pass) {
                        float corrected = ConstrainUpdatedOffset(GetEstimatedItemStart(nextAnchor) - inset);
                        if (Mathf.Abs(corrected - Offset) < 0.01f) {
                            break;
                        }
                        RenderAtOffset(corrected);
                        FlushPendingUpdates();
                    }
                }
                else {
                    SetOffset(ConstrainUpdatedOffset(offset));
                }
            }
            catch {
                Dispose();
                throw;
            }
            finally {
                m_applyingData = false;
            }
            NotifyViewStateChanged();
        }

        private ViewAnchor CaptureDataAnchor(int preferredIndex)
        {
            if (preferredIndex >= 0 && VisibleItemsByViewIndex.TryGetValue(preferredIndex, out ScrollItemView holder)) {
                float leading = LayoutIsHorizontal ? holder.Root.rect.xMin : holder.Root.rect.yMax;
                float inset = (Content.anchoredPosition[ScrollAxisIndex] + holder.Root.anchoredPosition[ScrollAxisIndex] + leading) * ScrollAxisSign;
                if (inset < m_viewportLength && inset + holder.ItemSize > 0f) {
                    return new ViewAnchor { Index = preferredIndex, Inset = inset, Valid = true };
                }
            }
            return CaptureAnchor();
        }

        private int ResolveDataAnchor(ViewAnchor anchor, int count, int? fallbackIndex)
        {
            if (!anchor.Valid || count == 0) {
                return -1;
            }
            if (m_dataOldToNew[anchor.Index] >= 0) {
                return m_dataOldToNew[anchor.Index];
            }
            if (fallbackIndex.HasValue) {
                return fallbackIndex.Value;
            }
            for (int i = anchor.Index + 1; i < ItemCount; ++i) {
                if (m_dataOldToNew[i] >= 0) {
                    return m_dataOldToNew[i];
                }
            }
            for (int i = anchor.Index - 1; i >= 0; --i) {
                if (m_dataOldToNew[i] >= 0) {
                    return m_dataOldToNew[i];
                }
            }
            return 0;
        }

        private void RemapVisibleData()
        {
            VisibleItemsByViewIndex.Clear();
            m_visibleItemsByRoot.Clear();
            int retained = 0;
            for (int i = 0; i < VisibleItemViews.Count; ++i) {
                ScrollItemView holder = VisibleItemViews[i];
                int next = m_dataOldToNew[holder.ViewIndex];
                if (next < 0) {
                    Recycle(holder);
                    continue;
                }
                holder.ItemIndex = holder.ViewIndex = next;
                VisibleItemViews[retained++] = holder;
                VisibleItemsByViewIndex.Add(next, holder);
                m_visibleItemsByRoot.Add(holder.Root.gameObject, holder);
                m_dirtyItems.Add(next);
            }
            VisibleItemViews.RemoveRange(retained, VisibleItemViews.Count - retained);
        }
    }
}
