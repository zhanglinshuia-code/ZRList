// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZRList.Samples.Support;

namespace ZRList.Samples
{
    /// <summary>一级分组折叠示例；红点、解锁、阅读状态、汇总和排序均属于示例业务。</summary>
    public sealed class ExpandableListDemo: MonoBehaviour
    {
        public VirtualScrollView ScrollView;
        public RectTransform HeaderPrefab;
        public RectTransform ItemPrefab;
        public Text Status;
        private readonly List<Group> m_groups = new List<Group>();
        private readonly Dictionary<int, Item> m_items = new Dictionary<int, Item>();
        private ExpandableListController<Group, Item, int> m_list;
        private DemoStatusText m_status;

        private sealed class Group
        {
            public int Id;
            public string Title;
            public string Summary;
            public bool HasRedDot;
            public bool IsUnlocked;
            public bool HasUnread;
            public readonly List<Item> Items = new List<Item>();
        }

        private sealed class Item
        {
            public int Id;
            public string Title;
            public bool HasRedDot;
            public bool IsUnlocked;
            public bool IsRead;
            public Group Group;
        }

        // 每个物理视图只注册一次监听；重绑时覆盖稳定 Key，点击时读取当前 Key。
        // 滚动绑定不创建闭包、不重复注册监听，也不捕获会因排序改变的行下标。
        private sealed class View: ScrollItemView
        {
            private readonly ExpandableListDemo m_owner;
            private readonly Button m_button;
            private readonly Text m_title;
            private readonly Text m_detail;
            private readonly Text m_arrow;
            private readonly GameObject m_dot;
            private int m_key;
            private bool m_header;

            public View(RectTransform root, ExpandableListDemo owner): base(root, -1, -1)
            {
                m_owner = owner;
                m_button = root.GetComponent<Button>();
                m_title = root.Find("Title").GetComponent<Text>();
                m_detail = root.Find("Detail").GetComponent<Text>();
                m_arrow = root.Find("Arrow").GetComponent<Text>();
                m_dot = root.Find("Dot").gameObject;
                m_button.onClick.AddListener(OnClick);
            }

            public void Bind(ExpandableListRow<Group, Item, int> row)
            {
                m_header = row.IsHeader;
                m_key = row.IsHeader ? row.GroupKey : row.ItemKey;
                m_title.text = row.IsHeader ? row.Group.Title : row.Item.Title;
                m_detail.text = row.IsHeader ? row.Group.Summary : !row.Item.IsUnlocked ? "Locked / click to unlock" :
                    !row.Item.IsRead ? "Unread / click to read" : "Read / click to add a notification";
                m_arrow.text = row.IsHeader ? (row.IsExpanded ? "-" : "+") : "";
                m_dot.SetActive(row.IsHeader ? row.Group.HasRedDot : row.Item.HasRedDot);
                m_title.color = row.IsHeader || row.Item.IsUnlocked ? Color.white : new Color(0.53f, 0.60f, 0.68f);
            }

            private void OnClick()
            {
                if (m_header) {
                    m_owner.m_list.Toggle(m_key);
                }
                else {
                    m_owner.ChangeItem(m_key);
                }
            }

            public void Release()
            {
                m_button.onClick.RemoveListener(OnClick);
            }
        }

        /// <summary>
        /// 将本示例的数据访问、模板、绑定和清理集中到一个对象，方便同类页面整体复用。
        /// Adapter 不保存展开状态或行下标，红点汇总和比较规则仍由业务代码维护。
        /// </summary>
        private sealed class ChapterListAdapter: ExpandableListAdapter<Group, Item, int>
        {
            private readonly ExpandableListDemo m_owner;

            public ChapterListAdapter(ExpandableListDemo owner)
            {
                m_owner = owner;
            }

            public override float EstimatedItemSize
            {
                get { return 72f; }
            }

            public override int GetGroupKey(Group group)
            {
                return group.Id;
            }

            public override IReadOnlyList<Item> GetItems(Group group)
            {
                return group.Items;
            }

            public override int GetItemKey(Item item)
            {
                return item.Id;
            }

            public override RectTransform GetItemPrefab(ExpandableListRow<Group, Item, int> row)
            {
                return row.IsHeader ? m_owner.HeaderPrefab : m_owner.ItemPrefab;
            }

            public override ScrollItemView CreateView(RectTransform root)
            {
                // 组件查询和按钮监听只发生在物理视图创建时，进入池后继续复用。
                return new View(root, m_owner);
            }

            public override void Bind(ScrollItemView view, ExpandableListRow<Group, Item, int> row, ScrollItemLayoutContext context)
            {
                // 绑定覆盖当前内容和稳定 Key，不缓存会因排序改变的行下标。
                ((View)view).Bind(row);
            }

            public override bool TryGetItemSize(ExpandableListRow<Group, Item, int> row, ScrollItemLayoutContext context, out float size)
            {
                // 已知高度直接返回，省去 Unity 布局测量；不会创建临时对象。
                size = row.IsHeader ? 64f : 72f + (row.ItemKey % 3) * 8f;
                return true;
            }

