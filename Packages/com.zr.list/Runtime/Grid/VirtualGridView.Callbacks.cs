// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    public sealed partial class VirtualGridView
    {
        private CallbackScrollItemAdapter m_callbackAdapter;
        private Action<ScrollItemView, int> m_onItemRender;
        private Action<ScrollItemView, int> m_onItemRecycle;
        private bool m_usesConfiguredPrefabs;

        private bool IsRendererReady
        {
            get
            {
                return m_adapter is not CallbackScrollItemAdapter callbackAdapter || callbackAdapter.Render != null;
            }
        }

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

                if (IsInitialized && ReferenceEquals(m_adapter, m_callbackAdapter)) {
                    RowScrollView.RefreshVisibleItems();
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

        /// <summary>The business selector chooses an index in a cell prefab collection of any size.</summary>
        public void Initialize(int itemCount, IReadOnlyList<RectTransform> itemPrefabs, Func<int, int> prefabIndexSelector)
        {
            EnsureOutsideCallback();
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            InitializeCallbackPrefabs(itemCount, itemPrefabs, prefabIndexSelector, false);
        }

        public void Initialize(int itemCount, Func<int, RectTransform> prefabSelector = null)
        {
            EnsureOutsideCallback();
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            if (prefabSelector == null && CellPrefabs != null && CellPrefabs.Length > 0) {
                InitializeWithPrefabIndex(itemCount);
                return;
            }

            if (CellPrefab == null && prefabSelector == null) {
                throw new InvalidOperationException("Assign a cell prefab or supply a prefab selector.");
            }

            m_callbackAdapter ??= new CallbackScrollItemAdapter();
            m_callbackAdapter.DefaultPrefab = CellPrefab;
            m_callbackAdapter.PrefabSelector = prefabSelector;
            m_callbackAdapter.Prefabs = null;
            m_callbackAdapter.PrefabIndexSelector = null;
            m_usesConfiguredPrefabs = false;
            m_callbackAdapter.Render = m_onItemRender;
            m_callbackAdapter.Recycle = m_onItemRecycle;
            m_callbackAdapter.EstimatedItemSize = CellSize.y;
            Initialize(m_callbackAdapter, itemCount);
        }

        /// <summary>Use the templates configured on this grid; business code only selects an index.</summary>
        public void InitializeWithPrefabIndex(int itemCount, Func<int, int> prefabIndexSelector = null)
        {
            InitializeCallbackPrefabs(itemCount, CellPrefabs, prefabIndexSelector, true);
        }

        private void InitializeCallbackPrefabs(int itemCount, IReadOnlyList<RectTransform> prefabs, Func<int, int> prefabIndexSelector, bool usesConfiguredPrefabs)
        {
            EnsureOutsideCallback();
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            ScrollItemPrefabCollection.Validate(prefabs, prefabIndexSelector);
            m_callbackAdapter ??= new CallbackScrollItemAdapter();
            m_callbackAdapter.DefaultPrefab = CellPrefab;
            m_callbackAdapter.PrefabSelector = null;
            m_callbackAdapter.Prefabs = prefabs;
            m_callbackAdapter.PrefabIndexSelector = prefabIndexSelector;
            m_callbackAdapter.Render = m_onItemRender;
            m_callbackAdapter.Recycle = m_onItemRecycle;
            m_callbackAdapter.EstimatedItemSize = CellSize.y;
            m_usesConfiguredPrefabs = usesConfiguredPrefabs;
            Initialize(m_callbackAdapter, itemCount);
        }
    }
}
