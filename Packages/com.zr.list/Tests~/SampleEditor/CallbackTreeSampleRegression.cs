// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZRList.Samples;

namespace ZRList.Tests
{
    /// <summary>在真实场景中验证回调折叠树的点击、重绑、回收与导航。</summary>
    public static class CallbackTreeSampleRegression
    {
        private const string PlayKey = "ZRList.CallbackTreeSampleRegression";
        private static double s_deadline;
        private static float s_jumpStart;
        private static bool s_started;
        private static object s_allocation;

        public static void RunPlayMode()
        {
            if (!Application.isBatchMode) {
                throw new InvalidOperationException("Run in an isolated batch project.");
            }
            string[] scenes = AssetDatabase.FindAssets("CallbackTree t:Scene");
            if (scenes.Length != 1) {
                throw new InvalidOperationException("Import exactly one copy of the CallbackTree sample.");
            }
            EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(scenes[0]));
            SessionState.SetBool(PlayKey, true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(PlayKey, false)) {
                return;
            }
            s_deadline = EditorApplication.timeSinceStartup + 90f;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try {
                if (EditorApplication.timeSinceStartup > s_deadline) {
                    throw new Exception("Callback tree sample timed out.");
                }
                if (!Application.isPlaying || Time.frameCount < 5) {
                    return;
                }
                CallbackTreeDemo demo = UnityEngine.Object.FindObjectOfType<CallbackTreeDemo>();
                if (!s_started) {
                    CheckInteraction(demo);
                    s_started = true;
                    s_jumpStart = Time.unscaledTime;
                    return;
                }
                if (Time.unscaledTime - s_jumpStart < 0.5f) {
                    return;
                }
                Require(HasVisibleTitle(demo.ScrollView, "Lesson 8408"), "Hidden navigation target was not visible");
                Require(demo.ScrollView.ItemCount == 20, "Navigation expanded unrelated branches");
                demo.ScrollView.Dispose();
                UnityEngine.Object.DestroyImmediate(demo.gameObject);
                Debug.Log("CALLBACK_TREE_PLAYMODE_PASSED");
                Finish(0);
            }
            catch (Exception error) {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void CheckInteraction(CallbackTreeDemo demo)
        {
            VirtualScrollView view = demo.ScrollView;
            Canvas.ForceUpdateCanvases();
            Require(view.ItemCount == 20 && Title(view, 2).text == "Lesson 1101", "Initial callback binding failed");
            Require(Mathf.Abs(Title(view, 2).rectTransform.offsetMin.x - Title(view, 0).rectTransform.offsetMin.x - 56f) < 0.1f,
                "Depth indentation did not render");

            // 点击读状态后排序，再点击重绑后的实例，验证读取的是当前 Key 且监听没有重复注册。
            ClickRow(view, 2);
            Require(Detail(view, 2).text.StartsWith("Read /"), "Lesson click did not mark read exactly once");
            ClickControl(demo, "Toggle unread priority");
            view.JumpToDataItem(0);
            Require(Title(view, 2).text == "Lesson 1103", "Unread priority did not reorder siblings");
            ClickRow(view, 2);
            view.JumpToDataItem(0);
            Require(Title(view, 2).text == "Lesson 1105", "Rebound button changed an obsolete key or toggled twice");
            ClickControl(demo, "Toggle unread priority");
            view.JumpToDataItem(0);
            Require(Title(view, 2).text == "Lesson 1101" && Detail(view, 4).text.StartsWith("Read /"),
                "ID sorting lost the changed lesson state");

            Require(view.TryGetVisibleItem(2, out var lesson), "No leaf to check recycling");
            Button pooledButton = lesson.Root.GetComponent<Button>();
            ClickRow(view, 0);
            Require(view.ItemCount == 8, "Root click did not collapse descendants");
            // 直接触发已回收按钮的事件，确认旧业务绑定已结束；正常 UI 不会点击隐藏实例。
            pooledButton.onClick.Invoke();
            Require(view.ItemCount == 8, "Recycled view retained its click binding");
            ClickRow(view, 0);
            Require(view.ItemCount == 20, "Reopen lost descendant expansion");
            Require(Detail(view, 2).text.StartsWith("Read /"), "Recycled view modified old data");
            Capture(demo);

            ClickControl(demo, "Expand all");
            Require(view.ItemCount == 296 && view.VisibleItems.Count < 20, "Expanded callback tree was not virtualized");
            view.JumpToDataItem(250);
            view.JumpToDataItem(0);
            Require(Title(view, 2).text == "Lesson 1101", "Reused view retained old content");
            ClickRow(view, 2);
            Require(Detail(view, 2).text.StartsWith("Unread /"), "Reused button was removed or registered twice");
            CheckWarmScrolling(view);
            ClickControl(demo, "Collapse all");
            Require(view.ItemCount == 8, "Collapse all did not leave roots");
            ClickControl(demo, "Find lesson 8408");
        }

        private static void CheckWarmScrolling(VirtualScrollView view)
        {
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1000, ProfilerRecorderOptions.CollectOnlyOnCurrentThread)) {
                s_allocation = new byte[123];
                recorder.Stop();
                Require(recorder.Valid && recorder.Count > 0, "Allocation recorder is not observing allocations");
            }
            for (int i = 0; i < 20; ++i) {
                Scroll(view, i % 2 == 0 ? 150f : 250f);
            }
            int instances = view.VisibleItems.Count + view.PooledViewCount;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 10000, ProfilerRecorderOptions.CollectOnlyOnCurrentThread)) {
                for (int i = 0; i < 1000; ++i) {
                    Scroll(view, i % 2 == 0 ? 150f : 250f);
                }
                recorder.Stop();
                Require(recorder.Valid && recorder.Count < recorder.Capacity, "Invalid allocation sample");
                int allocations = recorder.Count;
                Debug.Log("CALLBACK_TREE_SCROLL count=1000 gc_alloc_events=" + allocations);
                Require(allocations == 0, "Warmed callback scrolling allocated");
            }
            Require(view.VisibleItems.Count + view.PooledViewCount == instances, "Warmed scrolling created additional views");
            GC.KeepAlive(s_allocation);
        }

        private static void Scroll(VirtualScrollView view, float offset)
        {
            view.Content.anchoredPosition = new Vector2(0f, offset);
            view.ScrollRectValueChanged(Vector2.zero);
        }

        private static Text Title(VirtualScrollView view, int index)
        {
            Require(view.TryGetVisibleItem(index, out var item), "Expected row is not instantiated");
            return item.Root.Find("Title").GetComponent<Text>();
        }

        private static Text Detail(VirtualScrollView view, int index)
        {
            Require(view.TryGetVisibleItem(index, out var item), "Expected row is not instantiated");
            return item.Root.Find("Detail").GetComponent<Text>();
        }

        private static bool HasVisibleTitle(VirtualScrollView view, string title)
        {
            for (int i = 0; i < view.VisibleItems.Count; ++i) {
                ScrollItemView item = view.VisibleItems[i];
                if (view.IsItemVisible(item.ItemIndex) && item.Root.Find("Title").GetComponent<Text>().text == title) {
                    return true;
                }
            }
            return false;
        }

        private static void ClickRow(VirtualScrollView view, int index)
        {
            Require(view.TryGetVisibleItem(index, out var item), "Clicked row is not instantiated");
            Click(item.Root.gameObject);
        }

        private static void ClickControl(CallbackTreeDemo demo, string name)
        {
            Click(demo.transform.Find(name).gameObject);
        }

        private static void Click(GameObject target)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void Capture(CallbackTreeDemo demo)
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
                File.WriteAllBytes(Path.Combine(directory, "CallbackTree.png"), image.EncodeToPNG());
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
            if (!condition) {
                throw new Exception(message);
            }
        }
    }
}
