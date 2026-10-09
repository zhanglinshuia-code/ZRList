// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEngine;

namespace ZRList
{
    public sealed partial class TreeListController<TNode, TKey>:
        IScrollItemAdapter, IScrollItemPrefabProvider, IScrollItemViewLifecycle, IScrollItemRenderState
    {
        private float m_estimatedItemSize = 100f;
        private Func<TreeListRow<TNode, TKey>, RectTransform> m_itemPrefabSelector;
        private Func<RectTransform, ScrollItemView> m_viewFactory;
        private ScrollItemSizeProvider<TreeListRow<TNode, TKey>> m_itemSizeProvider;
        private Func<ScrollItemView, TreeListRow<TNode, TKey>, ScrollItemLayoutContext, float> m_itemMeasurer;
        private Action<ScrollItemView, TreeListRow<TNode, TKey>> m_onItemRender;
        private Action<ScrollItemView, TreeListRow<TNode, TKey>> m_onItemRecycle;
        private Action<ScrollItemView> m_onViewDestroyed;

        /// <summary>主轴估计长度。回调模式默认为 100；Adapter 模式读取 Adapter 的估计值。</summary>
        public float EstimatedItemSize
        {
            get { return m_adapter != null ? m_adapter.EstimatedItemSize : m_estimatedItemSize; }
            set
            {
                CheckCallbackConfiguration();
                if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value)) {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                m_estimatedItemSize = value;
            }
        }

        /// <summary>Optional for a single configured prefab. Apply policy changes with Submit.</summary>
        public Func<TreeListRow<TNode, TKey>, RectTransform> ItemPrefabSelector
        {
            get { return m_itemPrefabSelector; }
            set { CheckCallbackConfiguration(); m_itemPrefabSelector = value; }
        }

        /// <summary>Creates one holder per physical root. Configure before Submit; null uses ScrollItemView.</summary>
        public Func<RectTransform, ScrollItemView> ViewFactory
        {
            get { return m_viewFactory; }
            set { CheckCallbackConfiguration(true); m_viewFactory = value; }
        }

        /// <summary>Optional known size provider. Return false to measure the bound view. Apply changes with Submit.</summary>
        public ScrollItemSizeProvider<TreeListRow<TNode, TKey>> ItemSizeProvider
        {
            get { return m_itemSizeProvider; }
            set { CheckCallbackConfiguration(); m_itemSizeProvider = value; }
        }

        /// <summary>Optional measurement override; defaults to ScrollItemMeasurement. Apply changes with Submit.</summary>
        public Func<ScrollItemView, TreeListRow<TNode, TKey>, ScrollItemLayoutContext, float> ItemMeasurer
        {
            get { return m_itemMeasurer; }
            set { CheckCallbackConfiguration(); m_itemMeasurer = value; }
        }

        /// <summary>
        /// Fill a bound or refreshed row, including temporary off-screen measurement during navigation.
        /// This is not a visibility or read notification. Subscribe before or after Submit.
        /// Subscription changes refresh immediately while enabled, otherwise on re-enable.
        /// Without a subscriber no new views are bound.
        /// </summary>
        public event Action<ScrollItemView, TreeListRow<TNode, TKey>> OnItemRender
        {
            add
            {
                if (value == null) {
                    return;
                }
                CheckCallbackConfiguration();
                m_onItemRender += value;
                RefreshRenderSubscription();
            }
            remove
            {
                if (value == null) {
                    return;
                }
                CheckCallbackConfiguration();
                m_onItemRender -= value;
                RefreshRenderSubscription();
            }
        }

        /// <summary>
        /// Ends an old data binding, including a rebind that retains the physical view.
        /// Receives the old row before the snapshot switches. Configure before Submit.
        /// </summary>
        public event Action<ScrollItemView, TreeListRow<TNode, TKey>> OnItemRecycle
        {
            add
            {
                if (value == null) {
                    return;
                }
                CheckCallbackConfiguration(true);
                m_onItemRecycle += value;
            }
            remove
            {
                if (value == null) {
                    return;
                }
                CheckCallbackConfiguration(true);
                m_onItemRecycle -= value;
            }
        }

        /// <summary>Cleanup immediately before a physical view is destroyed. Configure before Submit.</summary>
        public event Action<ScrollItemView> OnViewDestroyed
        {
            add
            {
                if (value == null) {
                    return;
                }
                CheckCallbackConfiguration(true);
                m_onViewDestroyed += value;
            }
            remove
            {
                if (value == null) {
                    return;
                }
                CheckCallbackConfiguration(true);
                m_onViewDestroyed -= value;
            }
        }

