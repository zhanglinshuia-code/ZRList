// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using ZRList.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZRList.Tests
{
    public static class BusinessSortedTreeSampleRegression
    {
        private const string PlayKey = "ZRList.BusinessSortedTreeRegression";
        private static double s_deadline;
        private static bool s_started;
        private static float s_jumpStart;

        public static void RunPlayMode()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
            string[] scenes = AssetDatabase.FindAssets("BusinessSortedTree t:Scene");
            if (scenes.Length != 1) throw new InvalidOperationException("Import exactly one copy of BusinessSortedTree.");
            EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(scenes[0]));
            SessionState.SetBool(PlayKey, true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(PlayKey, false)) return;
            s_deadline = EditorApplication.timeSinceStartup + 90f;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try {
                if (EditorApplication.timeSinceStartup > s_deadline) throw new Exception("Business sorting sample timed out.");
                if (!Application.isPlaying || Time.frameCount < 5) return;
                var demo = UnityEngine.Object.FindObjectOfType<BusinessSortedTreeDemo>();
                if (!s_started) {
                    CheckPriorities(demo);
                    s_started = true;
                    s_jumpStart = Time.unscaledTime;
                    return;
                }
                if (Time.unscaledTime - s_jumpStart < 0.4f) return;
                Require(FindVisible(demo.ScrollView, "Task 1224") != null, "Animated hidden task navigation");
                CheckNestedOrderingAndSelection(demo);
                Debug.Log("BUSINESS_SORT_PLAYMODE_PASSED");
                Finish(0);
            }
            catch (Exception error) {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void CheckPriorities(BusinessSortedTreeDemo demo)
        {
            Canvas.ForceUpdateCanvases();
            Require(demo.Selection.text.Contains("Task 1224"), "Initial selected task");
            CheckRootOrder(demo, 4, 3, 2, 1);
            ClickControl(demo, "Toggle notification");
            CheckRootOrder(demo, 1, 4, 3, 2);
            Require(demo.UnlockLabel.text.StartsWith("Locked") && demo.ReadLabel.text.StartsWith("Unread"), "Notification changed unrelated flags");
            Require(demo.OrderSummary.text.Contains("Before   04 > 03 > 02 > 01") && demo.OrderSummary.text.Contains("After      01 > 04 > 03 > 02"), "Before/after root order");
            Capture(demo, "BusinessSortedTree-Overview.png");
            ClickControl(demo, "Toggle notification");
            CheckRootOrder(demo, 4, 3, 2, 1);
            ClickControl(demo, "Toggle unlock");
            // Equal unlocked/unread flags with Campaign 03 are resolved by ascending ID.
            CheckRootOrder(demo, 4, 1, 3, 2);
            Require(demo.NotificationLabel.text.StartsWith("Red dot: OFF"), "Unlock changed notification state");
            ClickControl(demo, "Toggle read");
            // Now Campaign 03 wins the unread priority; Campaign 01 and 02 tie on flags.
            CheckRootOrder(demo, 4, 3, 1, 2);
            ClickControl(demo, "Toggle unlock");
            CheckRootOrder(demo, 4, 3, 2, 1);
            ClickControl(demo, "Reset scenario");
            CheckRootOrder(demo, 4, 3, 2, 1);
            ClickControl(demo, "Find selected task");
            Require(demo.ScrollView.ItemCount == 12, "Navigation opened more than the selected ancestor chain");
        }

        private static void CheckNestedOrderingAndSelection(BusinessSortedTreeDemo demo)
        {
            VirtualScrollView view = demo.ScrollView;
            ScrollItemView task = FindVisible(view, "Task 1224");
            float inset = Inset(view, task);
            ClickControl(demo, "Toggle notification");
            ScrollItemView retained = FindVisible(view, "Task 1224");
            Require(ReferenceEquals(task, retained) && Mathf.Abs(Inset(view, retained) - inset) < 0.5f,
                "Business reorder lost the selected task's physical view or reading anchor");
            Require(view.ItemCount == 12, "Business update lost descendant expansion");
            view.JumpToDataItem(0);
            Require(TitleAt(view, 0) == "Campaign 01" && TitleAt(view, 1) == "Chapter 1.2" &&
                TitleAt(view, 2) == "Section 1.2.2" && TitleAt(view, 3) == "Task 1224", "Business priorities did not sort every sibling level");
            for (int i = 0; i < 4; ++i) {
                Require(view.TryGetVisibleItem(i, out var row) && row.Root.Find("Dot").gameObject.activeSelf, "Red dot aggregation did not reach every ancestor");
            }
            Capture(demo, "BusinessSortedTree.png");
            // Pick another visible task; its own state is edited through the same panel controls.
            Require(TitleAt(view, 4) == "Task 1221", "ID tie order for read siblings");
            view.TryGetVisibleItem(4, out var other);
            Click(other.Root.gameObject);
            Require(demo.Selection.text.Contains("Task 1221"), "Recycled task button used the wrong key");
            ClickControl(demo, "Toggle notification");
            // Clear the original notification while the sibling still has one: ANY aggregation must remain true.
            task = FindVisible(view, "Task 1224");
            Require(task != null, "Original task missing after sibling promotion");
            Click(task.Root.gameObject);
            ClickControl(demo, "Toggle notification");
            view.JumpToDataItem(0);
            Require(TitleAt(view, 0) == "Campaign 01" && TitleAt(view, 3) == "Task 1221", "Remaining sibling notification was lost during parent aggregation");
            ClickControl(demo, "Expand all");
            Require(view.ItemCount == 92 && view.VisibleItems.Count < 20, "Business tree virtualization");
            ClickControl(demo, "Root overview");
            CheckRootOrder(demo, 1, 4, 3, 2);
            ClickControl(demo, "Reset scenario");
            Require(demo.Selection.text.Contains("Task 1224") && demo.ReadLabel.text.StartsWith("Unread") && demo.NotificationLabel.text.StartsWith("Red dot: OFF"), "Reset did not restore initial business flags");
            CheckRootOrder(demo, 4, 3, 2, 1);
        }

        private static void CheckRootOrder(BusinessSortedTreeDemo demo, params int[] expected)
        {
            Require(demo.ScrollView.ItemCount == expected.Length, "Expected a collapsed root overview");
            for (int i = 0; i < expected.Length; ++i) {
                Require(TitleAt(demo.ScrollView, i) == "Campaign " + expected[i].ToString("D2"), "Root order mismatch at " + i);
            }
        }

        private static string TitleAt(VirtualScrollView view, int index)
        {
            Require(view.TryGetVisibleItem(index, out var item), "Missing visible index " + index);
            return item.Root.Find("Title").GetComponent<Text>().text;
        }

        private static ScrollItemView FindVisible(VirtualScrollView view, string title)
        {
            for (int i = 0; i < view.VisibleItems.Count; ++i) {
                ScrollItemView item = view.VisibleItems[i];
                if (view.IsItemVisible(item.ItemIndex) && item.Root.Find("Title").GetComponent<Text>().text == title) return item;
            }
            return null;
        }

        private static float Inset(VirtualScrollView view, ScrollItemView item)
        {
            return view.Content.anchoredPosition.y + item.Root.anchoredPosition.y + item.Root.rect.yMax;
        }

        private static void ClickControl(BusinessSortedTreeDemo demo, string name)
        {
            Click(demo.transform.Find(name).gameObject);
        }

        private static void Click(GameObject target)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void Capture(BusinessSortedTreeDemo demo, string name)
        {
            Canvas.ForceUpdateCanvases();
            Camera camera = demo.GetComponent<Canvas>().worldCamera;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Previews");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(PlayKey, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(exitCode);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
