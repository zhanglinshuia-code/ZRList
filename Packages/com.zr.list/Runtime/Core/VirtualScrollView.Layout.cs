// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace ZRList
{
    public partial class VirtualScrollView
    {
        private static readonly ProfilerMarker s_layoutCheckMarker = new ProfilerMarker("ZRList.CheckLayout");
        private static readonly ProfilerMarker s_rangeLookupMarker = new ProfilerMarker("ZRList.RangeLookup");
        private static readonly ProfilerMarker s_rebuildMarker = new ProfilerMarker("ZRList.RebuildViewport");
        private static readonly ProfilerMarker s_prefabMarker = new ProfilerMarker("ZRList.SelectPrefab");
        private static readonly ProfilerMarker s_bindMarker = new ProfilerMarker("ZRList.Bind");
        private static readonly ProfilerMarker s_sizeQueryMarker = new ProfilerMarker("ZRList.QuerySize");
        private static readonly ProfilerMarker s_measureMarker = new ProfilerMarker("ZRList.Measure");
        private bool m_hasRenderedRange;
        private int m_renderedFirstIndex;
        private int m_renderedLastIndex;
        private long m_renderedSizeVersion;
        private bool m_hasResolvedEnd;
        private int m_resolvedEndFirstIndex;
        private float m_resolvedEndOffset;
        private long m_resolvedEndSizeVersion;

        private bool HasPendingRenderWork
        {
            get
            {
                return m_refreshAll || m_deferredLayoutRefresh || m_dirtyItems.Count > 0 || m_pendingSizes.Count > 0 || m_committingSizes.Count > 0;
            }
        }

        private void InvalidateRenderCache()
        {
            m_hasRenderedRange = false;
            m_hasResolvedEnd = false;
        }

        private void BindHolder(ScrollItemView holder, int viewIndex)
        {
            UnbindHolder(holder);
            holder.OwnerAdapter = m_adapter;
            holder.ItemIndex = GetDataIndex(viewIndex);
            holder.ViewIndex = viewIndex;
            holder.BindingVersion = ++m_bindingVersion;
            ScrollItemLayoutContext context = GetItemLayoutContext(holder.SourcePrefab);
            if (holder.Root.rect.size[m_crossAxisIndex] != context.ItemCrossSize) {
                holder.Root.SetSizeWithCurrentAnchors((RectTransform.Axis)m_crossAxisIndex, context.ItemCrossSize);
            }

            m_dirtyItems.Remove(viewIndex);
            holder.IsBound = true;
            try {
                using (s_bindMarker.Auto()) {
                    m_adapter.Bind(holder, holder.ItemIndex, context);
                }
            }
            catch {
                UnbindHolder(holder);
                m_dirtyItems.Add(viewIndex);
                throw;
            }
        }

        private void UnbindHolder(ScrollItemView holder)
        {
            if (!holder.IsBound) {
                return;
            }

            holder.IsBound = false;
            holder.BindingVersion = ++m_bindingVersion;
            if (holder.OwnerAdapter is IScrollItemViewLifecycle lifecycle) {
                ++m_lifecycleCallbackDepth;
                try {
                    lifecycle.OnViewUnbound(holder, holder.ItemIndex);
                }
                finally {
                    --m_lifecycleCallbackDepth;
                }
            }
        }

        private float ResolveSize(ScrollItemView holder, int viewIndex)
        {
            // A size submitted by this binding/measurement wins for this update only.
            if (ApplyPendingSize(viewIndex)) {
                return m_sizeIndex.GetSize(viewIndex);
            }

            ScrollItemLayoutContext context = GetItemLayoutContext(holder.SourcePrefab);
            if (TryGetAdapterSize(viewIndex, context, out float supplied)) {
                StoreSize(viewIndex, supplied);
                return supplied;
            }

            float measured;
            using (s_measureMarker.Auto()) {
                measured = m_adapter.MeasureItem(holder, GetDataIndex(viewIndex), context);
            }
            // A measurement notification for this item is part of this transaction.
            if (ApplyPendingSize(viewIndex)) {
                return m_sizeIndex.GetSize(viewIndex);
            }

            StoreSize(viewIndex, measured);
            return measured;
        }

        internal ScrollItemView ExtractRecyclableViewsHolderOrCreateNew(int viewIndex)
        {
            return ExtractRecyclableViewsHolderOrCreateNew(GetItemPrefab(viewIndex));
        }

        private RectTransform GetItemPrefab(int viewIndex)
        {
            using var profileScope = s_prefabMarker.Auto();
            RectTransform prefab = m_adapter is IScrollItemPrefabProvider provider ? provider.GetItemPrefab(GetDataIndex(viewIndex)) : FirstItemPrefab;
            if (prefab == null) {
                throw new InvalidOperationException("The item prefab provider returned a null prefab.");
            }

            return prefab;
        }

        private bool TryGetAdapterSize(int viewIndex, ScrollItemLayoutContext context, out float size)
        {
            using var profileScope = s_sizeQueryMarker.Auto();
            return m_adapter.TryGetItemSize(GetDataIndex(viewIndex), context, out size);
        }

        private ScrollItemLayoutContext GetItemLayoutContext(RectTransform prefab)
        {
            if (StretchItems || prefab == null) {
                return m_layoutContext;
            }

            return new ScrollItemLayoutContext(m_layoutContext.Orientation, m_layoutContext.AvailableCrossSize, Mathf.Max(1f, prefab.rect.size[m_crossAxisIndex]), m_layoutContext.Revision);
        }

        protected ScrollItemView CreateViewsHolder(int viewIndex)
        {
            return CreateViewsHolder(GetItemPrefab(viewIndex));
        }

        private ScrollItemView CreateViewsHolder(RectTransform prefab)
        {
            if (m_poolRoot == null) {
                var container = new GameObject("VirtualScrollView pool", typeof(RectTransform));
                container.SetActive(false);
                m_poolRoot = (RectTransform)container.transform;
                m_poolRoot.SetParent(transform, false);
            }

            var root = (RectTransform)Instantiate(prefab.gameObject, m_poolRoot, false).transform;
            root.gameObject.SetActive(false);
            root.SetParent(Content, false);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            try {
                ScrollItemView holder = m_adapter.CreateViewsHolder(root);
                if (holder == null || holder.Root != root) {
                    throw new InvalidOperationException("The adapter must return a holder for the supplied item root.");
                }

                holder.OwnerAdapter = m_adapter;
                holder.SourcePrefab = prefab;
                return holder;
            }
            catch {
                ReleaseRoot(root);
                throw;
            }
        }

        public void SetViewsHolderEnabled(ScrollItemView holder)
        {
            if (!holder.Root.gameObject.activeSelf) {
                holder.Root.gameObject.SetActive(true);
            }
        }

        private void Recycle(ScrollItemView holder)
        {
            try {
                UnbindHolder(holder);
            }
            finally {
                holder.Root.gameObject.SetActive(false);
                holder.ItemIndex = holder.ViewIndex = -1;
                holder.BindingVersion = ++m_bindingVersion;
                AddPooledHolder(holder);
            }
        }

        private void RecycleAllVisible()
        {
            InvalidateRenderCache();
            Exception firstError = null;
            foreach (ScrollItemView holder in VisibleItemViews) {
                try {
                    Recycle(holder);
                }
                catch (Exception error) {
                    if (firstError == null) {
                        firstError = error;
                    }
                }
            }

            VisibleItemViews.Clear();
            VisibleItemsByViewIndex.Clear();
            m_visibleItemsByRoot.Clear();
            if (firstError != null) {
                throw firstError;
            }
        }

        private void SetOffset(float offset)
        {
            Vector2 pos = Content.anchoredPosition;
            float next = offset * -ScrollAxisSign;
            if (next != pos[ScrollAxisIndex]) {
                ScrollRect.CompensateContentPosition(ScrollAxisIndex, next - pos[ScrollAxisIndex]);
            }

            pos[ScrollAxisIndex] = next;
            if (Content.anchoredPosition != pos) {
                Content.anchoredPosition = pos;
            }

            m_previousContentPosition = next;
        }

        private void RenderAtOffset(float offset, bool forceBind = false)
        {
            if (!IsRendererReady) {
                CancelJump();
                RecycleAllVisible();
                SetOffset(0f);
                return;
            }

            if (m_sizeIndex.Count == 0) {
                RecycleAllVisible();
                SetOffset(0f);
                return;
            }

            if (forceBind || HasPendingRenderWork) {
                m_hasResolvedEnd = false;
            }

            if (!IsAnimatingJump && offset >= MaxOffset && MaxOffset > 0f) {
                // Preserve elastic overscroll while discovering the actual end sizes.
                float overscroll = offset - MaxOffset;
                if (!m_hasResolvedEnd || m_resolvedEndSizeVersion != m_sizeIndex.Version) {
                    InvalidateRenderCache();
                    m_resolvedEndOffset = GetJumpPosition(m_sizeIndex.Count - 1, out m_resolvedEndFirstIndex, out _);
                    m_resolvedEndSizeVersion = m_sizeIndex.Version;
                }

                RenderRangeIfChanged(m_resolvedEndFirstIndex, m_resolvedEndOffset + overscroll, forceBind);
                m_hasResolvedEnd = m_resolvedEndSizeVersion == m_sizeIndex.Version && !HasPendingRenderWork;
                return;
            }

            int index;
            using (s_rangeLookupMarker.Auto()) {
                index = m_sizeIndex.FindIndex(offset - StartPadding);
                // The prefix index includes spacing. An item whose end is above the
                // viewport need not be bound merely because the viewport starts in its gap.
                if (index + 1 < m_sizeIndex.Count && GetEstimatedItemStart(index) + m_sizeIndex.GetSize(index) <= offset) {
                    ++index;
                }
            }

            RenderRangeIfChanged(index, offset, forceBind);
        }

        private void RenderRangeIfChanged(int firstIndex, float offset, bool forceBind)
        {
            if (!forceBind && !HasPendingRenderWork && m_hasRenderedRange && m_renderedSizeVersion == m_sizeIndex.Version) {
                int lastIndex = FindLastRenderedIndex(firstIndex, offset);
                if (firstIndex == m_renderedFirstIndex && lastIndex == m_renderedLastIndex) {
                    SetOffset(offset);
                    return;
                }

                if (TryUpdateVisibleRange(firstIndex, lastIndex, offset)) {
                    return;
                }
            }

            RenderViewport(firstIndex, offset, forceBind);
        }

        private int FindLastRenderedIndex(int firstIndex, float offset)
        {
            using var profileScope = s_rangeLookupMarker.Auto();
            // The prefix lookup includes trailing spacing. Only a start strictly
            // before the viewport end enters; always retain the first item.
            double end = (double)offset + m_viewportLength;
            int lastIndex = m_sizeIndex.FindIndex(end - StartPadding);
            if (StartPadding + m_sizeIndex.PrefixSize(lastIndex) >= end) {
                --lastIndex;
            }
            return Math.Max(firstIndex, lastIndex);
        }

        // The same geometry and retention algorithm serves both axes, scrolling and jumping.
        private void RenderViewport(int firstIndex, float offset, bool forceBind = false)
        {
            using var profileScope = s_rebuildMarker.Auto();
            m_hasRenderedRange = false;
            m_rebuilding = true;
            try {
                m_previousVisible.Clear();
                foreach (ScrollItemView holder in VisibleItemViews) {
                    m_previousVisible[holder.ViewIndex] = holder;
                }

                // Transfer ownership before invoking business lifecycle callbacks. If
                // one throws, the finalizer must recycle every old view exactly once.
                VisibleItemViews.Clear();
                VisibleItemsByViewIndex.Clear();
                m_visibleItemsByRoot.Clear();
                int estimatedEnd = m_sizeIndex.FindIndex(offset + m_viewportLength - StartPadding) + 1;
                m_recycleIndices.Clear();
                foreach (KeyValuePair<int, ScrollItemView> pair in m_previousVisible) {
                    if (pair.Key < firstIndex || pair.Key > estimatedEnd) {
                        m_recycleIndices.Add(pair.Key);
                    }
                }

                foreach (int index in m_recycleIndices) {
                    ScrollItemView holder = m_previousVisible[index];
                    m_previousVisible.Remove(index);
                    Recycle(holder);
                }

                double start = StartPadding + m_sizeIndex.PrefixSize(firstIndex);
                for (int index = firstIndex; index < m_sizeIndex.Count; ++index) {
                    if (index > firstIndex && start - offset >= m_viewportLength) {
                        break;
                    }

                    bool retained = m_previousVisible.TryGetValue(index, out ScrollItemView holder);
                    if (retained) {
                        RectTransform prefab = GetItemPrefab(index);
                        // Keep ownership until selection succeeds, so a throwing
                        // provider cannot strand a previously visible holder.
                        m_previousVisible.Remove(index);
                        if (holder.SourcePrefab != prefab) {
                            Recycle(holder);
                            holder = ExtractRecyclableViewsHolderOrCreateNew(prefab);
                            retained = false;
                        }
                    }
                    else {
                        holder = ExtractRecyclableViewsHolderOrCreateNew(GetItemPrefab(index));
                    }

                    VisibleItemViews.Add(holder);
                    VisibleItemsByViewIndex[index] = holder;
                    m_visibleItemsByRoot[holder.Root.gameObject] = holder;
                    bool needsBind = !retained || forceBind || m_dirtyItems.Contains(index);
                    if (needsBind) {
                        BindHolder(holder, index);
                    }

                    SetViewsHolderEnabled(holder);
                    float size = needsBind || m_pendingSizes.ContainsKey(index) ? ResolveSize(holder, index) : m_sizeIndex.GetSize(index);
                    LayoutHolder(holder, size, start);

                    start += (double)size + ContentSpacing;
                }

                SetOffset(offset);
            }
            finally {
                try {
                    Exception firstError = null;
                    foreach (ScrollItemView holder in m_previousVisible.Values) {
                        try {
                            Recycle(holder);
                        }
                        catch (Exception error) {
                            if (firstError == null) {
                                firstError = error;
                            }
                        }
                    }

                    m_previousVisible.Clear();
                    if (firstError != null) {
                        throw firstError;
                    }
                }
                finally {
                    m_rebuilding = false;
                }
            }

            RememberRenderedRange();
        }

        private void RememberRenderedRange()
        {
            // Publish only after binding, positioning and final recycling all
            // succeed. Measurements can change the range calculated beforehand.
            if (VisibleItemViews.Count > 0) {
                m_renderedFirstIndex = VisibleItemViews[0].ViewIndex;
                m_renderedLastIndex = VisibleItemViews[VisibleItemViews.Count - 1].ViewIndex;
                m_renderedSizeVersion = m_sizeIndex.Version;
                m_hasRenderedRange = true;
            }
        }

        private void LayoutHolder(ScrollItemView holder, float size, double start)
        {
            holder.ItemSize = size;
            RectTransform root = holder.Root;
            Rect rect = root.rect;
            if (rect.size[ScrollAxisIndex] != size) {
                root.SetSizeWithCurrentAnchors((RectTransform.Axis)ScrollAxisIndex, size);
                rect = root.rect;
            }

            // Read geometry after binding/measurement and any size write. Reuse
            // this snapshot only within this placement, never across callbacks.
            Vector2 previous = root.anchoredPosition;
            Vector2 position = previous;
            position[m_crossAxisIndex] = LayoutIsHorizontal ? -CrossAxisStartPadding - rect.yMax : CrossAxisStartPadding - rect.xMin;
            position[ScrollAxisIndex] = LayoutIsHorizontal ? (float)(start - rect.xMin) : (float)(-start - rect.yMax);
            if (previous != position) {
                root.anchoredPosition = position;
            }
        }

        private float GetJumpSize(int viewIndex, ref ScrollItemView measuringHolder)
        {
            if (ApplyPendingSize(viewIndex)) {
                return m_sizeIndex.GetSize(viewIndex);
            }

            RectTransform prefab = GetItemPrefab(viewIndex);
            if (TryGetAdapterSize(viewIndex, GetItemLayoutContext(prefab), out float size)) {
                StoreSize(viewIndex, size);
                return size;
            }

            if (measuringHolder != null && measuringHolder.SourcePrefab != prefab) {
                Recycle(measuringHolder);
                measuringHolder = null;
            }

            if (measuringHolder == null) {
                measuringHolder = ExtractRecyclableViewsHolderOrCreateNew(prefab);
            }

            BindHolder(measuringHolder, viewIndex);
            SetViewsHolderEnabled(measuringHolder);
            return ResolveSize(measuringHolder, viewIndex);
        }

        private float GetJumpPosition(int index, out int firstIndex, out bool atEnd)
        {
            ScrollItemView measuringHolder = null;
            bool wasRebuilding = m_rebuilding;
            m_rebuilding = true;
            firstIndex = index;
            atEnd = false;
            try {
                float visibleSize = index == 0 ? StartPadding : m_halfContentSpacing;
                int next = index;
                do {
                    visibleSize += GetJumpSize(next++, ref measuringHolder) + ContentSpacing;
                }
                while (next < m_sizeIndex.Count && visibleSize < m_viewportLength);
                if (next == m_sizeIndex.Count && visibleSize - ContentSpacing + EndPadding < m_viewportLength) {
                    float suffixSize = (float)EndPadding;
                    firstIndex = m_sizeIndex.Count;
                    while (firstIndex > 0 && suffixSize < m_viewportLength) {
                        --firstIndex;
                        suffixSize += GetJumpSize(firstIndex, ref measuringHolder);
                        if (firstIndex < m_sizeIndex.Count - 1) {
                            suffixSize += ContentSpacing;
                        }
                    }

                    atEnd = true;
                    return MaxOffset;
                }

                float inset = index == 0 ? StartPadding : m_halfContentSpacing;
                return Mathf.Clamp(GetEstimatedItemStart(index) - inset, 0f, MaxOffset);
            }
            finally {
                try {
                    if (measuringHolder != null) {
                        Recycle(measuringHolder);
                    }
                }
                finally {
                    m_rebuilding = wasRebuilding;
                }
            }
        }
    }
}
