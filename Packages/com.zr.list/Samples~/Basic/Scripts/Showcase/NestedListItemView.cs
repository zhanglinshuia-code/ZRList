// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class NestedListItemView
    {
        public readonly RectTransform Root;
        public readonly Text Title;
        public readonly VirtualScrollView InnerList;
        public int GroupIndex { get; private set; } = -1;
        private readonly List<ScrollItemView> m_visibleItems = new List<ScrollItemView>();
        private string m_description;
        private string[] m_childTitles;

        public NestedListItemView(RectTransform root)
        {
            Root = root;
            Title = root.Find("Title").GetComponent<Text>();
            InnerList = root.Find("InnerList").GetComponent<VirtualScrollView>();
        }

        public void SetData(int groupIndex, int childCount, int firstVisibleIndex, string title, string description, string[] childTitles)
        {
            GroupIndex = groupIndex;
            Title.text = title;
            m_description = description;
            m_childTitles = childTitles;
            // Outer views are bound while inactive. Enable this group before
            // initializing and restoring its independently virtualized child.
            Root.gameObject.SetActive(true);
            if (InnerList.IsInitialized) {
                InnerList.ReloadData(childCount);
            }
            else {
                InnerList.Initialize(childCount);
                InnerList.OnItemRender += OnItemRender;
            }

            if (childCount > 0 && firstVisibleIndex > 0) {
                InnerList.JumpToDataItem(Mathf.Min(firstVisibleIndex, childCount - 1));
            }
        }

        private void OnItemRender(ScrollItemView item, int dataIndex)
        {
            item.CachedComponent ??= new NestedListChildView(item.Root);
            ((NestedListChildView)item.CachedComponent).SetData(GroupIndex, m_childTitles[dataIndex], m_description);
        }

        public int Unbind()
        {
            if (InnerList == null || !InnerList.IsInitialized) {
                return 0;
            }

            InnerList.GetVisibleItems(m_visibleItems);
            int firstVisibleIndex = m_visibleItems.Count > 0 ? m_visibleItems[0].ItemIndex : 0;
            InnerList.ReloadData(0);
            GroupIndex = -1;
            return firstVisibleIndex;
        }
    }
}
