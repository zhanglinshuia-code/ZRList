// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    public partial class VirtualScrollView
    {
        private void ResetDataState(int itemCount)
        {
            m_pendingSizes.Clear();
            m_committingSizes.Clear();
            m_dirtyItems.Clear();
            m_updateDepth = 0;
            ++m_updateGeneration;
            m_refreshAll = false;
            m_deferredLayoutRefresh = false;
            ItemsCount = itemCount;
        }

        /// <summary>Append new tail items after extending business data; existing items and sizes stay valid.</summary>
        public void AppendItems(int addedCount)
        {
            if (!m_initialized) {
                throw new InvalidOperationException("Initialize before appending data.");
            }

            if (addedCount < 0 || addedCount > int.MaxValue - 1 - ItemCount) {
                throw new ArgumentOutOfRangeException(nameof(addedCount));
            }

            if (m_updateDepth > 0 || m_rebuilding || m_flushing || m_preparingLayout || m_lifecycleCallbackDepth > 0) {
                throw new InvalidOperationException("Append data outside item callbacks and update batches.");
            }

            EnsureLayoutCurrent();
            if (FirstDataIndex != 0) {
                throw new InvalidOperationException("Appending requires FirstDataIndex to be zero so existing data indices remain stable.");
            }

            if (addedCount == 0) {
                return;
            }

            FlushPendingUpdates();
            CancelJump();
            try {
                m_rebuilding = true;
                try {
                    m_sizeIndex.Append(addedCount);
                    ItemsCount = ItemCount;
                    UpdateContentSize();
                }
                finally {
                    m_rebuilding = false;
                }

                if (isActiveAndEnabled) {
                    RenderAtOffset(Offset);
                    FlushPendingUpdates();
                }

                NotifyViewStateChanged();
            }
            catch {
                Dispose();
                throw;
            }
        }

        private void ResetSizes()
        {
            float estimatedItemSize = m_adapter.EstimatedItemSize;
            if (!IsValidSize(estimatedItemSize)) {
                throw new InvalidOperationException("Estimated size must be finite and positive.");
            }

            m_sizeIndex.Reset(ItemsCount, estimatedItemSize, ContentSpacing);
            UpdateContentSize();
        }

        private void UpdateContentSize()
        {
            double total = StartPadding + m_sizeIndex.TotalSize + EndPadding;
            float length = Mathf.Max(m_viewportLength, (float)total);
            if (Content.rect.size[ScrollAxisIndex] != length) {
                Content.SetSizeWithCurrentAnchors((RectTransform.Axis)ScrollAxisIndex, length);
            }

            if (IsAnimatingJump) {
                UpdateEstimatedJumpTarget();
            }
        }

        private static bool IsValidSize(float size)
        {
            return size > 0f && !float.IsNaN(size) && !float.IsInfinity(size);
        }

        private bool IsValidDataIndex(int index)
        {
            return m_initialized && index >= 0 && index < m_sizeIndex.Count;
        }

        private int GetDataIndex(int viewIndex)
        {
            return (int)((((long)m_layoutDataOffset + viewIndex) % m_sizeIndex.Count + m_sizeIndex.Count) % m_sizeIndex.Count);
        }

        private int GetViewIndex(int dataIndex)
        {
            return (int)((((long)dataIndex - m_layoutDataOffset) % m_sizeIndex.Count + m_sizeIndex.Count) % m_sizeIndex.Count);
        }

        private float GetEstimatedItemStart(int index)
        {
            return (float)(StartPadding + m_sizeIndex.PrefixSize(index));
        }

        private void StoreSize(int viewIndex, float size, bool updateContent = true)
        {
            if (!IsValidSize(size)) {
                throw new InvalidOperationException("The item adapter returned a non-positive or non-finite size.");
            }

            bool changed = m_sizeIndex.GetSize(viewIndex) != size;
            m_sizeIndex.SetSize(viewIndex, size);

            if (updateContent && changed) {
                UpdateContentSize();
            }
        }

        /// <summary>Size is along the scroll axis, excluding spacing and padding. Index is a data index.</summary>
        public bool SetItemSize(int dataIndex, float newSize)
        {
            EnsureLayoutCurrent();
            if (!IsValidDataIndex(dataIndex) || !IsValidSize(newSize)) {
                return false;
            }

            int viewIndex = GetViewIndex(dataIndex);
            if (!m_rebuilding && !m_dirtyItems.Contains(viewIndex) && !m_refreshAll && m_sizeIndex.GetSize(viewIndex) == newSize && !m_pendingSizes.ContainsKey(viewIndex)) {
                return true;
            }

            m_pendingSizes[viewIndex] = new SizeChange(newSize);
            FlushPendingUpdates();
            return true;
        }

        /// <summary>Read the current layout length, including the estimate for items not yet measured.</summary>
        public bool TryGetItemSize(int dataIndex, out float size)
        {
            EnsureLayoutCurrent();
            size = 0f;
            if (!IsValidDataIndex(dataIndex)) {
                return false;
            }

            size = m_sizeIndex.GetSize(GetViewIndex(dataIndex));
            return true;
        }

        public bool TryGetItemBinding(GameObject item, out ScrollItemBinding binding)
        {
            EnsureLayoutCurrent();
            if (TryGetBoundItem(item, out ScrollItemView holder) && !m_dirtyItems.Contains(holder.ViewIndex) && !m_refreshAll) {
                binding = new ScrollItemBinding(holder, m_layoutRevision);
                return true;
            }

            binding = default;
            return false;
        }

        /// <summary>Use this overload for asynchronous results from a recycled item view.</summary>
        public bool SetItemSize(ScrollItemBinding binding, float newSize)
        {
            EnsureLayoutCurrent();
            if (!IsCurrentBinding(binding) || !IsValidSize(newSize)) {
                return false;
            }

            if (!m_rebuilding && m_sizeIndex.GetSize(binding.ViewIndex) == newSize && !m_pendingSizes.ContainsKey(binding.ViewIndex)) {
                return true;
            }

            m_pendingSizes[binding.ViewIndex] = new SizeChange(newSize, binding);
            FlushPendingUpdates();
            return true;
        }

        private bool IsCurrentBinding(ScrollItemBinding binding)
        {
            return m_initialized && isActiveAndEnabled && !m_refreshAll && !m_dirtyItems.Contains(binding.ViewIndex) && binding.LayoutRevision == m_layoutRevision && VisibleItemsByViewIndex.TryGetValue(binding.ViewIndex, out ScrollItemView holder) && holder.IsBound && holder.Root == binding.Root && holder.BindingVersion == binding.Version && holder.ItemIndex == binding.DataIndex;
        }

        public void BeginUpdate()
        {
            EnsureLayoutCurrent();
            if (!m_initialized) {
                throw new InvalidOperationException("Initialize before starting an update batch.");
            }

            ++m_updateDepth;
        }

        /// <summary>Allocates one idempotent scope. Use BeginUpdate/EndUpdate with try/finally in allocation-sensitive loops.</summary>
        public IDisposable BeginUpdateScope()
        {
            BeginUpdate();
            return new UpdateScope(this);
        }

        private sealed class UpdateScope: IDisposable
        {
            private VirtualScrollView m_owner;
            private readonly long m_generation;
            public UpdateScope(VirtualScrollView owner)
            {
                m_owner = owner;
                m_generation = owner.m_updateGeneration;
            }

            public void Dispose()
            {
                if (m_owner == null) {
                    return;
                }

                VirtualScrollView current = m_owner;
                m_owner = null;
                if (current.m_initialized && current.m_updateGeneration == m_generation) {
                    current.EndUpdate();
                }
            }
        }

        public void EndUpdate()
        {
            if (m_updateDepth == 0) {
                throw new InvalidOperationException("EndUpdate requires a matching BeginUpdate.");
            }

            --m_updateDepth;
            FlushPendingUpdates();
            if (m_updateDepth == 0 && m_deferredLayoutRefresh) {
                RefreshLayout();
            }

            RunQueuedJump();
            NotifyViewStateChanged();
        }

        /// <summary>Rebinds and obtains a fresh size for one rendered data item on the next update.</summary>
        public void RefreshItem(int dataIndex)
        {
            EnsureLayoutCurrent();
            if (!IsValidDataIndex(dataIndex)) {
                return;
            }

            int viewIndex = GetViewIndex(dataIndex);
            m_pendingSizes.Remove(viewIndex);
            m_committingSizes.Remove(viewIndex);
            m_dirtyItems.Add(viewIndex);
            FlushPendingUpdates();
        }

        private struct ViewAnchor
        {
            public int Index;
            public float Inset;
            public bool Valid;
        }

        private ViewAnchor CaptureAnchor()
        {
            foreach (ScrollItemView holder in VisibleItemViews) {
                float leading = LayoutIsHorizontal ? holder.Root.rect.xMin : holder.Root.rect.yMax;
                float inset = (Content.anchoredPosition[ScrollAxisIndex] + holder.Root.anchoredPosition[ScrollAxisIndex] + leading) * ScrollAxisSign;
                if (inset + holder.ItemSize > 0f && inset < m_viewportLength) {
                    return new ViewAnchor
                    {
                        Index = holder.ViewIndex,
                        Inset = inset,
                        Valid = true
                    };
                }
            }

            return default;
        }

        public void FlushPendingUpdates()
        {
            EnsureLayoutCurrent();
            if (!m_initialized || !isActiveAndEnabled || m_rebuilding || m_flushing || m_updateDepth > 0) {
                return;
            }

            m_flushing = true;
            try {
                // Business callbacks may enqueue another batch while views are bound.
                // Bound the synchronous work; remaining notifications run next LateUpdate.
                for (int pass = 0; pass < 4 && (m_pendingSizes.Count > 0 || m_dirtyItems.Count > 0 || m_refreshAll); ++pass) {
                    ViewAnchor anchor = CaptureAnchor();
                    float oldOffset = Offset;
                    // Keep this transaction's submissions available to newly bound views,
                    // without retaining an override across later refreshes or recycling.
                    Dictionary<int, SizeChange> pending = m_pendingSizes;
                    m_pendingSizes = m_committingSizes;
                    m_committingSizes = pending;
                    foreach (KeyValuePair<int, SizeChange> pair in m_committingSizes) {
                        if (pair.Value.Binding.HasValue && !IsCurrentBinding(pair.Value.Binding.Value)) {
                            continue;
                        }

                        StoreSize(pair.Key, pair.Value.Size, false);
                    }

                    if (m_committingSizes.Count > 0) {
                        UpdateContentSize();
                    }

                    float offset = anchor.Valid ? GetEstimatedItemStart(anchor.Index) - anchor.Inset : oldOffset;
                    bool forceBind = m_refreshAll;
                    m_refreshAll = false;
                    // Off-screen dirty items will be rebound when they enter the viewport.
                    RenderAtOffset(ConstrainUpdatedOffset(offset), forceBind);
                    if (IsAnimatingJump) {
                        ViewAnchor currentAnchor = CaptureAnchor();
                        JumpTargetOffset = GetJumpPosition(m_activeJumpIndex, out _, out m_jumpAtEnd);
                        if (currentAnchor.Valid) {
                            RenderAtOffset(Mathf.Clamp(GetEstimatedItemStart(currentAnchor.Index) - currentAnchor.Inset, 0f, MaxOffset));
                        }
                    }

                    m_recycleIndices.Clear();
                    foreach (int index in m_dirtyItems) {
                        if (!VisibleItemsByViewIndex.ContainsKey(index)) {
                            m_recycleIndices.Add(index);
                        }
                    }

                    foreach (int index in m_recycleIndices) {
                        m_dirtyItems.Remove(index);
                    }

                    m_committingSizes.Clear();
                }
            }
            finally {
                m_committingSizes.Clear();
                m_flushing = false;
            }

            RunQueuedJump();
            NotifyViewStateChanged();
        }

        private float ConstrainUpdatedOffset(float offset)
        {
            if (ScrollRect.movementType == UnityEngine.UI.ScrollRect.MovementType.Unrestricted || (ScrollRect.movementType == UnityEngine.UI.ScrollRect.MovementType.Elastic && ScrollRect.IsDragging)) {
                return offset;
            }

            return Mathf.Clamp(offset, 0f, MaxOffset);
        }

        private bool ApplyPendingSize(int viewIndex)
        {
            if (!m_pendingSizes.TryGetValue(viewIndex, out SizeChange value)) {
                if (!m_committingSizes.TryGetValue(viewIndex, out value)) {
                    return false;
                }
            }

            m_pendingSizes.Remove(viewIndex);
            m_committingSizes.Remove(viewIndex);
            if (value.Binding.HasValue && !IsCurrentBinding(value.Binding.Value)) {
                return false;
            }

            StoreSize(viewIndex, value.Size);
            return true;
        }
    }
}
