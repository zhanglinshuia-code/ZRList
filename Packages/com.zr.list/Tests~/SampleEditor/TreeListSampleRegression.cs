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
    public static class TreeListSampleRegression
    {
        private const string PlayKey = "ZRList.TreeSampleRegression";
        private static double s_deadline;
        private static float s_jumpStart;
        private static bool s_started;

        public static void RunPlayMode()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
            string[] scenes = AssetDatabase.FindAssets("TreeList t:Scene");
            if (scenes.Length != 1) throw new InvalidOperationException("Import exactly one copy of the TreeList sample.");
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
                if (EditorApplication.timeSinceStartup > s_deadline) throw new Exception("Tree sample timed out.");
                if (!Application.isPlaying || Time.frameCount < 5) return;
                TreeListDemo demo = UnityEngine.Object.FindObjectOfType<TreeListDemo>();
                if (!s_started) {
                    CheckButtons(demo);
                    s_started = true;
                    s_jumpStart = Time.unscaledTime;
                    return;
                }
                if (Time.unscaledTime - s_jumpStart < 0.5f) return;
                Require(HasVisibleTitle(demo.ScrollView, "Task 6435"), "Navigation failed to reveal the deeply hidden target");
                demo.ScrollView.JumpToDataItem(0);
                Canvas.ForceUpdateCanvases();
                Capture(demo);
                TreeListRegression.Run();
                Debug.Log("TREE_PLAYMODE_PASSED");
                Finish(0);
            }
            catch (Exception error) {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void CheckButtons(TreeListDemo demo)
        {
            Canvas.ForceUpdateCanvases();
            VirtualScrollView view = demo.ScrollView;
            Require(view.ItemCount == 18, "Initial four-level path");
            demo.CollapseAll();
            Require(view.ItemCount == 6, "Collapse all did not leave only roots");
            Click(view, 0);
            Require(view.ItemCount == 10, "Root click did not reveal chapters");
            Click(view, 1);
            Require(view.ItemCount == 13, "Chapter click did not reveal sections");
            Click(view, 2);
            Require(view.ItemCount == 18, "Section click did not reveal tasks");
            Require(view.TryGetVisibleItem(3, out var task) && task.Root.Find("Dot").gameObject.activeSelf, "Notification task did not sort first");
            float rootIndent = Title(view, 0).rectTransform.offsetMin.x;
            float taskIndent = Title(view, 3).rectTransform.offsetMin.x;
            Require(Mathf.Abs(taskIndent - rootIndent - 84f) < 0.1f, "Depth indentation was not applied");
            Click(view, 0);
            Require(view.ItemCount == 6, "Parent collapse");
            Click(view, 0);
            Require(view.ItemCount == 18, "Parent reopen discarded descendant expansion");
            Click(view, 3);
            Require(view.ItemCount == 18, "Business change discarded branch expansion");
            demo.ExpandAll();
            Require(view.ItemCount == 462 && view.VisibleItems.Count < 20, "Full tree is not virtualized");
            demo.MarkAllRead();
            demo.CollapseAll();
            view.JumpToDataItem(0);
            Require(Title(view, 0).text == "Campaign 01", "Read state did not sort root groups");
            demo.NotifyLastTask();
            view.JumpToDataItem(0);
            Require(Title(view, 0).text == "Campaign 06", "Notification was not aggregated to the root");
            demo.FindLastTask();
            Require(view.ItemCount == 18, "Navigation opened unrelated branches");
            view.JumpToDataItem(0);
            Require(Title(view, 1).text == "Chapter 6.4" && Title(view, 2).text == "Section 6.4.3" && Title(view, 3).text == "Task 6435", "Notification ordering did not propagate through all sibling levels");
            demo.FindLastTask();
        }

        private static Text Title(VirtualScrollView view, int index)
        {
            Require(view.TryGetVisibleItem(index, out var item), "Expected visible row " + index);
            return item.Root.Find("Title").GetComponent<Text>();
        }

        private static void Click(VirtualScrollView view, int index)
        {
            view.JumpToDataItem(0);
            Require(view.TryGetVisibleItem(index, out var item), "Click target missing");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(item.Root.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static bool HasVisibleTitle(VirtualScrollView view, string title)
        {
            for (int i = 0; i < view.VisibleItems.Count; ++i) {
                ScrollItemView item = view.VisibleItems[i];
                if (view.IsItemVisible(item.ItemIndex) && item.Root.Find("Title").GetComponent<Text>().text == title) return true;
            }
            return false;
        }

        private static void Capture(TreeListDemo demo)
        {
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
                File.WriteAllBytes(Path.Combine(directory, "TreeList.png"), image.EncodeToPNG());
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
