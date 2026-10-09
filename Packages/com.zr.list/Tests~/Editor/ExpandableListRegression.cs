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
    /// <summary>Copy Tests~/Editor into a consumer project's Assets to run without NUnit dependencies.</summary>
    public static class ExpandableListRegression
    {
        private sealed class Group
        {
            public int Id;
            public int Priority;
            public readonly List<Item> Items = new List<Item>();
        }

        private sealed class Item
        {
            public int Id;
            public int Value;
            public float Size = 30f;
        }

        private sealed class Holder: ScrollItemView
        {
            public bool Header;
            public int Group;
            public int Key;
            public int Value;
            public Group GroupData;
            public Item ItemData;
            public Holder(RectTransform root): base(root, -1, -1) { }
        }

        private sealed class TestAdapter: ExpandableListAdapter<Group, Item, int>
        {
            public RectTransform HeaderPrefab;
            public RectTransform ItemPrefab;
            public RectTransform AlternativePrefab;
            public bool KnownSize = true;
            public bool Alternative;
            public int Created;
            public int Binds;
            public int Unbinds;
            public int Destroyed;
            public int Reads;
            public int SizeQueries;
            public Action DuringBind;
            public Action DuringRecycle;
            public Action DuringDestroy;
            public bool FailBind;
            public bool FailUnbind;

            public float Estimate = 30f;
            public override float EstimatedItemSize { get { return Estimate; } }
            public override int GetGroupKey(Group group) { return group.Id; }
            public override int GetItemKey(Item item) { return item.Id; }
            public override IReadOnlyList<Item> GetItems(Group group) { ++Reads; return group.Items; }
            public override RectTransform GetItemPrefab(ExpandableListRow<Group, Item, int> row)
            {
                return row.IsHeader ? HeaderPrefab : Alternative ? AlternativePrefab : ItemPrefab;
            }
            public override ScrollItemView CreateView(RectTransform root)
            {
                ++Created;
                return new Holder(root);
            }
            public void Render(ScrollItemView view, ExpandableListRow<Group, Item, int> row)
            {
                Bind(view, row, default);
            }

            public override void Bind(ScrollItemView view, ExpandableListRow<Group, Item, int> row, ScrollItemLayoutContext context)
            {
                ++Binds;
                var holder = (Holder)view;
                holder.Header = row.IsHeader;
                holder.Group = row.GroupKey;
                holder.Key = row.IsHeader ? row.GroupKey : row.ItemKey;
                holder.Value = row.IsHeader ? row.Group.Priority : row.Item.Value;
                holder.GroupData = row.Group;
                holder.ItemData = row.Item;
                DuringBind?.Invoke();
                if (FailBind) throw new InvalidOperationException("Expected binding failure");
            }
            public override bool TryGetItemSize(ExpandableListRow<Group, Item, int> row, ScrollItemLayoutContext context, out float size)
            {
                ++SizeQueries;
                size = row.IsHeader ? (row.IsExpanded ? 40f : 35f) : row.Item.Size;
                return KnownSize;
            }
            public override float MeasureItem(ScrollItemView view, ExpandableListRow<Group, Item, int> row, ScrollItemLayoutContext context)
            {
                return row.IsHeader ? (row.IsExpanded ? 40f : 35f) : row.Item.Size;
            }
            public override void Unbind(ScrollItemView view, ExpandableListRow<Group, Item, int> row)
            {
                ++Unbinds;
                var holder = (Holder)view;
                Require(holder.Header == row.IsHeader && holder.Group == row.GroupKey && holder.Key == (row.IsHeader ? row.GroupKey : row.ItemKey) &&
                    ReferenceEquals(holder.GroupData, row.Group) && ReferenceEquals(holder.ItemData, row.Item),
                    "Unbind used the new snapshot instead of the bound identity");
                DuringRecycle?.Invoke();
                if (FailUnbind) throw new InvalidOperationException("Expected unbinding failure");
            }
            public override void DestroyView(ScrollItemView view) { ++Destroyed; DuringDestroy?.Invoke(); }
        }

        private sealed class Fixture: IDisposable
        {
            public readonly VirtualScrollView View;
            public readonly TestAdapter Adapter;
            public readonly ExpandableListController<Group, Item, int> List;
            public readonly List<Group> Groups = new List<Group>();

            public Fixture(bool horizontal = false, bool known = true, int groups = 12, int children = 12,
                bool render = true, bool submit = true, bool useAdapter = false, TestAdapter adapter = null)
            {
                var root = new GameObject("Expandable regression", typeof(RectTransform), typeof(VirtualScrollRect), typeof(VirtualScrollView));
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
                Adapter = adapter ?? new TestAdapter
                {
                    HeaderPrefab = Prefab(viewport, "Header"),
                    ItemPrefab = Prefab(viewport, "Item"),
                    AlternativePrefab = Prefab(viewport, "Alternative"),
                    KnownSize = known
                };
                if (useAdapter) {
                    List = new ExpandableListController<Group, Item, int>(View, Adapter);
                }
                else {
                    List = new ExpandableListController<Group, Item, int>(View, Adapter.GetGroupKey, Adapter.GetItems, Adapter.GetItemKey)
                    {
                        EstimatedItemSize = Adapter.EstimatedItemSize,
                        ItemPrefabSelector = Adapter.GetItemPrefab,
                        ViewFactory = Adapter.CreateView,
                        ItemSizeProvider = Adapter.TryGetItemSize,
                        ItemMeasurer = Adapter.MeasureItem
                    };
                    List.OnItemRecycle += Adapter.Unbind;
                    List.OnViewDestroyed += Adapter.DestroyView;
                    if (render) List.OnItemRender += Adapter.Render;
                }
                List.DefaultExpanded = true;
                for (int g = 0; g < groups; ++g) {
                    var group = new Group { Id = g + 1 };
                    for (int i = 0; i < children; ++i) group.Items.Add(new Item { Id = (g + 1) * 1000 + i, Value = i });
                    Groups.Add(group);
                }
                if (submit) List.Submit(Groups);
            }

            private static RectTransform Prefab(RectTransform parent, string name)
            {
                var root = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                root.SetParent(parent, false);
                root.sizeDelta = new Vector2(30f, 30f);
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

            public Holder FirstVisible()
            {
                foreach (ScrollItemView holder in View.VisibleItems) {
                    if (Inset(holder) < 240f && Inset(holder) + holder.ItemSize > 0f) return (Holder)holder;
                }
                throw new Exception("No visible row");
            }

            public void Dispose()
            {
                Adapter.DuringBind = null;
                Adapter.DuringRecycle = null;
                Adapter.DuringDestroy = null;
                Adapter.FailBind = Adapter.FailUnbind = false;
                List.Dispose();
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
                CheckProjectionAndValidation(useAdapter);
                foreach (bool horizontal in new[] { false, true }) {
                    foreach (bool known in new[] { false, true }) {
                        CheckReorderAndContent(horizontal, known, useAdapter);
                        CheckCollapseAndNavigation(horizontal, known, useAdapter);
                    }
                }
                CheckReentrancyAndRecovery(useAdapter);
                CheckCoreMapValidation(useAdapter);
                CheckExternalDisposal(useAdapter);
                CheckAllocations(useAdapter);
            }
            CheckCallbackLifecycle();
            CheckCallbackConfiguration();
            CheckAdapterIntegration();
            Debug.Log("EXPANDABLE_REGRESSION_PASSED checks=" + s_checks);
        }

        private static void CheckProjectionAndValidation(bool useAdapter)
        {
            using (var f = new Fixture(useAdapter: useAdapter)) {
                Require(f.List.RowCount == 156, "Expanded row count");
                int reads = f.Adapter.Reads;
                f.List.SetExpanded(2, false);
                Require(f.List.RowCount == 144 && !f.List.TryGetItemIndex(2000, out _), "Collapsed rows remain projected");
                Require(f.Adapter.Reads == reads, "Toggle reread business collections");
                f.List.GroupComparison = (a, b) => b.Priority.CompareTo(a.Priority);
                f.List.ItemComparison = (g, a, b) => b.Value.CompareTo(a.Value);
                f.Groups[1].Priority = 1;
                f.List.Submit(f.Groups);
                Require(f.List.TryGetRow(0, out var first) && first.GroupKey == 2 && !first.IsExpanded, "Sorted group lost expansion");
                f.List.SetExpanded(2, true);
                Require(f.List.TryGetRow(1, out first) && first.ItemKey == 2011, "Child comparator not applied");
                Require(f.Groups[1].Items[0].Id == 2000 && f.Groups[0].Id == 1, "Sorting mutated caller collections");
                f.List.SetAllExpanded(false);
                int count = f.List.RowCount;
                f.Groups[1].Items.Add(f.Groups[0].Items[0]);
                Expect<ArgumentException>(() => f.List.Submit(f.Groups));
                Require(f.List.RowCount == count && f.View.IsInitialized, "Rejected duplicate changed committed state");
                f.Groups[1].Items.RemoveAt(f.Groups[1].Items.Count - 1);
                f.Groups.Add(f.Groups[0]);
                Expect<ArgumentException>(() => f.List.Submit(f.Groups));
                f.Groups.RemoveAt(f.Groups.Count - 1);
                f.List.GroupComparison = (a, b) => { throw new InvalidOperationException("Expected comparison failure"); };
                Expect<InvalidOperationException>(() => f.List.Submit(f.Groups));
                Require(f.List.RowCount == count, "Comparison failure changed state");
                f.List.GroupComparison = (a, b) => 0;
                f.List.ItemComparison = null;
                f.List.Submit(f.Groups);
                Require(f.List.TryGetRow(0, out first) && first.GroupKey == 1, "Ties must preserve input order");
                f.Groups.Clear();
                f.List.Submit(f.Groups);
                Require(f.List.RowCount == 0 && f.View.ItemCount == 0 && f.View.VisibleItems.Count == 0, "Empty data");
                f.Groups.Add(new Group { Id = 99 });
                f.List.Submit(f.Groups);
                Require(f.List.RowCount == 1 && f.List.IsExpanded(99), "Empty group or default expansion");
            }
        }

        private static void CheckReorderAndContent(bool horizontal, bool known, bool useAdapter)
        {
            using (var f = new Fixture(horizontal, known, useAdapter: useAdapter)) {
                f.List.ScrollToItem(5003);
                f.Scroll(f.View.ScrollOffset + 11f);
                Holder anchor = f.FirstVisible();
                int key = anchor.Key;
                float inset = f.Inset(anchor);
                Require(!anchor.Header, "Expected child anchor");
                Require(f.View.TryGetItemBinding(anchor.Root.gameObject, out ScrollItemBinding binding), "Missing binding");
                f.Groups.Reverse();
                f.List.Submit(f.Groups);
                ScrollItemView holder = null;
                Require(f.List.TryGetItemIndex(key, out int row) && f.View.TryGetVisibleItem(row, out holder) && ReferenceEquals(anchor, holder), "Moved visible item lost its physical view");
                Require(Mathf.Abs(f.Inset(anchor) - inset) < 0.1f, "Reorder moved the anchor on screen");
                Require(!f.View.SetItemSize(binding, 99f), "Old binding remained valid");
                Group owner = f.Groups.Find(g => g.Id == 5);
                Item item = owner.Items.Find(i => i.Id == key);
                item.Value = 123;
                item.Size = 85f;
                f.List.Submit(f.Groups);
                Require(f.List.TryGetItemIndex(key, out row) && f.View.TryGetVisibleItem(row, out holder) && ((Holder)holder).Value == 123 && holder.ItemSize == 85f, "Same key/reference hid changed content or size");
                owner.Items.Remove(item);
                Group destination = f.Groups.Find(g => g.Id == 6);
                destination.Items.Insert(0, item);
                f.List.Submit(f.Groups);
                Require(f.List.TryGetItemIndex(key, out row) && f.View.TryGetVisibleItem(row, out holder) && ((Holder)holder).Group == 6, "Cross-group move lost global item identity");
                Require(Mathf.Abs(f.Inset(holder) - inset) < 0.1f, "Cross-group move lost anchor");
                f.Adapter.Alternative = true;
                f.List.Submit(f.Groups);
                Require(f.View.TryGetVisibleItem(row, out holder) && holder.Prefab == f.Adapter.AlternativePrefab, "Same-key prefab replacement failed");
                f.List.Submit(f.Groups, ScrollUpdatePosition.Start);
                Require(Mathf.Abs(f.View.ScrollOffset) < 0.1f, "Start policy");
                f.Scroll(60f);
                f.List.Submit(f.Groups, ScrollUpdatePosition.KeepOffset);
                Require(Mathf.Abs(f.View.ScrollOffset - 60f) < 0.1f, "KeepOffset policy");
            }
        }

        private static void CheckCollapseAndNavigation(bool horizontal, bool known, bool useAdapter)
        {
            using (var f = new Fixture(horizontal, known, useAdapter: useAdapter)) {
                f.List.ScrollToItem(5010);
                f.List.SetExpanded(5, false);
                Require(f.List.TryGetHeaderIndex(5, out int header) && f.View.IsItemVisible(header), "Hidden anchor did not fall back to its group header");
                Require(!f.List.ScrollToItem(5001, false), "Hidden navigation ignored expandIfNeeded");
                Require(!f.List.ScrollToItem(5001, true, float.NaN) && !f.List.IsExpanded(5), "Invalid duration changed expansion state");
                Require(f.List.ScrollToItem(5001) && f.List.IsExpanded(5) && f.List.TryGetItemIndex(5001, out int index) && f.View.IsItemVisible(index), "Automatic expansion/navigation");
                f.List.ScrollToGroup(5);
                f.Scroll(f.View.ScrollOffset - 65f);
                Require(f.View.TryGetVisibleItem(header, out var holder), "Preferred header missing");
                float inset = f.Inset(holder);
                f.List.SetExpanded(5, false);
                Require(f.List.TryGetHeaderIndex(5, out header) && f.View.TryGetVisibleItem(header, out holder) && Mathf.Abs(f.Inset(holder) - inset) < 0.1f, "Visible clicked header shifted");
                f.List.ScrollToItem(12011);
                f.List.SetAllExpanded(false);
                Require(f.List.RowCount == 12 && f.View.ScrollOffset >= 0f && f.View.VisibleItems.Count > 0, "Collapse at end broke boundaries");
                f.List.SetAllExpanded(true);
                f.View.gameObject.SetActive(false);
                f.Groups.Reverse();
                f.List.Submit(f.Groups);
                f.View.gameObject.SetActive(true);
                f.View.RefreshVisibleItems();
                Require(f.List.ScrollToItem(2001), "Disabled submission did not recover");
            }
        }

        private static void CheckReentrancyAndRecovery(bool useAdapter)
        {
            using (var f = new Fixture(useAdapter: useAdapter)) {
                f.Adapter.DuringBind = () => {
                    Expect<InvalidOperationException>(() => f.List.Submit(f.Groups));
                    Expect<InvalidOperationException>(() => f.List.Dispose());
                };
                f.View.RefreshVisibleItems();
                f.Adapter.DuringBind = null;
                Require(f.List.ScrollToItem(2000), "Rejected reentrancy corrupted the controller");
                f.Adapter.FailBind = true;
                Expect<InvalidOperationException>(() => f.List.Submit(f.Groups));
                Require(!f.View.IsInitialized, "Failed binding left a partially active list");
                f.Adapter.FailBind = false;
                f.List.Submit(f.Groups);
                Require(f.View.IsInitialized && f.List.ScrollToItem(3000), "Cannot recover after a failed commit");
                f.Adapter.FailUnbind = true;
                Expect<InvalidOperationException>(() => f.List.Submit(f.Groups));
                f.Adapter.FailUnbind = false;
                f.List.Submit(f.Groups);
                Require(f.View.IsInitialized, "Cannot recover after failed old unbinding");
            }
        }

        private static void CheckCoreMapValidation(bool useAdapter)
        {
            using (var f = new Fixture(useAdapter: useAdapter)) {
                bool committed = false;
                Action commit = () => committed = true;
                Expect<ArgumentException>(() => f.View.ApplyData(new[] { new ScrollItemUpdate(1), new ScrollItemUpdate(1) }, commit));
                Expect<ArgumentException>(() => f.View.ApplyData(new[] { new ScrollItemUpdate(-2) }, commit));
                Require(!committed && f.View.ItemCount == 156, "Invalid core map committed data");
            }
        }

        private static void CheckCallbackLifecycle()
        {
            using (var f = new Fixture(render: false)) {
                Require(f.List.RowCount == 156 && f.View.VisibleItems.Count == 0 && f.Adapter.Created == 0,
                    "Submitting before a render subscription created unbound views");
                int coreRenders = 0;
                int coreRecycles = 0;
                f.View.OnItemRender += (view, index) => ++coreRenders;
                f.View.OnItemRecycle += (view, index) => ++coreRecycles;
                f.List.OnItemRender += f.Adapter.Render;
                Require(f.View.VisibleItems.Count > 0 && f.Adapter.Binds > 0, "Late render subscription did not populate the list");
                Require(f.List.ScrollToItem(12011, duration: 1f) && f.View.IsJumping, "Animated navigation did not start");
                int created = f.Adapter.Created;
                int recycled = f.Adapter.Unbinds;
                f.List.OnItemRender -= f.Adapter.Render;
                Require(f.View.VisibleItems.Count == 0 && f.Adapter.Unbinds > recycled && f.Adapter.Destroyed == 0,
                    "Removing the last renderer did not preserve the view pools");
                Require(!f.View.IsJumping, "Removing the last renderer left its animated navigation active");
                int suspendedBinds = f.Adapter.Binds;
                typeof(VirtualScrollView).GetMethod("AdvanceJump", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.View, new object[] { 1f });
                Require(f.Adapter.Created == created && f.Adapter.Binds == suspendedBinds && f.View.VisibleItems.Count == 0,
                    "Advancing obsolete navigation created or rebound views without a renderer");
                f.List.OnItemRender += f.Adapter.Render;
                Require(f.View.VisibleItems.Count > 0 && f.Adapter.Created == created, "Resubscribing discarded compatible pooled views");

                int observerCalls = 0;
                Action<ScrollItemView, ExpandableListRow<Group, Item, int>> observer = (view, row) => ++observerCalls;
                int binds = f.Adapter.Binds;
                f.List.OnItemRender += observer;
                Require(observerCalls > 0 && observerCalls == f.Adapter.Binds - binds,
                    "Render subscribers did not receive exactly one call per binding");
                f.List.OnItemRender -= observer;
                observerCalls = 0;
                f.List.Submit(f.Groups);
                Require(observerCalls == 0, "Removed renderer still receives bindings");

                var replacement = new Group { Id = f.Groups[0].Id, Priority = 3 };
                replacement.Items.AddRange(f.Groups[0].Items);
                replacement.Items[0] = new Item { Id = replacement.Items[0].Id, Value = 27 };
                f.Groups[0] = replacement;
                f.List.Submit(f.Groups);
                Require(f.View.TryGetVisibleItem(1, out var holder) && ReferenceEquals(((Holder)holder).GroupData, replacement) &&
                    ReferenceEquals(((Holder)holder).ItemData, replacement.Items[0]), "Same-key replacement retained the old payload");
                Require(coreRenders == 0 && coreRecycles == 0, "Controller binding also invoked underlying list callbacks");
                f.List.Dispose();
                Require(f.Adapter.Destroyed == f.Adapter.Created, "Dispose did not destroy each owned view exactly once");
            }
        }

        private static void CheckCallbackConfiguration()
        {
            using (var f = new Fixture()) {
                Expect<InvalidOperationException>(() => f.List.ViewFactory = root => new Holder(root));
                Expect<InvalidOperationException>(() => f.List.OnItemRecycle -= f.Adapter.Unbind);
                Expect<InvalidOperationException>(() => f.List.OnItemRecycle += (view, row) => { });
                Expect<InvalidOperationException>(() => f.List.OnViewDestroyed -= f.Adapter.DestroyView);
                Expect<InvalidOperationException>(() => f.List.OnViewDestroyed += view => { });

                f.Adapter.DuringBind = () => {
                    Expect<InvalidOperationException>(() => f.List.EstimatedItemSize = 45f);
                    Expect<InvalidOperationException>(() => f.List.ItemPrefabSelector = f.Adapter.GetItemPrefab);
                    Expect<InvalidOperationException>(() => f.List.ItemSizeProvider = f.Adapter.TryGetItemSize);
                    Expect<InvalidOperationException>(() => f.List.ItemMeasurer = f.Adapter.MeasureItem);
                    Expect<InvalidOperationException>(() => f.List.OnItemRender -= f.Adapter.Render);
                    Expect<InvalidOperationException>(() => f.List.OnItemRender += f.Adapter.Render);
                };
                f.List.Submit(f.Groups);
                f.Adapter.DuringBind = null;
                f.Adapter.DuringRecycle = () => {
                    Expect<InvalidOperationException>(() => f.List.Submit(f.Groups));
                    Expect<InvalidOperationException>(() => f.List.OnItemRender -= f.Adapter.Render);
                };
                f.List.Submit(f.Groups);
                f.Adapter.DuringRecycle = null;

                f.List.EstimatedItemSize = 45f;
                f.List.ItemPrefabSelector = row => f.Adapter.AlternativePrefab;
                f.List.ItemSizeProvider = null;
                f.List.ItemMeasurer = (view, row, context) => 47f;
                f.List.Submit(f.Groups, ScrollUpdatePosition.Start);
                Require(f.View.TryGetVisibleItem(0, out var holder) && holder.Prefab == f.Adapter.AlternativePrefab && holder.ItemSize == 47f,
                    "Changing content policies outside callbacks was ignored");

                f.Adapter.FailBind = true;
                Expect<InvalidOperationException>(() => f.List.Submit(f.Groups));
                Require(!f.View.IsInitialized, "Failed renderer remained active");
                f.Adapter.FailBind = false;
                f.List.ViewFactory = f.Adapter.CreateView;
                f.List.OnItemRecycle -= f.Adapter.Unbind;
                f.List.OnItemRecycle += f.Adapter.Unbind;
                f.List.OnViewDestroyed -= f.Adapter.DestroyView;
                f.List.OnViewDestroyed += f.Adapter.DestroyView;
                f.List.Submit(f.Groups);
                Require(f.View.VisibleItems.Count > 0, "Renderer could not be reconfigured after failure");
            }
            using (var f = new Fixture(submit: false)) {
                f.List.ViewFactory = f.Adapter.CreateView;
                f.List.OnItemRecycle -= f.Adapter.Unbind;
                f.List.OnItemRecycle += f.Adapter.Unbind;
                f.List.OnViewDestroyed -= f.Adapter.DestroyView;
                f.List.OnViewDestroyed += f.Adapter.DestroyView;
                f.List.Submit(f.Groups);
                Require(f.Adapter.Binds > 0, "Pre-initialization lifecycle configuration failed");
            }
            using (var f = new Fixture(render: false, submit: false)) {
                Expect<ArgumentNullException>(() => new ExpandableListController<Group, Item, int>(f.View, null, f.Adapter.GetItems, f.Adapter.GetItemKey));
                Expect<ArgumentNullException>(() => new ExpandableListController<Group, Item, int>(f.View, f.Adapter.GetGroupKey, null, f.Adapter.GetItemKey));
                Expect<ArgumentNullException>(() => new ExpandableListController<Group, Item, int>(f.View, f.Adapter.GetGroupKey, f.Adapter.GetItems, null));
                float estimate = f.List.EstimatedItemSize;
                Expect<ArgumentOutOfRangeException>(() => f.List.EstimatedItemSize = 0f);
                Expect<ArgumentOutOfRangeException>(() => f.List.EstimatedItemSize = -1f);
                Expect<ArgumentOutOfRangeException>(() => f.List.EstimatedItemSize = float.NaN);
                Expect<ArgumentOutOfRangeException>(() => f.List.EstimatedItemSize = float.PositiveInfinity);
                Require(f.List.EstimatedItemSize == estimate, "Rejected estimate changed the active policy");
                f.List.OnItemRender += null;
                f.List.OnItemRender -= null;
                f.View.ItemPrefabs = new[] { f.Adapter.ItemPrefab };
                f.List.ItemPrefabSelector = null;
                f.List.ViewFactory = null;
                f.List.ItemSizeProvider = null;
                f.List.ItemMeasurer = null;
                f.List.OnItemRecycle -= f.Adapter.Unbind;
                int renders = 0;
                f.List.OnItemRender += (view, row) => ++renders;
                f.List.Submit(f.Groups);
                Require(renders > 0 && f.View.TryGetVisibleItem(0, out var holder) && holder.GetType() == typeof(ScrollItemView) &&
                    holder.Prefab == f.Adapter.ItemPrefab && holder.ItemSize > 0f, "Default single-prefab rendering requires custom callbacks");
            }
        }

        private static void CheckAdapterIntegration()
        {
            using (var f = new Fixture(useAdapter: true, submit: false)) {
                Expect<ArgumentNullException>(() => new ExpandableListController<Group, Item, int>(f.View, null));
                // Adapter 独占显示配置，避免两个入口同时绑定或清理同一视图。
                Expect<InvalidOperationException>(() => f.List.EstimatedItemSize = 45f);
                Expect<InvalidOperationException>(() => f.List.ItemPrefabSelector = f.Adapter.GetItemPrefab);
                Expect<InvalidOperationException>(() => f.List.ViewFactory = f.Adapter.CreateView);
                Expect<InvalidOperationException>(() => f.List.ItemSizeProvider = f.Adapter.TryGetItemSize);
                Expect<InvalidOperationException>(() => f.List.ItemMeasurer = f.Adapter.MeasureItem);
                Expect<InvalidOperationException>(() => f.List.OnItemRender += f.Adapter.Render);
                Expect<InvalidOperationException>(() => f.List.OnItemRender -= f.Adapter.Render);
                Expect<InvalidOperationException>(() => f.List.OnItemRecycle += f.Adapter.Unbind);
                Expect<InvalidOperationException>(() => f.List.OnItemRecycle -= f.Adapter.Unbind);
                Expect<InvalidOperationException>(() => f.List.OnViewDestroyed += f.Adapter.DestroyView);
                Expect<InvalidOperationException>(() => f.List.OnViewDestroyed -= f.Adapter.DestroyView);
                foreach (float estimate in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) {
                    f.Adapter.Estimate = estimate;
                    Expect<InvalidOperationException>(() => f.List.Submit(f.Groups));
                    Require(f.List.RowCount == 0 && !f.View.IsInitialized, "Invalid adapter estimate committed data");
                }
                f.Adapter.Estimate = 30f;
                f.List.Submit(f.Groups);
                Require(f.View.VisibleItems.Count > 0 && f.List.EstimatedItemSize == 30f, "Adapter did not render without event subscribers");
                f.Adapter.Estimate = float.NaN;
                int rows = f.List.RowCount;
                int binds = f.Adapter.Binds;
                Expect<InvalidOperationException>(() => f.List.Submit(f.Groups));
                Require(f.List.RowCount == rows && f.Adapter.Binds == binds && f.View.IsInitialized,
                    "Rejected adapter configuration changed the committed renderer");
                f.Adapter.Estimate = 30f;
                // 无页面状态的 Adapter 可以共享；控制器的展开状态和物理视图仍各自独立。
                using (var second = new Fixture(useAdapter: true, adapter: f.Adapter)) {
                    second.List.SetExpanded(1, false);
                    Require(f.List.IsExpanded(1) && !second.List.IsExpanded(1), "Shared adapter leaked expansion state");
                    Require(f.View.TryGetVisibleItem(0, out var firstView) && second.View.TryGetVisibleItem(0, out var secondView) &&
                        firstView.Root != secondView.Root, "Shared adapter reused a physical view across lists");
                }
                Require(f.List.ScrollToItem(12011) && f.View.VisibleItems.Count > 0, "Disposing one controller invalidated a shared adapter");
                f.List.Dispose();
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
                    Expect<InvalidOperationException>(() => f.List.Dispose());
                };
                f.Adapter.DuringDestroy = () => {
                    ++destroyAttempts;
                    Expect<InvalidOperationException>(() => f.List.Dispose());
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
                Require(recorder.Valid && recorder.Count > 0, "Allocation recorder is not observing allocations");
            }
            string mode = useAdapter ? "adapter_" : "callback_";
            using (var f = new Fixture(groups: 80, children: 24, useAdapter: useAdapter)) {
                f.List.GroupComparison = (a, b) => b.Priority.CompareTo(a.Priority);
                f.List.ItemComparison = (g, a, b) => b.Value.CompareTo(a.Value);
                Measure(mode + "submit_2000_rows", 100, () => f.List.Submit(f.Groups));
                Measure(mode + "toggle_2000_rows", 200, () => f.List.Toggle(1));
                Measure(mode + "scroll_cross_range", 1000, () => f.Scroll(f.View.ScrollOffset > 200f ? 150f : 250f));
                int reads = f.Adapter.Reads;
                int binds = f.Adapter.Binds;
                Measure(mode + "scroll_same_range", 1000, () => f.Scroll(151f));
                Require(f.Adapter.Reads == reads && f.Adapter.Binds <= binds + 12, "Steady scrolling reread data or rebound retained views");
                int previousQueries = f.Adapter.SizeQueries;
                f.List.Submit(f.Groups);
                Require(f.Adapter.SizeQueries - previousQueries < 100, "Submit measured offscreen rows eagerly");
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
                Debug.Log("EXPANDABLE_PERFORMANCE name=" + name + " count=" + count + " gc_alloc_events=" + allocations + " elapsed_ms=" + timer.Elapsed.TotalMilliseconds);
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