        bool IScrollItemRenderState.IsRendererReady
        {
            get { return m_adapter != null || m_onItemRender != null; }
        }

        ScrollItemView IScrollItemAdapter.CreateViewsHolder(RectTransform root)
        {
            if (m_adapter != null) {
                return m_adapter.CreateView(root);
            }
            return m_viewFactory != null ? m_viewFactory(root) : new ScrollItemView(root, -1, -1);
        }

        RectTransform IScrollItemPrefabProvider.GetItemPrefab(int index)
        {
            if (m_adapter != null) {
                return m_adapter.GetItemPrefab(m_rows[index]);
            }
            return m_itemPrefabSelector != null ? m_itemPrefabSelector(m_rows[index])
                : ScrollItemPrefabCollection.Select(m_view.ItemPrefabs, null, index);
        }

        void IScrollItemAdapter.Bind(ScrollItemView view, int index, ScrollItemLayoutContext context)
        {
            if (m_adapter != null) {
                m_adapter.Bind(view, m_rows[index], context);
            }
            else {
                m_onItemRender?.Invoke(view, m_rows[index]);
            }
        }

        bool IScrollItemAdapter.TryGetItemSize(int index, ScrollItemLayoutContext context, out float size)
        {
            if (m_adapter != null) {
                return m_adapter.TryGetItemSize(m_rows[index], context, out size);
            }
            if (m_itemSizeProvider != null) {
                return m_itemSizeProvider(m_rows[index], context, out size);
            }
            size = 0f;
            return false;
        }

        float IScrollItemAdapter.MeasureItem(ScrollItemView view, int index, ScrollItemLayoutContext context)
        {
            if (m_adapter != null) {
                return m_adapter.MeasureItem(view, m_rows[index], context);
            }
            return m_itemMeasurer != null ? m_itemMeasurer(view, m_rows[index], context)
                : ScrollItemMeasurement.Measure(view.Root, context.Orientation);
        }

        void IScrollItemViewLifecycle.OnViewUnbound(ScrollItemView view, int index)
        {
            if (m_adapter != null) {
                m_adapter.Unbind(view, m_rows[index]);
            }
            else {
                m_onItemRecycle?.Invoke(view, m_rows[index]);
            }
        }

        void IScrollItemViewLifecycle.OnViewDestroyed(ScrollItemView view)
        {
            if (m_adapter != null) {
                m_adapter.DestroyView(view);
            }
            else {
                m_onViewDestroyed?.Invoke(view);
            }
        }

        private void CheckCallbackConfiguration(bool requiresUninitializedRenderer = false)
        {
            CheckAvailable();
            if (m_adapter != null) {
                throw new InvalidOperationException("Adapter mode owns rendering. Configure the adapter or use the function-based constructor.");
            }
            if (requiresUninitializedRenderer && m_view.IsInitialized) {
                throw new InvalidOperationException("Configure the view factory and cleanup callbacks before Submit. Dispose releases their subscriptions.");
            }
        }

        private void ValidateRenderingConfiguration()
        {
            float estimate = EstimatedItemSize;
            if (estimate <= 0f || float.IsNaN(estimate) || float.IsInfinity(estimate)) {
                throw new InvalidOperationException("Estimated size must be finite and positive.");
            }
            if (m_adapter == null && m_itemPrefabSelector == null) {
                ScrollItemPrefabCollection.Validate(m_view.ItemPrefabs, null);
            }
        }

        private void RefreshRenderSubscription()
        {
            if (!m_view.IsInitialized) {
                return;
            }
            // Event callbacks and rendering must obey the same mutation guard as Submit.
            m_busy = true;
            try {
                m_view.RefreshVisibleItems();
            }
            catch {
                m_view.Dispose();
                throw;
            }
            finally {
                m_busy = false;
            }
        }

        private void ClearRenderingCallbacks()
        {
            // Called after old bindings and owned physical views have finished their cleanup.
            m_itemPrefabSelector = null;
            m_viewFactory = null;
            m_itemSizeProvider = null;
            m_itemMeasurer = null;
            m_onItemRender = null;
            m_onItemRecycle = null;
            m_onViewDestroyed = null;
        }
    }
}
