// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZRList.Samples.Support;

namespace ZRList.Samples
{
    /// <summary>活动、章节、小节、任务四层树示例；状态汇总与排序由示例业务维护。</summary>
    public sealed class TreeListDemo: MonoBehaviour
    {
        public VirtualScrollView ScrollView;
        public RectTransform BranchPrefab;
        public RectTransform LeafPrefab;
        public Text Status;
        private readonly List<Node> m_roots = new List<Node>();
        private readonly List<Node> m_nodes = new List<Node>();
        private readonly Dictionary<int, Node> m_byKey = new Dictionary<int, Node>();
        private TreeListController<Node, int> m_list;
        private DemoStatusText m_status;

        private sealed class Node
        {
            public int Id;
            public string Title;
            public string Summary;
            public Node Parent;
            public List<Node> Children;
            public bool HasRedDot;
            public bool IsUnlocked;
            public bool HasUnread;
        }

        // 视图缓存组件引用与按钮监听；每次绑定只更新显示内容和当前节点 Key。
        private sealed class View: ScrollItemView
        {
            private readonly TreeListDemo m_owner;
            private readonly Button m_button;
            private readonly Image m_background;
            private readonly Text m_title;
            private readonly Text m_detail;
            private readonly Text m_arrow;
            private readonly GameObject m_dot;
            private int m_key;
            private bool m_branch;

            public View(RectTransform root, TreeListDemo owner): base(root, -1, -1)
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
                m_detail.text = row.Node.Summary;
                m_arrow.text = row.HasChildren ? (row.IsExpanded ? "-" : "+") : "";
                m_dot.SetActive(row.Node.HasRedDot);
                float indent = row.Depth * 28f;
                Vector2 position = m_arrow.rectTransform.anchoredPosition;
                position.x = 16f + indent;
                m_arrow.rectTransform.anchoredPosition = position;
                SetIndent(m_title.rectTransform, 50f + indent);
                SetIndent(m_detail.rectTransform, 50f + indent);
                m_background.color = !row.HasChildren ? new Color32(23, 36, 54, 255) : row.Depth == 0
                    ? new Color32(41, 68, 63, 255) : row.Depth == 1 ? new Color32(36, 55, 76, 255) : new Color32(34, 45, 66, 255);
                m_title.color = row.HasChildren || row.Node.IsUnlocked ? Color.white : new Color32(135, 153, 173, 255);
            }

            private static void SetIndent(RectTransform text, float left)
            {
                Vector2 offset = text.offsetMin;
                offset.x = left;
                text.offsetMin = offset;
            }