            public override void DestroyView(ScrollItemView view)
            {
                // 仅在物理实例销毁时移除监听，普通回收保留组件缓存与监听。
                ((View)view).Release();
            }
        }

        private void Start()
        {
            InitializeDemo();
        }

        public void InitializeDemo()
        {
            if (m_list != null) {
                return;
            }
            for (int g = 1; g <= 24; ++g) {
                var group = new Group { Id = g, Title = "Chapter " + g.ToString("D2") };
                for (int i = 0; i < 12; ++i) {
                    int id = g * 100 + i;
                    var item = new Item
                    {
                        Id = id, Group = group, Title = "Objective " + id,
                        IsUnlocked = g < 16 && i % 4 != 3,
                        IsRead = i % 3 == 0,
                        HasRedDot = g % 5 == 0 && i == 1
                    };
                    group.Items.Add(item);
                    m_items.Add(id, item);
                }
                UpdateSummary(group);
                m_groups.Add(group);
            }
            m_status = new DemoStatusText(Status, " projected rows  /  ", " visible  /  ", " pooled");
            // Adapter 每个页面初始化一次；排序通过控制器的业务比较器统一执行。
            m_list = new ExpandableListController<Group, Item, int>(ScrollView, new ChapterListAdapter(this))
            {
                DefaultExpanded = true,
                GroupComparison = CompareGroups,
                ItemComparison = CompareItems
            };
            ScrollView.ViewStateChanged += UpdateStatus;
            m_list.Submit(m_groups);
            UpdateStatus();
        }

        private static int CompareGroups(Group left, Group right)
        {
            int result = right.HasRedDot.CompareTo(left.HasRedDot);
            if (result == 0) {
                result = right.IsUnlocked.CompareTo(left.IsUnlocked);
            }
            if (result == 0) {
                result = right.HasUnread.CompareTo(left.HasUnread);
            }
            return result != 0 ? result : left.Id.CompareTo(right.Id);
        }

        private static int CompareItems(Group group, Item left, Item right)
        {
            int result = right.HasRedDot.CompareTo(left.HasRedDot);
            if (result == 0) {
                result = right.IsUnlocked.CompareTo(left.IsUnlocked);
            }
            if (result == 0) {
                result = left.IsRead.CompareTo(right.IsRead);
            }
            return result != 0 ? result : left.Id.CompareTo(right.Id);
        }

        private static void UpdateSummary(Group group)
        {
            // 汇总与显示字符串在业务变化时更新，渲染和比较器直接读取已有结果。
            int unread = 0;
            group.HasRedDot = group.IsUnlocked = group.HasUnread = false;
            for (int i = 0; i < group.Items.Count; ++i) {
                Item item = group.Items[i];
                group.HasRedDot |= item.HasRedDot;
                group.IsUnlocked |= item.IsUnlocked;
                group.HasUnread |= !item.IsRead;
                if (!item.IsRead) {
                    ++unread;
                }
            }
            group.Summary = group.Items.Count + " objectives / " + unread + " unread";
        }

        private void ChangeItem(int key)
        {
            Item item = m_items[key];
            if (!item.IsUnlocked) {
                item.IsUnlocked = true;
            }
            else if (!item.IsRead) {
                item.IsRead = true;
                item.HasRedDot = false;
            }
            else {
                item.IsRead = false;
                item.HasRedDot = true;
            }
            UpdateSummary(item.Group);
            m_list.Submit(m_groups);
        }

        public void ExpandAll()
        {
            m_list.SetAllExpanded(true);
        }
        public void CollapseAll()
        {
            m_list.SetAllExpanded(false);
        }

        public void NotifyLastGroup()
        {
            Group group = m_groups[m_groups.Count - 1];
            group.Items[0].HasRedDot = true;
            group.Items[0].IsRead = false;
            group.Items[0].IsUnlocked = true;
            UpdateSummary(group);
            m_list.Submit(m_groups);
        }

        public void MarkAllRead()
        {
            for (int g = 0; g < m_groups.Count; ++g) {
                Group group = m_groups[g];
                for (int i = 0; i < group.Items.Count; ++i) {
                    group.Items[i].IsRead = true;
                    group.Items[i].HasRedDot = false;
                }
                UpdateSummary(group);
            }
            m_list.Submit(m_groups);
        }

        public void JumpToLastItem()
        {
            m_list.ScrollToItem(2411, true, 0.3f);
        }

        private void UpdateStatus()
        {
            if (Status != null && m_list != null) {
                m_status.Set(m_list.RowCount, ScrollView.VisibleItems.Count, ScrollView.PooledViewCount);
            }
        }

        private void OnDestroy()
        {
            // 控制器先解绑并销毁视图，再释放 Adapter 引用，确保实例清理可以完整执行。
            if (ScrollView != null) {
                ScrollView.ViewStateChanged -= UpdateStatus;
            }
            m_list?.Dispose();
        }
    }
}
