// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    public partial class VirtualScrollView
    {
        private CallbackScrollItemAdapter m_callbackAdapter;
        private Action<ScrollItemView, int> m_onItemRender;
        private Action<ScrollItemView, int> m_onItemRecycle;
        private bool m_usesConfiguredPrefabs;

        private bool ConfiguredPrefabsChanged
        {
            get
            {
                return m_usesConfiguredPrefabs && ReferenceEquals(m_adapter, m_callbackAdapter) && !ReferenceEquals(m_callbackAdapter.Prefabs, ItemPrefabs);
            }
        }

        private void RefreshCallbackPrefab()
        {
            if (m_callbackAdapter == null || !ReferenceEquals(m_adapter, m_callbackAdapter)) {
                return;
            }

            if (m_usesConfiguredPrefabs) {
                ScrollItemPrefabCollection.Validate(ItemPrefabs, m_callbackAdapter.PrefabIndexSelector);
                m_callbackAdapter.Prefabs = ItemPrefabs;
            }
            IReadOnlyList<RectTransform> prefabs = m_callbackAdapter.Prefabs;
            RectTransform estimatePrefab = prefabs != null && prefabs.Count > 0 ? prefabs[0] : null;
            float estimate = estimatePrefab != null ? estimatePrefab.rect.size[IsHorizontal ? 0 : 1] : 100f;
            m_callbackAdapter.EstimatedItemSize = IsValidSize(estimate) ? estimate : 100f;
        }

        private bool IsRendererReady
        {
            get
            {
                return m_adapter is not IScrollItemRenderState state || state.IsRendererReady;
            }
        }

        /// <summary>Fill an item when it is created, reused or explicitly refreshed.</summary>
        public event Action<ScrollItemView, int> OnItemRender
        {
            add
            {
                if (value == null) {
                    return;
                }

                m_onItemRender += value;
                if (m_callbackAdapter != null) {
                    m_callbackAdapter.Render = m_onItemRender;
                }

                if (m_initialized && ReferenceEquals(m_adapter, m_callbackAdapter)) {
                    BeginUpdate();
                    try {
                        m_refreshAll = true;
                    }
                    finally {
                        EndUpdate();
                    }
                }
            }

            remove
            {
                m_onItemRender -= value;
                if (m_callbackAdapter != null) {
                    m_callbackAdapter.Render = m_onItemRender;
                }
            }
        }

        /// <summary>Optional cleanup before an item is returned to the pool or rebound.</summary>
        public event Action<ScrollItemView, int> OnItemRecycle
        {
            add
            {
                m_onItemRecycle += value;
                if (m_callbackAdapter != null) {
                    m_callbackAdapter.Recycle = m_onItemRecycle;
                }
            }

            remove
            {
                m_onItemRecycle -= value;
                if (m_callbackAdapter != null) {
                    m_callbackAdapter.Recycle = m_onItemRecycle;
                }
            }
        }

        /// <summary>The business selector chooses an index in a prefab collection of any size.</summary>
        public void Initialize(int itemCount, IReadOnlyList<RectTransform> itemPrefabs, Func<int, int> prefabIndexSelector)
        {
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            InitializeCallbackPrefabs(itemCount, itemPrefabs, prefabIndexSelector, false);
        }

        public void Initialize(int itemCount, Func<int, RectTransform> prefabSelector = null)
        {
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            if (m_rebuilding || m_flushing || m_lifecycleCallbackDepth > 0) {
                throw new InvalidOperationException("Initialize outside item callbacks.");
            }

            if (prefabSelector == null) {
                InitializeWithPrefabIndex(itemCount);
                return;
            }

            m_callbackAdapter ??= new CallbackScrollItemAdapter();
            m_callbackAdapter.DefaultPrefab = null;
            m_callbackAdapter.PrefabSelector = prefabSelector;
            m_callbackAdapter.Prefabs = null;
            m_callbackAdapter.PrefabIndexSelector = null;
            m_usesConfiguredPrefabs = false;
            m_callbackAdapter.Render = m_onItemRender;
            m_callbackAdapter.Recycle = m_onItemRecycle;
            m_callbackAdapter.EstimatedItemSize = 100f;
            Initialize(m_callbackAdapter, itemCount);
        }

        /// <summary>Use the templates configured on this list; business code only selects an index.</summary>
        public void InitializeWithPrefabIndex(int itemCount, Func<int, int> prefabIndexSelector = null)
        {
            InitializeCallbackPrefabs(itemCount, ItemPrefabs, prefabIndexSelector, true);
        }

        private void InitializeCallbackPrefabs(int itemCount, IReadOnlyList<RectTransform> prefabs, Func<int, int> prefabIndexSelector, bool usesConfiguredPrefabs)
        {
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            if (m_rebuilding || m_flushing || m_lifecycleCallbackDepth > 0) {
                throw new InvalidOperationException("Initialize outside item callbacks.");
            }

            ScrollItemPrefabCollection.Validate(prefabs, prefabIndexSelector);
            m_callbackAdapter ??= new CallbackScrollItemAdapter();
            m_callbackAdapter.DefaultPrefab = null;
            m_callbackAdapter.PrefabSelector = null;
            m_callbackAdapter.Prefabs = prefabs;
            m_callbackAdapter.PrefabIndexSelector = prefabIndexSelector;
            m_callbackAdapter.Render = m_onItemRender;
            m_callbackAdapter.Recycle = m_onItemRecycle;
            m_usesConfiguredPrefabs = usesConfiguredPrefabs;
            RectTransform estimatePrefab = prefabs[0];
            float estimate = estimatePrefab.rect.size[IsHorizontal ? 0 : 1];
            m_callbackAdapter.EstimatedItemSize = IsValidSize(estimate) ? estimate : 100f;
            Initialize(m_callbackAdapter, itemCount);
        }
    }
}
