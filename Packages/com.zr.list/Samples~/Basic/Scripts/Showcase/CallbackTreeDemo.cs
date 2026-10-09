// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    /// <summary>通过 OnItemRender 接入三层折叠树，演示视图缓存、业务排序和完整清理流程。</summary>
    public sealed class CallbackTreeDemo: MonoBehaviour
    {
        public VirtualScrollView ScrollView;
        public RectTransform BranchPrefab;
        public RectTransform LessonPrefab;
        public Text SortStatus;

        private readonly List<Node> m_roots = new List<Node>();
        private readonly Dictionary<int, Node> m_nodes = new Dictionary<int, Node>();
        private TreeListController<Node, int> m_list;
        private bool m_unreadFirst;

        private sealed class Node
        {
            public int Id;
            public string Title;
            public bool IsRead;
            public List<Node> Children;
        }

        /// <summary>随物理实例复用的组件缓存，不保存会因排序变化的行下标。</summary>
        private sealed class RowView: ScrollItemView
        {
            public readonly Text Title;
            public readonly Text Detail;
            public readonly Text Arrow;
            public readonly GameObject Dot;
            public int Key;
            public bool IsBound;

            private readonly CallbackTreeDemo m_owner;
            private readonly Button m_button;

            public RowView(RectTransform root, CallbackTreeDemo owner): base(root, -1, -1)
            {
                m_owner = owner;
                Title = root.Find("Title").GetComponent<Text>();
                Detail = root.Find("Detail").GetComponent<Text>();
                Arrow = root.Find("Arrow").GetComponent<Text>();
                Dot = root.Find("Dot").gameObject;
                m_button = root.GetComponent<Button>();
                // 监听随实例注册一次；点击读取本次绑定的 Key，不创建捕获节点的闭包。
                m_button.onClick.AddListener(OnClick);
            }

            private void OnClick()
            {
                if (IsBound) {
                    m_owner.ClickNode(Key);
                }
            }

            public void Release()
            {
                IsBound = false;
                m_button.onClick.RemoveListener(OnClick);
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
            CreateData();

            // 数据访问与显示策略在初始化时绑定一次；使用已有集合，不复制成另一套树模型。
            m_list = new TreeListController<Node, int>(ScrollView, GetKey, GetChildren)
            {
                EstimatedItemSize = 72f,
                ItemPrefabSelector = SelectItemPrefab,
                ViewFactory = CreateView,
                ItemSizeProvider = TryGetItemSize,
                NodeComparison = CompareNodes
            };

            // 订阅的是折叠控制器的事件，row 带有节点、深度和展开状态。
            // 工厂和清理事件需在首次 Submit 前配置，确保创建与销毁使用同一套逻辑。
            m_list.OnItemRender += OnItemRender;
            m_list.OnItemRecycle += OnItemRecycle;
            m_list.OnViewDestroyed += OnViewDestroyed;
            m_list.Submit(m_roots);
            m_list.SetExpanded(1000, true);
            m_list.SetExpanded(1100, true);
            UpdateSortStatus();
        }

        private void CreateData()
        {
            // 显示字符串只在创建业务数据时生成，滚动绑定直接读取缓存。
            for (int chapter = 1; chapter <= 8; ++chapter) {
                var root = new Node { Id = chapter * 1000, Title = "Chapter " + chapter.ToString("D2"), Children = new List<Node>(4) };
                m_roots.Add(root);
                m_nodes.Add(root.Id, root);
                for (int section = 1; section <= 4; ++section) {
                    var branch = new Node { Id = root.Id + section * 100, Title = "Section " + chapter + "." + section, Children = new List<Node>(8) };
                    root.Children.Add(branch);
                    m_nodes.Add(branch.Id, branch);
                    for (int lesson = 1; lesson <= 8; ++lesson) {
                        int id = branch.Id + lesson;
                        var node = new Node { Id = id, Title = "Lesson " + id, IsRead = lesson % 2 == 0 };
                        branch.Children.Add(node);
                        m_nodes.Add(id, node);
                    }
                }
            }
        }

        private static int GetKey(Node node)
        {
            return node.Id;
        }

        private static IReadOnlyList<Node> GetChildren(Node node)
        {
            return node.Children;
        }

        private RectTransform SelectItemPrefab(TreeListRow<Node, int> row)
        {
            return row.HasChildren ? BranchPrefab : LessonPrefab;
        }

        private ScrollItemView CreateView(RectTransform root)
        {
            // 只在创建物理视图时查询组件；实例进入池后继续复用该包装。
            return new RowView(root, this);
        }

        private static bool TryGetItemSize(TreeListRow<Node, int> row, ScrollItemLayoutContext context, out float size)
        {
            // 本场景为竖向列表，已知高度直接返回，省去布局测量。
            size = row.HasChildren ? 64f : 72f;
            return true;
        }

        private static void OnItemRender(ScrollItemView view, TreeListRow<Node, int> row)
        {
            // 完整覆盖复用实例的显示状态，不在这里注册监听、查找组件或拼接字符串。
            // 屏外导航测量也可能触发渲染；阅读状态只在用户点击时修改。
            var item = (RowView)view;
            item.Key = row.Key;
            item.IsBound = true;
            item.Title.text = row.Node.Title;
            item.Detail.text = row.HasChildren ? "Click to expand or collapse" :
                row.Node.IsRead ? "Read / click to mark unread" : "Unread / click to read";
            item.Arrow.text = row.HasChildren ? (row.IsExpanded ? "-" : "+") : string.Empty;
            item.Dot.SetActive(!row.HasChildren && !row.Node.IsRead);
            item.Title.color = row.HasChildren || !row.Node.IsRead ? Color.white : new Color32(148, 168, 195, 255);

            float indent = row.Depth * 28f;
            Vector2 position = item.Arrow.rectTransform.anchoredPosition;
            position.x = 16f + indent;
            item.Arrow.rectTransform.anchoredPosition = position;
            SetIndent(item.Title.rectTransform, 50f + indent);
            SetIndent(item.Detail.rectTransform, 50f + indent);
        }

        private static void SetIndent(RectTransform text, float left)
        {
            Vector2 offset = text.offsetMin;
            offset.x = left;
            text.offsetMin = offset;
        }

        private static void OnItemRecycle(ScrollItemView view, TreeListRow<Node, int> oldRow)
        {
            // 旧绑定结束也包括刷新重绑，不一定进入池。真实业务可在这里取消旧数据订阅或异步任务。
            // 本例只结束点击绑定；保留组件缓存和实例级按钮监听供下次绑定复用。
            var item = (RowView)view;
            item.IsBound = false;
            item.Key = 0;
        }

        private static void OnViewDestroyed(ScrollItemView view)
        {
            // 物理视图销毁前才移除实例级监听；root 由列表负责销毁。
            ((RowView)view).Release();
        }

        private int CompareNodes(Node left, Node right)
        {
            // 是否优先未读属于示例业务；比较器无分配，控制器只执行同级排序。
            int result = m_unreadFirst ? left.IsRead.CompareTo(right.IsRead) : 0;
            return result != 0 ? result : left.Id.CompareTo(right.Id);
        }

        private void ClickNode(int key)
        {
            Node node = m_nodes[key];
            if (node.Children != null) {
                m_list.Toggle(key);
            }
            else {
                node.IsRead = !node.IsRead;
                // 一次提交同时刷新内容与顺序，并按稳定 Key 保留展开状态和阅读位置。
                m_list.Submit(m_roots);
            }
        }

        public void ToggleUnreadPriority()
        {
            m_unreadFirst = !m_unreadFirst;
            m_list.Submit(m_roots);
            UpdateSortStatus();
        }

        private void UpdateSortStatus()
        {
            SortStatus.text = m_unreadFirst ? "Order: unread first, then ID / click a lesson to change its state" :
                "Order: ID ascending / toggle unread priority to compare";
        }

        public void ExpandAll()
        {
            m_list.SetAllExpanded(true);
        }

        public void CollapseAll()
        {
            m_list.SetAllExpanded(false);
        }

        public void FindLastLesson()
        {
            m_list.ScrollToNode(8408, expandIfNeeded: true, duration: 0.25f);
        }

        private void OnDestroy()
        {
            // 不提前退订清理事件；Dispose 先结束绑定、销毁视图，再释放所有回调引用。
            m_list?.Dispose();
        }
    }
}
