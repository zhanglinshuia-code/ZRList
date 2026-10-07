// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using ZRList.Samples.Support;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class CatalogScrollDemo: MonoBehaviour
    {
        public VirtualScrollView ScrollView;
        public Texture2D[] Icons;
        public Text Status;
        [Min(0)]
        public int DataCount = 1000;
        private readonly HashSet<int> m_expanded = new HashSet<int>();
        private string[] m_titles;
        private string[] m_indices;
        private DemoStatusText m_statusText;
        private void Start()
        {
            InitializeDemo();
        }

        public void InitializeDemo()
        {
            if (ScrollView.IsInitialized) {
                return;
            }

            EnsureLabels(Mathf.Max(0, DataCount));

            m_statusText = new DemoStatusText(Status, " items  /  ", " visible  /  ", " pooled");
            ScrollView.ViewStateChanged += UpdateStatus;
            ScrollView.Initialize(DataCount);
            ScrollView.OnItemRender += OnItemRender;
        }

        public void OnItemRender(ScrollItemView item, int dataIndex)
        {
            EnsureLabels(Mathf.Max(ScrollView.ItemCount, dataIndex + 1));
            if (item.CachedComponent == null) {
                item.CachedComponent = new CatalogItemView(item.Root);
            }

            Texture texture = Icons != null && Icons.Length > 0 ? Icons[dataIndex % Icons.Length] : null;
            ((CatalogItemView)item.CachedComponent).SetData(dataIndex, m_titles[dataIndex], m_indices[dataIndex], texture, ScrollView.IsHorizontal);
            ScrollView.SetItemSize(dataIndex, GetItemSize(dataIndex));
        }

        private void EnsureLabels(int count)
        {
            int previousCount = m_titles != null ? m_titles.Length : 0;
            if (m_titles != null && previousCount >= count) {
                return;
            }

            System.Array.Resize(ref m_titles, count);
            System.Array.Resize(ref m_indices, count);
            for (int index = previousCount; index < count; ++index) {
                m_indices[index] = (index + 1).ToString("D3");
                m_titles[index] = "Adventure kit " + m_indices[index];
            }
        }

        private float GetItemSize(int dataIndex)
        {
            return (ScrollView.IsHorizontal ? 210f : 104f) + ((dataIndex % 4) * 24f) + (m_expanded.Contains(dataIndex) ? 80f : 0f);
        }

        public void JumpToFirst()
        {
            ScrollView.JumpToDataItem(0, 0.3f);
        }

        public void JumpToLast()
        {
            ScrollView.JumpToDataItem(ScrollView.ItemCount - 1, 0.3f);
        }

        public void ResizeMiddleItem()
        {
            if (ScrollView.ItemCount == 0) {
                return;
            }

            int index = ScrollView.ItemCount / 2;
            if (!m_expanded.Add(index)) {
                m_expanded.Remove(index);
            }

            ScrollView.SetItemSize(index, GetItemSize(index));
            ScrollView.JumpToDataItem(index, 0.3f);
        }

        private void OnDestroy()
        {
            if (ScrollView != null) {
                ScrollView.ViewStateChanged -= UpdateStatus;
                ScrollView.OnItemRender -= OnItemRender;
            }
        }

        private void UpdateStatus()
        {
            if (Status != null && ScrollView != null) {
                m_statusText.Set(ScrollView.ItemCount, ScrollView.VisibleItems.Count, ScrollView.PooledViewCount);
            }
        }
    }
}