            private void OnClick()
            {
                if (m_branch) {
                    m_owner.m_list.Toggle(m_key);
                }
                else {
                    m_owner.ChangeTask(m_key);
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
        private sealed class CampaignTreeAdapter: TreeListAdapter<Node, int>
        {
            private readonly TreeListDemo m_owner;

            public CampaignTreeAdapter(TreeListDemo owner)
            {
                m_owner = owner;
            }

            public override float EstimatedItemSize
            {
                get { return 64f; }
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
                return row.HasChildren ? m_owner.BranchPrefab : m_owner.LeafPrefab;
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
                size = row.HasChildren ? 64f : 72f;
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
            for (int c = 1; c <= 6; ++c) {
                Node campaign = AddNode(c * 1000, "Campaign " + c.ToString("D2"), null, true);
                for (int h = 1; h <= 4; ++h) {
                    Node chapter = AddNode(campaign.Id + h * 100, "Chapter " + c + "." + h, campaign, true);
                    for (int s = 1; s <= 3; ++s) {
                        Node section = AddNode(chapter.Id + s * 10, "Section " + c + "." + h + "." + s, chapter, true);
                        for (int t = 1; t <= 5; ++t) {
                            Node task = AddNode(section.Id + t, "Task " + (section.Id + t), section, false);
                            task.IsUnlocked = c < 5 && t < 5;
                            task.HasUnread = t % 3 != 0;
                            task.HasRedDot = c == 3 && h == 2 && s == 1 && t == 1;
                        }
                    }
                }
            }
            UpdateAllSummaries();
            m_status = new DemoStatusText(Status, " projected rows / ", " visible / ", " pooled");
            // Adapter 每个页面初始化一次；排序通过控制器的业务比较器统一执行。
            m_list = new TreeListController<Node, int>(ScrollView, new CampaignTreeAdapter(this))
            {
                NodeComparison = CompareNodes
            };
            ScrollView.ViewStateChanged += UpdateStatus;
            m_list.Submit(m_roots);
            // 首次展开一条完整路径，让四个层级均可直接看到。
            m_list.ScrollToNode(3211);
            ScrollView.JumpToDataItem(0);
            UpdateStatus();
        }

        private Node AddNode(int id, string title, Node parent, bool branch)
        {
            var node = new Node { Id = id, Title = title, Parent = parent, Children = branch ? new List<Node>() : null };
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

        private static int CompareNodes(Node left, Node right)
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
            if (node.Children == null) {
                node.Summary = !node.IsUnlocked ? "Locked / click to unlock" : node.HasUnread ? "Unread / click to read" : "Read / click to notify";
                return;
            }
            node.HasRedDot = node.IsUnlocked = node.HasUnread = false;
            for (int i = 0; i < node.Children.Count; ++i) {
                Node child = node.Children[i];
                node.HasRedDot |= child.HasRedDot;
                node.IsUnlocked |= child.IsUnlocked;
                node.HasUnread |= child.HasUnread;
            }
            node.Summary = node.Children.Count + " children / " + (node.HasRedDot ? "New activity below" : node.HasUnread ? "Unread tasks below" : "All tasks read");
        }

        private void UpdateAllSummaries()
        {
            // 节点按前序创建，倒序遍历可先汇总子节点再汇总父节点，无需递归。
            for (int i = m_nodes.Count - 1; i >= 0; --i) {
                UpdateSummary(m_nodes[i]);
            }
        }

        private void SubmitChangedPath(Node node)
        {
            // 先沿祖先链完成业务汇总，再提交一次，同时处理排序、内容与滚动锚点。
            for (Node current = node; current != null; current = current.Parent) {
                UpdateSummary(current);
            }
            m_list.Submit(m_roots);
        }

        private void ChangeTask(int key)
        {
            Node node = m_byKey[key];
            if (!node.IsUnlocked) {
                node.IsUnlocked = true;
            }
            else if (node.HasUnread) {
                node.HasUnread = false;
                node.HasRedDot = false;
            }
            else {
                node.HasUnread = true;
                node.HasRedDot = true;
            }
            SubmitChangedPath(node);
        }

        public void ExpandAll()
        {
            m_list.SetAllExpanded(true);
        }
        public void CollapseAll()
        {
            m_list.SetAllExpanded(false);
        }
        public void FindLastTask()
        {
            m_list.ScrollToNode(6435, true, 0.3f);
        }

        public void NotifyLastTask()
        {
            Node node = m_byKey[6435];
            node.HasRedDot = node.HasUnread = node.IsUnlocked = true;
            SubmitChangedPath(node);
        }

        public void MarkAllRead()
        {
            for (int i = 0; i < m_nodes.Count; ++i) {
                m_nodes[i].HasUnread = m_nodes[i].HasRedDot = false;
            }
            UpdateAllSummaries();
            m_list.Submit(m_roots);
        }

        private void UpdateStatus()
        {
            if (Status != null && m_list != null) {
                m_status.Set(m_list.RowCount, ScrollView.VisibleItems.Count, ScrollView.PooledViewCount);
            }
        }

        private void OnDestroy()
        {
            // 由控制器完成旧绑定清理及物理视图销毁，再解除所持有的 Adapter 引用。
            if (ScrollView != null) {
                ScrollView.ViewStateChanged -= UpdateStatus;
            }
            m_list?.Dispose();
        }
    }
}
