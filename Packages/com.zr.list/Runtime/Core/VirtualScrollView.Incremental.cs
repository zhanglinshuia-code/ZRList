// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace ZRList
{
    public partial class VirtualScrollView
    {
        private static readonly ProfilerMarker s_updateRangeMarker = new ProfilerMarker("ZRList.UpdateRange");
        private readonly List<ScrollItemView> m_rangeScratch = new List<ScrollItemView>();

        // Reuse the intersection only while its geometry is unchanged. Unknown
        // entering sizes can invalidate this assumption, in which case the full
        // renderer takes over with every view still owned by the visible list.
        private bool TryUpdateVisibleRange(int firstIndex, int lastIndex, float offset)
        {
            int oldFirst = m_renderedFirstIndex;
            int oldLast = m_renderedLastIndex;
            if (firstIndex > oldLast || lastIndex < oldFirst || VisibleItemViews.Count != oldLast - oldFirst + 1) {
                return false;
            }

            using var profileScope = s_updateRangeMarker.Auto();
            long sizeVersion = m_sizeIndex.Version;
            m_hasRenderedRange = false;
            m_rebuilding = true;
            try {
                // Remove the tail before the head so existing list offsets stay
                // valid. Dictionaries change only for items actually leaving.
                if (lastIndex < oldLast) {
                    RecycleVisibleRange(VisibleItemViews.Count - (oldLast - lastIndex), oldLast - lastIndex);
                }
                if (firstIndex > oldFirst) {
                    RecycleVisibleRange(0, firstIndex - oldFirst);
                }
                if (!CanContinueRangeUpdate(sizeVersion)) {
                    return false;
                }

                if (firstIndex < oldFirst && !AddVisibleRange(firstIndex, oldFirst - 1, true, sizeVersion)) {
                    return false;
                }
                if (lastIndex > oldLast && !AddVisibleRange(oldLast + 1, lastIndex, false, sizeVersion)) {
                    return false;
                }
                if (HasLayoutChanged()) {
                    return false;
                }

                SetOffset(offset);
            }
            finally {
                m_rebuilding = false;
            }

            RememberRenderedRange();
            return true;
        }

        private bool CanContinueRangeUpdate(long sizeVersion)
        {
            return m_sizeIndex.Version == sizeVersion && !HasPendingRenderWork;
        }

        private void RecycleVisibleRange(int start, int count)
        {
            for (int i = start; i < start + count; ++i) {
                ScrollItemView holder = VisibleItemViews[i];
                m_rangeScratch.Add(holder);
                VisibleItemsByViewIndex.Remove(holder.ViewIndex);
                m_visibleItemsByRoot.Remove(holder.Root.gameObject);
            }
            VisibleItemViews.RemoveRange(start, count);

            // Transfer ownership before any callback. Even when an unbind throws,
            // every removed view must enter the pool exactly once.
            Exception firstError = null;
            try {
                foreach (ScrollItemView holder in m_rangeScratch) {
                    try {
                        Recycle(holder);
                    }
                    catch (Exception error) {
                        firstError ??= error;
                    }
                }
            }
            finally {
                m_rangeScratch.Clear();
            }
            if (firstError != null) {
                throw firstError;
            }
        }

        private bool AddVisibleRange(int first, int last, bool prepend, long sizeVersion)
        {
            double start = StartPadding + m_sizeIndex.PrefixSize(first);
            try {
                for (int index = first; index <= last; ++index) {
                    ScrollItemView holder = ExtractRecyclableViewsHolderOrCreateNew(GetItemPrefab(index));
                    // Staging head additions avoids shifting the retained range
                    // once per new item. The finalizer publishes them on failure too.
                    if (prepend) {
                        m_rangeScratch.Add(holder);
                    }
                    else {
                        VisibleItemViews.Add(holder);
                    }
                    VisibleItemsByViewIndex[index] = holder;
                    m_visibleItemsByRoot[holder.Root.gameObject] = holder;
                    float size;
                    try {
                        BindHolder(holder, index);
                        SetViewsHolderEnabled(holder);
                        size = ResolveSize(holder, index);
                        LayoutHolder(holder, size, start);
                    }
                    catch {
                        // A failed measurement must be retried too, even when
                        // its binding had already succeeded before the exception.
                        m_dirtyItems.Add(index);
                        throw;
                    }
                    if (!CanContinueRangeUpdate(sizeVersion)) {
                        return false;
                    }
                    start += (double)size + ContentSpacing;
                }
            }
            finally {
                if (prepend) {
                    VisibleItemViews.InsertRange(0, m_rangeScratch);
                    m_rangeScratch.Clear();
                }
            }
            return true;
        }
    }
}
