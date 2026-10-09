// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;

namespace ZRList
{
    public partial class VirtualScrollView
    {
        /// <summary>Raised after changes to bindings, visible views, pool size or layout revision settle.</summary>
        public event Action ViewStateChanged;

        private bool m_hasNotifiedViewState;
        private bool m_notifyingViewState;
        private bool m_notifiedInitialized;
        private long m_notifiedBindingVersion;
        private int m_notifiedLayoutRevision;
        private int m_notifiedItemCount;
        private int m_notifiedVisibleCount;
        private int m_notifiedPooledCount;

        private void NotifyViewStateChanged()
        {
            if (m_notifyingViewState || m_applyingData || m_rebuilding || m_flushing || m_preparingLayout || m_runningQueuedJump || m_deferredLayoutRefresh || m_updateDepth > 0 || m_lifecycleCallbackDepth > 0) {
                return;
            }

            if (m_hasNotifiedViewState && m_notifiedInitialized == m_initialized && m_notifiedBindingVersion == m_bindingVersion && m_notifiedLayoutRevision == m_layoutRevision && m_notifiedItemCount == ItemCount && m_notifiedVisibleCount == VisibleItemViews.Count && m_notifiedPooledCount == PooledViewCount) {
                return;
            }

            m_hasNotifiedViewState = true;
            m_notifiedInitialized = m_initialized;
            m_notifiedBindingVersion = m_bindingVersion;
            m_notifiedLayoutRevision = m_layoutRevision;
            m_notifiedItemCount = ItemCount;
            m_notifiedVisibleCount = VisibleItemViews.Count;
            m_notifiedPooledCount = PooledViewCount;
            m_notifyingViewState = true;
            try {
                ViewStateChanged?.Invoke();
            }
            finally {
                m_notifyingViewState = false;
            }
        }
    }
}
