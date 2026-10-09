// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    /// <summary>可交互的业务排序示例；状态、祖先汇总和优先级全部由示例业务维护。</summary>
    public sealed class BusinessSortedTreeDemo: MonoBehaviour
    {
        public VirtualScrollView ScrollView;
        public RectTransform BranchPrefab;
        public RectTransform TaskPrefab;
        public Text Selection;
        public Text NotificationLabel;
        public Text UnlockLabel;
        public Text ReadLabel;
        public Text OrderSummary;
        public Text LastAction;
        private readonly List<Node> m_roots = new List<Node>();
        private readonly List<Node> m_nodes = new List<Node>();
        private readonly Dictionary<int, Node> m_byKey = new Dictionary<int, Node>();
        private readonly StringBuilder m_text = new StringBuilder(96);
        private TreeListController<Node, int> m_list;
        private Node m_selected;
        private string m_currentOrder;
        private bool m_overview = true;

        private sealed class Node
        {
            public int Id;
            public string Title;
            public string Detail;
            public string OrderLabel;
            public Node Parent;
            public List<Node> Children;
            public bool HasRedDot;
            public bool IsUnlocked;
            public bool HasUnread;
        }

        // 组件和监听随物理视图创建一次；重绑只覆盖当前 Key 和缓存好的显示内容。
        private sealed class View: ScrollItemView
        {
            private readonly BusinessSortedTreeDemo m_owner;
            private readonly Button m_button;
            private readonly Image m_background;
            private readonly Text m_title;
            private readonly Text m_detail;
            private readonly Text m_arrow;
            private readonly GameObject m_dot;
            private int m_key;
            private bool m_branch;

            public View(RectTransform root, BusinessSortedTreeDemo owner): base(root, -1, -1)
            {
                m_owner = owner;
                m_button = root.GetComponent<Button>();
                m_background = root.GetComponent<Image>();
                m_title = root.Find("Title").GetComponent<Text>();
                m_detail = root.Find("Detail").GetComponent<Text>();
                m_arrow = root.Find("Arrow").GetComponent<Text>();
                m_dot = root.Find("Dot").gameObject;
                m_button.onClick.AddListener(OnClick);
            }

            public void Bind(TreeListRow<Node, int> row)
            {
                m_key = row.Key;
                m_branch = row.HasChildren;
                m_title.text = row.Node.Title;
                m_detail.text = row.Node.Detail;
                m_arrow.text = row.HasChildren ? (row.IsExpanded ? "-" : "+") : "";
                m_dot.SetActive(row.Node.HasRedDot);
                float indent = row.Depth * 20f;
                Vector2 position = m_arrow.rectTransform.anchoredPosition;
                position.x = 16f + indent;
                m_arrow.rectTransform.anchoredPosition = position;
                SetIndent(m_title.rectTransform, 50f + indent);
                SetIndent(m_detail.rectTransform, 50f + indent);
                m_background.color = row.Key == m_owner.m_selected.Id ? new Color32(40, 67, 85, 255) : row.HasChildren
                    ? new Color32(35, 55, 64, 255) : new Color32(23, 36, 54, 255);
                m_title.color = row.Node.IsUnlocked ? Color.white : new Color32(161, 177, 195, 255);
            }

            private static void SetIndent(RectTransform rect, float left)
            {
                Vector2 offset = rect.offsetMin;
                offset.x = left;
                rect.offsetMin = offset;
            }

            private void OnClick()
            {
                if (m_branch) {
                    m_owner.m_overview = false;
                    m_owner.m_list.Toggle(m_key);
                }
                else {
                    m_owner.SelectTask(m_key);
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
        private sealed class BusinessPriorityTreeAdapter: TreeListAdapter<Node, int>
        {
            private readonly BusinessSortedTreeDemo m_owner;

            public BusinessPriorityTreeAdapter(BusinessSortedTreeDemo owner)
            {
                m_owner = owner;
            }

            public override float EstimatedItemSize
            {
                get { return 68f; }
            }

            public override int GetKey(Node node)
            {
                return node.Id;
            }

            public override IReadOnlyList<Node> GetChildren(Node node)
            {
                return node.Children;
            }

            public override RectTransform GetItemPrefab(TreeListRow<Node, int> row)
            {
                return row.HasChildren ? m_owner.BranchPrefab : m_owner.TaskPrefab;
            }

            public override ScrollItemView CreateView(RectTransform root)
            {
                // 组件查询和按钮监听只发生在物理视图创建时，进入池后继续复用。
                return new View(root, m_owner);
            }

            public override void Bind(ScrollItemView view, TreeListRow<Node, int> row, ScrollItemLayoutContext context)
            {
                // 绑定覆盖当前内容和稳定 Key，不缓存会因排序改变的行下标。
                ((View)view).Bind(row);
            }

            public override bool TryGetItemSize(TreeListRow<Node, int> row, ScrollItemLayoutContext context, out float size)
            {
                // 已知高度直接返回，省去 Unity 布局测量；不会创建临时对象。
                size = 68f;
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
            for (int c = 1; c <= 4; ++c) {
                Node campaign = AddNode(c * 1000, "Campaign " + c.ToString("D2"), null, true);
                for (int h = 1; h <= 2; ++h) {
                    Node chapter = AddNode(campaign.Id + h * 100, "Chapter " + c + "." + h, campaign, true);
                    for (int s = 1; s <= 2; ++s) {
                        Node section = AddNode(chapter.Id + s * 10, "Section " + c + "." + h + "." + s, chapter, true);
                        for (int t = 1; t <= 4; ++t) {
                            AddNode(section.Id + t, "Task " + (section.Id + t), section, false);
                        }
                    }
                }
            }
            // Adapter 每个页面初始化一次；排序通过控制器的业务比较器统一执行。
            m_list = new TreeListController<Node, int>(ScrollView, new BusinessPriorityTreeAdapter(this))
            {
                NodeComparison = CompareBusinessPriority
            };
            ResetScenario();
        }

        private Node AddNode(int id, string title, Node parent, bool branch)
        {
            var node = new Node
            {
                Id = id, Title = title, Parent = parent, Children = branch ? new List<Node>() : null,
                OrderLabel = parent == null ? (id / 1000).ToString("D2") : null
            };
            if (parent == null) {
                m_roots.Add(node);
            }
            else {
                parent.Children.Add(node);
            }
            m_nodes.Add(node);
            m_byKey.Add(id, node);
            return node;
        }

        // 按红点、解锁、未读、ID 逐级比较，高优先级存在差异时不再比较后续字段。
        // 规则用于每一层同级节点；通用控制器只执行比较，不识别这些业务字段。
        private static int CompareBusinessPriority(Node left, Node right)
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

        private static void UpdateSummary(Node node)
        {
            if (node.Children != null) {
                // 示例采用“任意后代满足即为 true”的分支汇总规则。
                // 未解锁、未读和红点相互独立，切换一个字段不自动修改其他字段。
                node.HasRedDot = node.IsUnlocked = node.HasUnread = false;
                for (int i = 0; i < node.Children.Count; ++i) {
                    Node child = node.Children[i];
                    node.HasRedDot |= child.HasRedDot;
                    node.IsUnlocked |= child.IsUnlocked;
                    node.HasUnread |= child.HasUnread;
                }
            }
            // 只在业务状态变化后构建显示字符串，Bind 和比较器直接读取缓存，避免滚动分配。
            node.Detail = "ID " + node.Id + "   DOT " + (node.HasRedDot ? "1" : "0") +
                " / OPEN " + (node.IsUnlocked ? "1" : "0") + " / UNREAD " + (node.HasUnread ? "1" : "0");
        }

        private void SubmitChangedPath(string action)
        {
            for (Node node = m_selected; node != null; node = node.Parent) {
                UpdateSummary(node);
            }
            // 总览从顶部展示根分组的新顺序；详情模式保持当前阅读锚点。
            m_list.Submit(m_roots, m_overview ? ScrollUpdatePosition.Start : ScrollUpdatePosition.KeepVisibleItem);
            UpdateOrderSummary();
            UpdateSelection();
            LastAction.text = m_selected.Title + ": " + action + ". Parent flags use ANY descendant; ID breaks ties.";
        }

        private void UpdateOrderSummary()
        {
            m_text.Clear();
            // 读取实际提交后的行投影，包含当前视口之外的根节点。
            for (int i = 0; i < m_list.RowCount; ++i) {
                m_list.TryGetRow(i, out var row);
                if (row.HasParent) {
                    continue;
                }
                if (m_text.Length > 0) {
                    m_text.Append(" > ");
                }
                m_text.Append(row.Node.OrderLabel);
            }
            string order = m_text.ToString();
            OrderSummary.text = "ROOT ORDER\nBefore   " + (m_currentOrder ?? order) + "\nAfter      " + order;
            m_currentOrder = order;
        }

        private void UpdateSelection()
        {
            Selection.text = "SELECTED  " + m_selected.Title + "\n" + m_selected.Parent.Title + " / " + m_selected.Parent.Parent.Title;
            NotificationLabel.text = m_selected.HasRedDot ? "Red dot: ON  /  click to clear" : "Red dot: OFF  /  click to add";
            UnlockLabel.text = m_selected.IsUnlocked ? "Unlocked  /  click to lock" : "Locked  /  click to unlock";
            ReadLabel.text = m_selected.HasUnread ? "Unread  /  click to read" : "Read  /  click to mark unread";
        }

        private void SelectTask(int key)
        {
            m_selected = m_byKey[key];
            UpdateSelection();
            ScrollView.RefreshVisibleItems();
            LastAction.text = "Selected " + m_selected.Title + ". Change one flag on the right to compare its sorting effect.";
        }

        public void ToggleNotification()
        {
            m_selected.HasRedDot = !m_selected.HasRedDot;
            SubmitChangedPath(m_selected.HasRedDot ? "red dot ON" : "red dot OFF");
        }

        public void ToggleUnlock()
        {
            m_selected.IsUnlocked = !m_selected.IsUnlocked;
            SubmitChangedPath(m_selected.IsUnlocked ? "unlocked" : "locked");
        }

        public void ToggleRead()
        {
            m_selected.HasUnread = !m_selected.HasUnread;
            SubmitChangedPath(m_selected.HasUnread ? "marked unread" : "marked read");
        }

        public void ShowRoots()
        {
            m_overview = true;
            m_list.SetAllExpanded(false);
            ScrollView.JumpToDataItem(0);
        }

        public void ShowSelected()
        {
            m_overview = false;
            m_list.ScrollToNode(m_selected.Id, true, 0.25f);
        }

        public void ExpandAll()
        {
            m_overview = false;
            m_list.SetAllExpanded(true);
        }

        public void ResetScenario()
        {
            m_selected = m_byKey[1224];
            m_overview = true;
            for (int i = 0; i < m_nodes.Count; ++i) {
                Node node = m_nodes[i];
                node.HasRedDot = node.Id == 4111;
                node.IsUnlocked = node.Id / 1000 == 2 || node.Id / 1000 == 3;
                node.HasUnread = node.Id == 1224 || node.Id == 3111;
            }
            for (int i = m_nodes.Count - 1; i >= 0; --i) {
                UpdateSummary(m_nodes[i]);
            }
            m_list.Submit(m_roots, ScrollUpdatePosition.Start);
            ShowRoots();
            UpdateOrderSummary();
            UpdateSelection();
            LastAction.text = "Start with Task 1224. Try red dot, unlock, then read; watch the first differing priority decide the order.";
        }

        private void OnDestroy()
        {
            m_list?.Dispose();
        }
    }
}
