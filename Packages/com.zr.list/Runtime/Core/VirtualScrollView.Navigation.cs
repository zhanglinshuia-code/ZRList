// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;

namespace ZRList
{
    public partial class VirtualScrollView
    {
        /// <summary>Navigates by business data index, including rotated display ordering.</summary>
        public bool JumpToDataItem(int dataIndex, float duration = 0f)
        {
            EnsureLayoutCurrent();
            if (!IsRendererReady || !IsValidDataIndex(dataIndex) || !isActiveAndEnabled || float.IsNaN(duration) || float.IsInfinity(duration)) {
                return false;
            }

            if (m_updateDepth > 0 || m_rebuilding || m_flushing || m_preparingLayout) {
                m_queuedJumpIndex = dataIndex;
                m_queuedJumpDuration = duration;
                m_queuedJumpIsDataIndex = true;
                return true;
            }

            return JumpToItem(GetViewIndex(dataIndex), duration);
        }

        /// <summary>Compatibility navigation API: index is in the displayed ordering.</summary>
        public bool JumpToItem(int index, float duration = 0f)
        {
            EnsureLayoutCurrent();
            if (!IsRendererReady || !m_initialized || !isActiveAndEnabled || index < 0 || index >= m_sizeIndex.Count || float.IsNaN(duration) || float.IsInfinity(duration)) {
                return false;
            }

            if (m_updateDepth > 0 || m_rebuilding || m_flushing || m_preparingLayout) {
                m_queuedJumpIndex = index;
                m_queuedJumpDuration = duration;
                m_queuedJumpIsDataIndex = false;
                return true;
            }

            FlushPendingUpdates();
            CancelActiveJump();
            m_activeJumpIndex = JumpViewIndex = index;
            JumpElapsedTime = 0f;
            m_inertiaBeforeJump = ScrollRect.inertia;
            m_jumpScrollRect = ScrollRect;
            m_decelerationRate = ScrollRect.decelerationRate;
            ScrollRect.inertia = false;
            ScrollRect.StopMovement();
            m_jumpDuration = Mathf.Max(0f, duration);
            IsAnimatingJump = true;
            try {
                JumpTargetOffset = GetJumpPosition(index, out _, out m_jumpAtEnd);
                if (m_jumpDuration == 0f) {
                    CompleteJump();
                }
                else {
                    FlushPendingUpdates();
                }

                return true;
            }
            catch {
                CancelJump();
                throw;
            }
        }

        private void UpdateEstimatedJumpTarget()
        {
            if (m_activeJumpIndex < 0 || m_activeJumpIndex >= m_sizeIndex.Count) {
                return;
            }

            float inset = m_activeJumpIndex == 0 ? StartPadding : m_halfContentSpacing;
            JumpTargetOffset = m_jumpAtEnd ? MaxOffset : Mathf.Clamp(GetEstimatedItemStart(m_activeJumpIndex) - inset, 0f, MaxOffset);
        }

        public void CancelJump()
        {
            m_queuedJumpIndex = -1;
            CancelActiveJump();
        }

        private void CancelActiveJump()
        {
            if (!IsAnimatingJump) {
                return;
            }

            IsAnimatingJump = false;
            m_activeJumpIndex = -1;
            if (m_jumpScrollRect != null) {
                m_jumpScrollRect.inertia = m_inertiaBeforeJump;
                m_jumpScrollRect.decelerationRate = m_decelerationRate;
                m_jumpScrollRect.StopMovement();
            }

            m_jumpScrollRect = null;
        }

        private void RunQueuedJump()
        {
            if (m_runningQueuedJump || m_queuedJumpIndex < 0 || !m_initialized || !isActiveAndEnabled || m_updateDepth > 0 || m_rebuilding || m_flushing || m_preparingLayout || m_deferredLayoutRefresh) {
                return;
            }

            int index = m_queuedJumpIndex;
            float duration = m_queuedJumpDuration;
            bool isDataIndex = m_queuedJumpIsDataIndex;
            m_queuedJumpIndex = -1;
            m_runningQueuedJump = true;
            try {
                if (isDataIndex) {
                    JumpToDataItem(index, duration);
                }
                else {
                    JumpToItem(index, duration);
                }
            }
            finally {
                m_runningQueuedJump = false;
            }
        }

        private void CompleteJump()
        {
            float offset = GetJumpPosition(m_activeJumpIndex, out int first, out m_jumpAtEnd);
            RenderViewport(first, offset);
            CancelActiveJump();
            JumpElapsedTime = JumpTargetOffset = 0f;
            FlushPendingUpdates();
        }

        private void AdvanceJump(float deltaTime)
        {
            if (m_updateDepth > 0) {
                return;
            }

            if (!IsAnimatingJump) {
                return;
            }

            if (ScrollRect.IsDragging) {
                CancelJump();
                return;
            }

            float remaining = m_jumpDuration - JumpElapsedTime;
            if (remaining <= deltaTime) {
                CompleteJump();
                return;
            }

            float offset = Mathf.Lerp(Offset, JumpTargetOffset, Mathf.Clamp01(deltaTime / remaining));
            JumpElapsedTime += deltaTime;
            RenderAtOffset(offset);
            FlushPendingUpdates();
        }
    }
}
