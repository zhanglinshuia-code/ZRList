// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using ZRList.Samples.Support;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class NestedScrollDemo: MonoBehaviour
    {
        public VirtualScrollView ScrollView;
        public Text Status;
        [Min(0)]
        public int GroupCount = 120;
        [Min(1)]
        public int ItemsPerGroup = 80;
        private readonly Dictionary<int, int> m_groupPositions = new Dictionary<int, int>();
        private string[] m_titles;
        private string[] m_descriptions;
        private string[] m_childTitles;
        private int m_cachedItemsPerGroup;
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

            EnsureLabels(Mathf.Max(0, GroupCount));

            m_statusText = new DemoStatusText(Status, " groups  /  ", " visible  /  ", "both levels recycled");
            ScrollView.ViewStateChanged += UpdateStatus;
            ScrollView.Initialize(GroupCount);
            ScrollView.OnItemRecycle += OnItemRecycle;
            ScrollView.OnItemRender += OnItemRender;
        }

        public int GetChildCount(int groupIndex)
        {
            return ItemsPerGroup + ((groupIndex % 5) * 7);
        }

        private void OnItemRender(ScrollItemView item, int dataIndex)
        {
            EnsureLabels(Mathf.Max(ScrollView.ItemCount, dataIndex + 1));
            item.CachedComponent ??= new NestedListItemView(item.Root);
            m_groupPositions.TryGetValue(dataIndex, out int firstVisibleIndex);
            ((NestedListItemView)item.CachedComponent).SetData(dataIndex, GetChildCount(dataIndex), firstVisibleIndex, m_titles[dataIndex], m_descriptions[dataIndex], m_childTitles);
        }

        private void EnsureLabels(int count)
        {
            bool childCountsChanged = m_childTitles == null || m_cachedItemsPerGroup != ItemsPerGroup;
            int previousCount = m_titles != null ? m_titles.Length : 0;
            if (!childCountsChanged && m_titles != null && previousCount >= count) {
                return;
            }

            int capacity = Mathf.Max(previousCount, count);
            System.Array.Resize(ref m_titles, capacity);
            System.Array.Resize(ref m_descriptions, capacity);
            for (int index = childCountsChanged ? 0 : previousCount; index < capacity; ++index) {
                string number = (index + 1).ToString("D3");
                m_titles[index] = "Group " + number + "  /  " + GetChildCount(index) + " items";
                m_descriptions[index] = "Group " + number + " / independently recycled";
            }

            if (childCountsChanged) {
                m_cachedItemsPerGroup = ItemsPerGroup;
                m_childTitles = new string[Mathf.Max(0, ItemsPerGroup + 28)];
                for (int index = 0; index < m_childTitles.Length; ++index) {
                    m_childTitles[index] = "Item " + (index + 1).ToString("D3");
                }
            }
        }

        private void OnItemRecycle(ScrollItemView item, int dataIndex)
        {
            if (item.CachedComponent is NestedListItemView view) {
                m_groupPositions[dataIndex] = view.Unbind();
            }
        }

        public void JumpToFirst()
        {
            ScrollView.JumpToDataItem(0, 0.3f);
        }

        public void JumpToLast()
        {
            ScrollView.JumpToDataItem(ScrollView.ItemCount - 1, 0.3f);
        }

        public void JumpInnerToLast()
        {
            var items = ScrollView.VisibleItems;
            for (var index = 0; index < items.Count; ++index) {
                var view = (NestedListItemView)items[index].CachedComponent;
                view.InnerList.JumpToDataItem(view.InnerList.ItemCount - 1, 0.3f);
            }
        }

        private void UpdateStatus()
        {
            if (Status != null) {
                m_statusText.Set(ScrollView.ItemCount, ScrollView.VisibleItems.Count);
            }
        }

        private void OnDestroy()
        {
            if (ScrollView != null) {
                ScrollView.ViewStateChanged -= UpdateStatus;
                ScrollView.OnItemRecycle -= OnItemRecycle;
                ScrollView.OnItemRender -= OnItemRender;
            }
        }
    }
}
