// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Unity.Profiling;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace ZRList.Tests
{
    public static class TreeListRegression
    {
        private sealed class Node
        {
            public int Id;
            public int Value;
            public float Size = 32f;
            public List<Node> Children;
        }

        private sealed class Holder: ScrollItemView
        {
            public int Key;
            public int Parent;
            public int Depth;
            public int Value;
            public Node Node;
            public Holder(RectTransform root): base(root, -1, -1) { }
        }

        private sealed class TestAdapter: TreeListAdapter<Node, int>
        {
            public RectTransform Prefab;
            public RectTransform AlternativePrefab;
            public bool KnownSize = true;
            public bool Alternative;
            public bool FailBind;
            public bool FailRead;
            public int Reads;
            public int Binds;
            public int Created;
            public int Recycled;
            public int Destroyed;
            public int SizeQueries;
            public Action DuringBind;
            public Action DuringRecycle;
            public Action DuringDestroy;
            public float Estimate = 32f;
            public override float EstimatedItemSize { get { return Estimate; } }
            public override int GetKey(Node node) { return node.Id; }
            public override IReadOnlyList<Node> GetChildren(Node node)
            {
                ++Reads;
                if (FailRead) throw new InvalidOperationException("Expected accessor failure");
                return node.Children;
            }
            public override RectTransform GetItemPrefab(TreeListRow<Node, int> row) { return Alternative ? AlternativePrefab : Prefab; }
            public override ScrollItemView CreateView(RectTransform root) { ++Created; return new Holder(root); }
            public void Render(ScrollItemView view, TreeListRow<Node, int> row)
            {
                Bind(view, row, default);
            }

            public override void Bind(ScrollItemView view, TreeListRow<Node, int> row, ScrollItemLayoutContext context)
            {
                ++Binds;
                var holder = (Holder)view;
                holder.Key = row.Key;
                holder.Parent = row.ParentKey;
                holder.Depth = row.Depth;
                holder.Value = row.Node.Value;
                holder.Node = row.Node;
                DuringBind?.Invoke();
                if (FailBind) throw new InvalidOperationException("Expected binding failure");
            }
            public override bool TryGetItemSize(TreeListRow<Node, int> row, ScrollItemLayoutContext context, out float size)
            {
                ++SizeQueries;
                size = row.Node.Size + (row.IsExpanded ? 4f : 0f);
                return KnownSize;
            }
            public override float MeasureItem(ScrollItemView view, TreeListRow<Node, int> row, ScrollItemLayoutContext context)
            {
                return row.Node.Size + (row.IsExpanded ? 4f : 0f);
            }
            public override void Unbind(ScrollItemView view, TreeListRow<Node, int> row)
            {
                ++Recycled;
                var holder = (Holder)view;
                Require(holder.Key == row.Key && holder.Parent == row.ParentKey && holder.Depth == row.Depth && ReferenceEquals(holder.Node, row.Node),
                    "Unbind did not retain the old tree identity");
                DuringRecycle?.Invoke();
            }
            public override void DestroyView(ScrollItemView view) { ++Destroyed; DuringDestroy?.Invoke(); }
        }

        private sealed class Fixture: IDisposable
        {
            public readonly VirtualScrollView View;
            public readonly TestAdapter Adapter;
            public readonly TreeListController<Node, int> Tree;
            public readonly List<Node> Roots = new List<Node>();

            public Fixture(bool horizontal = false, bool known = true, int roots = 8, int branches = 3, int leaves = 8,
                bool render = true, bool submit = true, bool useAdapter = false, TestAdapter adapter = null)
            {
                var root = new GameObject("Tree regression", typeof(RectTransform), typeof(VirtualScrollRect), typeof(VirtualScrollView));
                var viewport = (RectTransform)root.transform;
                viewport.sizeDelta = horizontal ? new Vector2(240f, 300f) : new Vector2(300f, 240f);
                var content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
                content.SetParent(viewport, false);
                content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
                View = root.GetComponent<VirtualScrollView>();
                View.Viewport = viewport;
                View.Content = content;
                View.ScrollRect = root.GetComponent<VirtualScrollRect>();
                View.Orientation = horizontal ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical;
                View.ContentSpacing = 3f;
                View.ContentPadding = new RectOffset(7, 7, 7, 7);
                View.ScrollRect.movementType = ScrollRect.MovementType.Clamped;
                Adapter = adapter ?? new TestAdapter { Prefab = Prefab(viewport, "Node"), AlternativePrefab = Prefab(viewport, "Alternative"), KnownSize = known };
                if (useAdapter) {
                    Tree = new TreeListController<Node, int>(View, Adapter);
                }
                else {
                    Tree = new TreeListController<Node, int>(View, Adapter.GetKey, Adapter.GetChildren)
                    {
                        EstimatedItemSize = Adapter.EstimatedItemSize,
                        ItemPrefabSelector = Adapter.GetItemPrefab,
                        ViewFactory = Adapter.CreateView,
                        ItemSizeProvider = Adapter.TryGetItemSize,
                        ItemMeasurer = Adapter.MeasureItem
                    };
                    Tree.OnItemRecycle += Adapter.Unbind;
                    Tree.OnViewDestroyed += Adapter.DestroyView;
                    if (render) Tree.OnItemRender += Adapter.Render;
                }
                Tree.DefaultExpanded = true;
                for (int r = 1; r <= roots; ++r) {
                    var node = new Node { Id = r * 10000, Children = new List<Node>() };
                    for (int b = 1; b <= branches; ++b) {
                        var branch = new Node { Id = node.Id + b * 100, Children = new List<Node>() };
                        for (int l = 1; l <= leaves; ++l) branch.Children.Add(new Node { Id = branch.Id + l });
                        node.Children.Add(branch);
                    }
                    Roots.Add(node);
                }
                if (submit) Tree.Submit(Roots);
            }

            private static RectTransform Prefab(RectTransform parent, string name)
            {
                var root = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                root.SetParent(parent, false);
                root.sizeDelta = new Vector2(32f, 32f);
                root.gameObject.SetActive(false);
                return root;
            }

            public void Scroll(float offset)
            {
                View.Content.anchoredPosition = View.IsHorizontal ? new Vector2(-offset, 0f) : new Vector2(0f, offset);
                View.ScrollRectValueChanged(Vector2.zero);
            }

            public float Inset(ScrollItemView holder)
            {
                return (View.Content.anchoredPosition[View.ScrollAxisIndex] + holder.Root.anchoredPosition[View.ScrollAxisIndex] +
                    (View.IsHorizontal ? holder.Root.rect.xMin : holder.Root.rect.yMax)) * View.ScrollAxisSign;
            }

            public void Dispose()
            {
                Adapter.DuringBind = null;
                Adapter.DuringRecycle = null;
                Adapter.DuringDestroy = null;
                Adapter.FailBind = false;
                Tree.Dispose();
                UnityEngine.Object.DestroyImmediate(View.gameObject);
            }
        }

        private static int s_checks;
        private static object s_allocation;

        public static void Run()
        {
            if (!Application.isPlaying) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            s_checks = 0;
            foreach (bool useAdapter in new[] { false, true }) {
                CheckProjection(useAdapter);
                CheckValidation(useAdapter);
                CheckDeepTree(useAdapter);
                CheckRandomOperations(useAdapter);
                foreach (bool horizontal in new[] { false, true }) {
                    foreach (bool known in new[] { false, true }) CheckNavigationAndMoves(horizontal, known, useAdapter);
                }
                CheckLifecycle(useAdapter);
                CheckExternalDisposal(useAdapter);
                CheckAllocations(useAdapter);
            }
            CheckCallbackLifecycle();
            CheckCallbackConfiguration();
            CheckAdapterIntegration();
            Debug.Log("TREE_REGRESSION_PASSED checks=" + s_checks);
        }

        private static void CheckProjection(bool useAdapter)
        {
            using (var f = new Fixture(useAdapter: useAdapter)) {
                Require(f.Tree.NodeCount == 224 && f.Tree.RowCount == 224 && f.Tree.RootCount == 8, "Initial tree counts");
                Require(f.Tree.TryGetRow(2, out var row) && row.Key == 10101 && row.Depth == 2 && row.ParentKey == 10100 && !row.HasChildren, "Preorder context");
                int reads = f.Adapter.Reads;
                int events = 0;
                f.Tree.ExpansionChanged += () => ++events;
                f.Tree.SetExpanded(10000, false);
                Require(f.Tree.RowCount == 197 && f.Tree.IsExpanded(10100), "Parent collapse discarded descendant expansion");
                int binds = f.Adapter.Binds;
                f.Tree.SetExpanded(10100, false);
                Require(f.Adapter.Binds == binds && f.Tree.RowCount == 197, "Hidden toggle rebuilt the visible tree");
                f.Tree.SetExpanded(10000, true);
                Require(f.Tree.RowCount == 216 && !f.Tree.TryGetRowIndex(10101, out _), "Hidden child expansion preference lost");
                Require(f.Adapter.Reads == reads && events == 3, "Expansion reread the business tree or raised multiple events");
                Require(!f.Tree.Toggle(10101) && !f.Tree.Toggle(-1), "Leaf/unknown toggle should be a no-op");
                f.Tree.NodeComparison = (a, b) => b.Id.CompareTo(a.Id);
                f.Tree.ChildComparison = (parent, a, b) => parent.Id == 80000 ? a.Id.CompareTo(b.Id) : b.Id.CompareTo(a.Id);
                f.Tree.Submit(f.Roots);
                Require(f.Tree.TryGetRow(0, out row) && row.Key == 80000 && !row.HasParent, "Root sort");
                Require(f.Tree.TryGetRow(1, out row) && row.Key == 80100, "Parent-specific child comparison");
                Require(f.Tree.TryGetRow(2, out row) && row.Key == 80108, "Grandchild comparison");
                Require(f.Roots[0].Id == 10000 && f.Roots[0].Children[0].Id == 10100, "Sorting mutated business collections");
                f.Tree.NodeComparison = (a, b) => 0;
                f.Tree.ChildComparison = null;
                f.Tree.Submit(f.Roots);
                Require(f.Tree.TryGetRow(0, out row) && row.Key == 10000 && f.Tree.TryGetRow(1, out row) && row.Key == 10100, "Stable input order for ties");
                f.Tree.SetAllExpanded(false);
                Require(f.Tree.RowCount == 8 && events == 4 && !f.Tree.IsExpanded(80100), "Collapse all omitted hidden branches");
                f.Tree.SetAllExpanded(false);
                Require(events == 4, "No-op expansion event");
                f.Tree.Submit(Array.Empty<Node>());
                Require(f.Tree.RowCount == 0 && f.Tree.NodeCount == 0 && f.View.VisibleItems.Count == 0, "Empty forest");
                f.Tree.DefaultExpanded = false;
                f.Tree.Submit(f.Roots);
                Require(f.Tree.RowCount == 8 && !f.Tree.IsExpanded(10000), "Removed nodes retained state");
                f.Tree.Submit(new[] { new Node { Id = 0, Children = new List<Node>() }, new Node { Id = 1 } });
                Require(f.Tree.RowCount == 2 && !f.Tree.IsExpanded(0), "Null/empty leaves and default-valued key");
            }
        }

        private static void CheckValidation(bool useAdapter)
        {
            using (var f = new Fixture(useAdapter: useAdapter)) {
                f.Tree.SetAllExpanded(false);
                Node branch = f.Roots[0].Children[0];
                branch.Children.Add(f.Roots[0]);
                Expect<ArgumentException>(() => f.Tree.Submit(f.Roots));
                branch.Children.RemoveAt(branch.Children.Count - 1);
                f.Roots[1].Children.Add(branch);
                Expect<ArgumentException>(() => f.Tree.Submit(f.Roots));
                f.Roots[1].Children.RemoveAt(f.Roots[1].Children.Count - 1);
                branch.Children.Add(new Node { Id = branch.Children[0].Id });
                Expect<ArgumentException>(() => f.Tree.Submit(f.Roots));
                branch.Children.RemoveAt(branch.Children.Count - 1);
                Require(f.Tree.RowCount == 8 && f.Tree.NodeCount == 224 && f.View.IsInitialized, "Hidden duplicate/cycle partially committed");
                f.Adapter.FailRead = true;
                Expect<InvalidOperationException>(() => f.Tree.Submit(f.Roots));
                f.Adapter.FailRead = false;
                f.Tree.NodeComparison = (a, b) => throw new InvalidOperationException("Expected comparison failure");
                Expect<InvalidOperationException>(() => f.Tree.Submit(f.Roots));
                f.Tree.NodeComparison = null;
                Expect<ArgumentOutOfRangeException>(() => f.Tree.Submit(f.Roots, (ScrollUpdatePosition)99));
                Require(f.Tree.RowCount == 8 && f.View.ItemCount == 8, "Rejected submission changed projection");
                f.Tree.Submit(f.Roots);
                Require(f.Tree.ScrollToNode(10101), "Cannot recover from rejected submissions");
            }
        }

        private static void CheckDeepTree(bool useAdapter)
        {
            using (var f = new Fixture(roots: 0, useAdapter: useAdapter)) {
                const int count = 10000;
                var root = new Node { Id = 0 };
                Node node = root;
                for (int i = 1; i < count; ++i) {
                    var child = new Node { Id = i };
                    node.Children = new List<Node> { child };
                    node = child;
                }
                f.Tree.DefaultExpanded = false;
                f.Tree.Submit(new[] { root });
                Require(f.Tree.NodeCount == count && f.Tree.RowCount == 1, "Deep ingestion");
                int events = 0;
                f.Tree.ExpansionChanged += () => ++events;
                Require(!f.Tree.ScrollToNode(count - 1, false) && !f.Tree.ScrollToNode(count - 1, true, float.NaN), "Rejected navigation mutated a hidden tree");
                Require(f.Tree.RowCount == 1 && events == 0, "Invalid navigation changed expansion");
                Require(f.Tree.ScrollToNode(count - 1) && f.Tree.RowCount == count && events == 1, "Ancestor chain was not opened atomically");
                Require(f.Tree.TryGetRow(count - 1, out var row) && row.Depth == count - 1 && row.ParentKey == count - 2, "Deep hierarchy metadata");
                Require(f.View.IsItemVisible(count - 1), "Deep target not visible");
                f.Tree.SetExpanded(0, false);
                Require(f.Tree.RowCount == 1 && f.Tree.IsExpanded(count - 2), "Deep collapse lost descendant state");
                f.Tree.SetExpanded(0, true);
                Require(f.Tree.RowCount == count, "Deep unfold failed");
            }
        }

        private static void CheckRandomOperations(bool useAdapter)
        {
            using (var f = new Fixture(useAdapter: useAdapter)) {
                var random = new System.Random(724);
                for (int i = 0; i < 250; ++i) {
                    Node root = f.Roots[random.Next(f.Roots.Count)];
                    if (i % 5 == 0 && root.Children.Count > 0) {
                        Node branch = root.Children[0];
                        root.Children.RemoveAt(0);
                        f.Roots[random.Next(f.Roots.Count)].Children.Add(branch);
                        f.Roots.Reverse();
                        f.Tree.Submit(f.Roots);
                    }
                    else if (root.Children.Count > 0) {
                        f.Tree.Toggle(i % 2 == 0 ? root.Id : root.Children[random.Next(root.Children.Count)].Id);
                    }
                    int rowIndex = 0;
                    for (int r = 0; r < f.Roots.Count; ++r) CheckReference(f, f.Roots[r], 0, 0, ref rowIndex);
                    Require(rowIndex == f.Tree.RowCount, "Random tree projection count");
                }
            }
        }

        private static void CheckReference(Fixture f, Node node, int parent, int depth, ref int index)
        {
            Require(f.Tree.TryGetRow(index++, out var row) && row.Key == node.Id && row.ParentKey == parent && row.Depth == depth, "Reference DFS disagrees with packed snapshot");
            if (node.Children != null && f.Tree.IsExpanded(node.Id)) {
                for (int i = 0; i < node.Children.Count; ++i) CheckReference(f, node.Children[i], node.Id, depth + 1, ref index);
            }
        }

        private static void CheckNavigationAndMoves(bool horizontal, bool known, bool useAdapter)
        {
            using (var f = new Fixture(horizontal, known, useAdapter: useAdapter)) {
                f.Tree.ScrollToNode(30103);
                f.Tree.TryGetRowIndex(30103, out int row);
                Require(f.View.TryGetVisibleItem(row, out var holder), "Missing anchor before move");
                float inset = f.Inset(holder);
                f.View.TryGetItemBinding(holder.Root.gameObject, out ScrollItemBinding binding);
                f.Roots.Reverse();
                f.Tree.Submit(f.Roots);
                f.Tree.TryGetRowIndex(30103, out row);
                Require(f.View.TryGetVisibleItem(row, out var retained) && ReferenceEquals(holder, retained) && Mathf.Abs(f.Inset(retained) - inset) < 0.1f, "Reorder lost anchored physical view");
                Require(!f.View.SetItemSize(binding, 123f), "Old binding stayed valid");
                Node sourceRoot = f.Roots[5]; // Root 3 after reversal.
                Node branch = sourceRoot.Children[0];
                Node target = f.Roots[2].Children[0];
                sourceRoot.Children.RemoveAt(0);
                target.Children.Add(branch);
                branch.Children[2].Value = 999;
                branch.Children[2].Size = 81f;
                f.Tree.Submit(f.Roots);
                Require(f.Tree.TryGetNode(30100, out var node) && node.Depth == 2 && node.ParentKey == target.Id && node.IsExpanded, "Subtree move lost parent/depth/expansion");
                Require(f.Tree.TryGetNode(30103, out node) && node.Depth == 3, "Moved subtree descendant depth");
                f.Tree.TryGetRowIndex(30103, out row);
                Require(f.View.TryGetVisibleItem(row, out retained) && ((Holder)retained).Value == 999 && retained.ItemSize == 81f && Mathf.Abs(f.Inset(retained) - inset) < 0.1f, "Moved anchor content/size/position");
                f.Adapter.Alternative = true;
                f.Tree.Submit(f.Roots);
                Require(f.View.TryGetVisibleItem(row, out retained) && retained.Prefab == f.Adapter.AlternativePrefab, "Tree prefab replacement");
                f.Tree.SetExpanded(target.Id, false);
                Require(f.Tree.TryGetRowIndex(target.Id, out row) && f.View.IsItemVisible(row), "Hidden anchor did not fall back to nearest visible ancestor");
                f.Tree.ScrollToNode(30103);
                target.Children.Remove(branch);
                f.Tree.Submit(f.Roots);
                Require(f.Tree.TryGetRowIndex(target.Id, out row) && f.View.IsItemVisible(row), "Deleted subtree did not fall back to surviving ancestor");
                f.Tree.SetAllExpanded(false);
                f.Tree.ScrollToNode(20101);
                Require(f.Tree.IsExpanded(20000) && f.Tree.IsExpanded(20100) && !f.Tree.IsExpanded(20200), "Navigation opened unrelated branches");
                f.View.enabled = false;
                f.Tree.SetAllExpanded(false);
                f.Tree.Submit(f.Roots);
                f.View.enabled = true;
                Require(f.Tree.ScrollToNode(20102), "Disabled updates failed to recover");
            }
        }

        private static void CheckLifecycle(bool useAdapter)
        {
            using (var f = new Fixture(useAdapter: useAdapter)) {
                f.Adapter.DuringBind = () => {
                    Expect<InvalidOperationException>(() => f.Tree.Toggle(10000));
                    Expect<InvalidOperationException>(() => f.Tree.Submit(f.Roots));
                    Expect<InvalidOperationException>(() => f.Tree.Dispose());
                };
                f.Tree.Submit(f.Roots);
                f.Adapter.DuringBind = null;
                f.Adapter.FailBind = true;
                Expect<InvalidOperationException>(() => f.Tree.Submit(f.Roots));
                Require(!f.View.IsInitialized, "Failed bind left a partially active renderer");
                f.Adapter.FailBind = false;
                f.Tree.Submit(f.Roots);
                Require(f.Tree.ScrollToNode(80108), "Cannot recover after binding failure");
            }
        }

        private static void CheckCallbackLifecycle()
        {
            using (var f = new Fixture(render: false)) {
                Require(f.Tree.RowCount == 224 && f.View.VisibleItems.Count == 0 && f.Adapter.Created == 0,
                    "Submitting before a render subscription created unbound views");
                int coreRenders = 0;
                int coreRecycles = 0;
                f.View.OnItemRender += (view, index) => ++coreRenders;
                f.View.OnItemRecycle += (view, index) => ++coreRecycles;
                f.Tree.OnItemRender += f.Adapter.Render;
                Require(f.View.VisibleItems.Count > 0 && f.Adapter.Binds > 0, "Late render subscription did not populate the tree");
                Require(f.Tree.ScrollToNode(80108, duration: 1f) && f.View.IsJumping, "Animated navigation did not start");
                int created = f.Adapter.Created;
                int recycled = f.Adapter.Recycled;
                f.Tree.OnItemRender -= f.Adapter.Render;
                Require(f.View.VisibleItems.Count == 0 && f.Adapter.Recycled > recycled && f.Adapter.Destroyed == 0,
                    "Removing the last renderer did not recycle views into the existing pool");
                Require(!f.View.IsJumping, "Removing the last renderer left its animated navigation active");
                int suspendedBinds = f.Adapter.Binds;
                typeof(VirtualScrollView).GetMethod("AdvanceJump", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.View, new object[] { 1f });
                Require(f.Adapter.Created == created && f.Adapter.Binds == suspendedBinds && f.View.VisibleItems.Count == 0,
                    "Advancing obsolete navigation created or rebound views without a renderer");
                f.Tree.OnItemRender += f.Adapter.Render;
                Require(f.View.VisibleItems.Count > 0 && f.Adapter.Created == created, "Resubscribing discarded compatible pooled views");

                int observerCalls = 0;
                Action<ScrollItemView, TreeListRow<Node, int>> observer = (view, row) => ++observerCalls;
                int binds = f.Adapter.Binds;
                f.Tree.OnItemRender += observer;
                Require(observerCalls > 0 && observerCalls == f.Adapter.Binds - binds,
                    "Render subscribers did not receive exactly one call per binding");
                f.Tree.OnItemRender -= observer;
                observerCalls = 0;
                f.Tree.Submit(f.Roots);
                Require(observerCalls == 0, "Removed renderer still receives bindings");

                Node previous = f.Roots[0];
                f.Roots[0] = new Node { Id = previous.Id, Children = previous.Children, Value = 17 };
                f.Tree.Submit(f.Roots);
                Require(f.View.TryGetVisibleItem(0, out var holder) && ReferenceEquals(((Holder)holder).Node, f.Roots[0]),
                    "Same-key replacement retained the old payload");
                Require(coreRenders == 0 && coreRecycles == 0, "Tree binding also invoked the underlying list callbacks");
                f.Tree.Dispose();
                Require(f.Adapter.Destroyed == f.Adapter.Created, "Dispose did not destroy each owned view exactly once");
            }
        }

        private static void CheckCallbackConfiguration()
        {
            using (var f = new Fixture()) {
                Expect<InvalidOperationException>(() => f.Tree.ViewFactory = root => new Holder(root));
                Expect<InvalidOperationException>(() => f.Tree.OnItemRecycle -= f.Adapter.Unbind);
                Expect<InvalidOperationException>(() => f.Tree.OnItemRecycle += (view, row) => { });
                Expect<InvalidOperationException>(() => f.Tree.OnViewDestroyed -= f.Adapter.DestroyView);
                Expect<InvalidOperationException>(() => f.Tree.OnViewDestroyed += view => { });

                f.Adapter.DuringBind = () => {
                    Expect<InvalidOperationException>(() => f.Tree.EstimatedItemSize = 45f);
                    Expect<InvalidOperationException>(() => f.Tree.ItemPrefabSelector = f.Adapter.GetItemPrefab);
                    Expect<InvalidOperationException>(() => f.Tree.ItemSizeProvider = f.Adapter.TryGetItemSize);
                    Expect<InvalidOperationException>(() => f.Tree.ItemMeasurer = f.Adapter.MeasureItem);
                    Expect<InvalidOperationException>(() => f.Tree.OnItemRender -= f.Adapter.Render);
                    Expect<InvalidOperationException>(() => f.Tree.OnItemRender += f.Adapter.Render);
                };
                f.Tree.Submit(f.Roots);
                f.Adapter.DuringBind = null;
                f.Adapter.DuringRecycle = () => {
                    Expect<InvalidOperationException>(() => f.Tree.Submit(f.Roots));
                    Expect<InvalidOperationException>(() => f.Tree.OnItemRender -= f.Adapter.Render);
                };
                f.Tree.Submit(f.Roots);
                f.Adapter.DuringRecycle = null;

                f.Tree.EstimatedItemSize = 45f;
                f.Tree.ItemPrefabSelector = row => f.Adapter.AlternativePrefab;
                f.Tree.ItemSizeProvider = null;
                f.Tree.ItemMeasurer = (view, row, context) => 47f;
                f.Tree.Submit(f.Roots, ScrollUpdatePosition.Start);
                Require(f.View.TryGetVisibleItem(0, out var holder) && holder.Prefab == f.Adapter.AlternativePrefab && holder.ItemSize == 47f,
                    "Changing content policies outside callbacks was ignored");

                f.Adapter.FailBind = true;
                Expect<InvalidOperationException>(() => f.Tree.Submit(f.Roots));
                Require(!f.View.IsInitialized, "Failed renderer remained active");
                f.Adapter.FailBind = false;
                f.Tree.ViewFactory = f.Adapter.CreateView;
                f.Tree.OnItemRecycle -= f.Adapter.Unbind;
                f.Tree.OnItemRecycle += f.Adapter.Unbind;
                f.Tree.OnViewDestroyed -= f.Adapter.DestroyView;
                f.Tree.OnViewDestroyed += f.Adapter.DestroyView;
                f.Tree.Submit(f.Roots);
                Require(f.View.VisibleItems.Count > 0, "Renderer could not be reconfigured after failure");
            }
            using (var f = new Fixture(submit: false)) {
                f.Tree.ViewFactory = f.Adapter.CreateView;
                f.Tree.OnItemRecycle -= f.Adapter.Unbind;
                f.Tree.OnItemRecycle += f.Adapter.Unbind;
                f.Tree.OnViewDestroyed -= f.Adapter.DestroyView;
                f.Tree.OnViewDestroyed += f.Adapter.DestroyView;
                f.Tree.Submit(f.Roots);
                Require(f.Adapter.Binds > 0, "Pre-initialization lifecycle configuration failed");
            }
            using (var f = new Fixture(render: false, submit: false)) {
                Expect<ArgumentNullException>(() => new TreeListController<Node, int>(f.View, null, f.Adapter.GetChildren));
                Expect<ArgumentNullException>(() => new TreeListController<Node, int>(f.View, f.Adapter.GetKey, null));
                float estimate = f.Tree.EstimatedItemSize;
                Expect<ArgumentOutOfRangeException>(() => f.Tree.EstimatedItemSize = 0f);
                Expect<ArgumentOutOfRangeException>(() => f.Tree.EstimatedItemSize = -1f);
                Expect<ArgumentOutOfRangeException>(() => f.Tree.EstimatedItemSize = float.NaN);
                Expect<ArgumentOutOfRangeException>(() => f.Tree.EstimatedItemSize = float.PositiveInfinity);
                Require(f.Tree.EstimatedItemSize == estimate, "Rejected estimate changed the active policy");
                f.Tree.OnItemRender += null;
                f.Tree.OnItemRender -= null;
                f.View.ItemPrefabs = new[] { f.Adapter.Prefab };
                f.Tree.ItemPrefabSelector = null;
                f.Tree.ViewFactory = null;
                f.Tree.ItemSizeProvider = null;
                f.Tree.ItemMeasurer = null;
                f.Tree.OnItemRecycle -= f.Adapter.Unbind;
                int renders = 0;
                f.Tree.OnItemRender += (view, row) => ++renders;
                f.Tree.Submit(f.Roots);
                Require(renders > 0 && f.View.TryGetVisibleItem(0, out var holder) && holder.GetType() == typeof(ScrollItemView) &&
                    holder.Prefab == f.Adapter.Prefab && holder.ItemSize > 0f, "Default single-prefab rendering requires custom callbacks");
            }
        }

        private static void CheckAdapterIntegration()
        {
            using (var f = new Fixture(useAdapter: true, submit: false)) {
                Expect<ArgumentNullException>(() => new TreeListController<Node, int>(f.View, null));
                // Adapter 独占显示配置，避免两个入口同时绑定或清理同一视图。
                Expect<InvalidOperationException>(() => f.Tree.EstimatedItemSize = 45f);
                Expect<InvalidOperationException>(() => f.Tree.ItemPrefabSelector = f.Adapter.GetItemPrefab);
                Expect<InvalidOperationException>(() => f.Tree.ViewFactory = f.Adapter.CreateView);
                Expect<InvalidOperationException>(() => f.Tree.ItemSizeProvider = f.Adapter.TryGetItemSize);
                Expect<InvalidOperationException>(() => f.Tree.ItemMeasurer = f.Adapter.MeasureItem);
                Expect<InvalidOperationException>(() => f.Tree.OnItemRender += f.Adapter.Render);
                Expect<InvalidOperationException>(() => f.Tree.OnItemRender -= f.Adapter.Render);
                Expect<InvalidOperationException>(() => f.Tree.OnItemRecycle += f.Adapter.Unbind);
                Expect<InvalidOperationException>(() => f.Tree.OnItemRecycle -= f.Adapter.Unbind);
                Expect<InvalidOperationException>(() => f.Tree.OnViewDestroyed += f.Adapter.DestroyView);
                Expect<InvalidOperationException>(() => f.Tree.OnViewDestroyed -= f.Adapter.DestroyView);
                foreach (float estimate in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) {
                    f.Adapter.Estimate = estimate;
                    Expect<InvalidOperationException>(() => f.Tree.Submit(f.Roots));
                    Require(f.Tree.RowCount == 0 && !f.View.IsInitialized, "Invalid adapter estimate committed data");
                }
                f.Adapter.Estimate = 32f;
                f.Tree.Submit(f.Roots);
                Require(f.View.VisibleItems.Count > 0 && f.Tree.EstimatedItemSize == 32f, "Adapter did not render without event subscribers");
                f.Adapter.Estimate = float.NaN;
                int rows = f.Tree.RowCount;
                int binds = f.Adapter.Binds;
                Expect<InvalidOperationException>(() => f.Tree.Submit(f.Roots));
                Require(f.Tree.RowCount == rows && f.Adapter.Binds == binds && f.View.IsInitialized,
                    "Rejected adapter configuration changed the committed renderer");
                f.Adapter.Estimate = 32f;
                // 无页面状态的 Adapter 可以共享；控制器的展开状态和物理视图仍各自独立。
                using (var second = new Fixture(useAdapter: true, adapter: f.Adapter)) {
                    second.Tree.SetExpanded(10000, false);
                    Require(f.Tree.IsExpanded(10000) && !second.Tree.IsExpanded(10000), "Shared adapter leaked expansion state");
                    Require(f.View.TryGetVisibleItem(0, out var firstView) && second.View.TryGetVisibleItem(0, out var secondView) &&
                        firstView.Root != secondView.Root, "Shared adapter reused a physical view across lists");
                }
                Require(f.Tree.ScrollToNode(80108) && f.View.VisibleItems.Count > 0, "Disposing one controller invalidated a shared adapter");
                f.Tree.Dispose();
                Require(f.Adapter.Created == f.Adapter.Destroyed, "Shared adapter did not release every owned view exactly once");
            }
        }

        private static void CheckExternalDisposal(bool useAdapter)
        {
            using (var f = new Fixture(useAdapter: useAdapter)) {
                int visible = f.View.VisibleItems.Count;
                int recycleAttempts = 0;
                int destroyAttempts = 0;
                f.Adapter.DuringRecycle = () => {
                    ++recycleAttempts;
                    Expect<InvalidOperationException>(() => f.Tree.Dispose());
                };
                f.Adapter.DuringDestroy = () => {
                    ++destroyAttempts;
                    Expect<InvalidOperationException>(() => f.Tree.Dispose());
                };
                f.View.Dispose();
                Require(!f.View.IsInitialized && f.View.VisibleItems.Count == 0 && recycleAttempts == visible &&
                    destroyAttempts == f.Adapter.Created && f.Adapter.Destroyed == f.Adapter.Created,
                    "Reentrant controller disposal interrupted external view cleanup");
            }
        }

        private static void CheckAllocations(bool useAdapter)
        {
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1000, ProfilerRecorderOptions.CollectOnlyOnCurrentThread)) {
                s_allocation = new byte[123];
                recorder.Stop();
                Require(recorder.Valid && recorder.Count > 0, "Allocation recorder not observing allocations");
            }
            string mode = useAdapter ? "adapter_" : "callback_";
            using (var f = new Fixture(roots: 40, branches: 6, leaves: 8, useAdapter: useAdapter)) {
                f.Tree.NodeComparison = (a, b) => b.Value.CompareTo(a.Value);
                f.Tree.ChildComparison = (p, a, b) => b.Value.CompareTo(a.Value);
                Require(f.Tree.NodeCount == 2200, "Benchmark tree size");
                Measure(mode + "submit_2200_nodes", 100, () => f.Tree.Submit(f.Roots));
                Measure(mode + "toggle_2200_nodes", 200, () => f.Tree.Toggle(10000));
                Measure(mode + "scroll_cross_range", 1000, () => f.Scroll(f.View.ScrollOffset > 200f ? 150f : 250f));
                int reads = f.Adapter.Reads;
                Measure(mode + "scroll_same_range", 1000, () => f.Scroll(151f));
                Require(reads == f.Adapter.Reads, "Scroll reread business collections");
                int queries = f.Adapter.SizeQueries;
                f.Tree.Submit(f.Roots);
                Require(f.Adapter.SizeQueries - queries < 100, "Submit measured all nodes eagerly");
                Measure(mode + "collapse_and_reveal_path", 100, () => { f.Tree.SetAllExpanded(false); f.Tree.ScrollToNode(20101); });
                Measure(mode + "hidden_branch_toggle", 200, () => f.Tree.Toggle(10100));
            }
            GC.KeepAlive(s_allocation);
        }

        private static void Measure(string name, int count, Action action)
        {
            for (int i = 0; i < 12; ++i) action();
            var timer = new Stopwatch();
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 100000, ProfilerRecorderOptions.CollectOnlyOnCurrentThread)) {
                timer.Start();
                for (int i = 0; i < count; ++i) action();
                timer.Stop();
                recorder.Stop();
                Require(recorder.Valid && recorder.Count < recorder.Capacity, "Allocation recorder invalid or full");
                int allocations = recorder.Count;
                Debug.Log("TREE_PERFORMANCE name=" + name + " count=" + count + " gc_alloc_events=" + allocations + " elapsed_ms=" + timer.Elapsed.TotalMilliseconds);
                Require(allocations == 0, name + " allocated after warmup");
            }
        }

        private static void Require(bool condition, string message)
        {
            ++s_checks;
            if (!condition) throw new Exception(message);
        }

        private static void Expect<T>(Action action) where T: Exception
        {
            try { action(); }
            catch (T) { ++s_checks; return; }
            throw new Exception("Expected " + typeof(T).Name);
        }
    }
}
