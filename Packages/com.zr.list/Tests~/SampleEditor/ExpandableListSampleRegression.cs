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
    public static class ExpandableListSampleRegression
    {
        private const string PlayKey = "ZRList.ExpandableSampleRegression";
        private static double s_deadline;
        private static float s_jumpStart;
        private static bool s_started;

        public static void RunPlayMode()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
            string[] scenes = AssetDatabase.FindAssets("ExpandableList t:Scene");
            if (scenes.Length != 1) throw new InvalidOperationException("Import exactly one copy of the ExpandableList sample.");
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
                if (EditorApplication.timeSinceStartup > s_deadline) throw new Exception("Expandable sample timed out.");
                if (!Application.isPlaying || Time.frameCount < 5) return;
#if UNITY_2022_2_OR_NEWER
                ExpandableListDemo demo = UnityEngine.Object.FindFirstObjectByType<ExpandableListDemo>();
#else
                ExpandableListDemo demo = UnityEngine.Object.FindObjectOfType<ExpandableListDemo>();
#endif
                if (!s_started) {
                    CheckButtons(demo);
                    s_started = true;
                    s_jumpStart = Time.unscaledTime;
                    return;
                }
                if (Time.unscaledTime - s_jumpStart < 0.5f) return;
                Require(HasVisibleTitle(demo.ScrollView, "Objective 2411"), "Hidden-item navigation did not reveal its target");
                CheckDrag(demo);
                demo.ExpandAll();
                demo.ScrollView.JumpToDataItem(0);
                Canvas.ForceUpdateCanvases();
                Capture(demo);
                // Exercise the same functional and allocation paths in an actual Player loop.
                ExpandableListRegression.Run();
                Debug.Log("EXPANDABLE_PLAYMODE_PASSED");
                Finish(0);
            }
            catch (Exception error) {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void CheckButtons(ExpandableListDemo demo)
        {
            Canvas.ForceUpdateCanvases();
            demo.CollapseAll();
            VirtualScrollView view = demo.ScrollView;
            Require(view.ItemCount == 24, "Collapse all did not leave just headers");
            view.JumpToDataItem(0);
            Require(view.TryGetVisibleItem(0, out ScrollItemView header), "No first header");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(header.Root.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Require(view.ItemCount == 36, "Header click did not expand one group");
            Require(view.TryGetVisibleItem(1, out ScrollItemView item), "No child after expansion");
            Require(item.Root.Find("Dot").gameObject.activeSelf, "Expected unread notification child first");
            ExecuteEvents.Execute(item.Root.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Require(view.ItemCount == 36, "Changing business state lost the expansion");
            demo.MarkAllRead();
            demo.CollapseAll();
            view.JumpToDataItem(0);
            Require(view.TryGetVisibleItem(0, out header) && header.Root.Find("Title").GetComponent<Text>().text == "Chapter 01", "Read state did not update group ordering");
            demo.NotifyLastGroup();
            view.JumpToDataItem(0);
            Require(view.TryGetVisibleItem(0, out header) && header.Root.Find("Title").GetComponent<Text>().text == "Chapter 24", "Business notification did not move the collapsed group");
            demo.JumpToLastItem();
            Require(view.ItemCount == 36, "Navigation failed to expand its group");
        }

        private static void CheckDrag(ExpandableListDemo demo)
        {
            VirtualScrollView view = demo.ScrollView;
            VirtualScrollRect scroll = view.ScrollRect;
            Camera camera = demo.GetComponent<Canvas>().worldCamera;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, view.Viewport.TransformPoint(view.Viewport.rect.center));
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = center,
                pressPosition = center
            };
            scroll.OnInitializePotentialDrag(pointer);
            scroll.OnBeginDrag(pointer);
            pointer.position += new Vector2(0f, 20f);
            pointer.delta = new Vector2(0f, 20f);
            scroll.OnDrag(pointer);
            view.ScrollRectValueChanged(Vector2.zero);
            scroll.velocity = new Vector2(0f, 123f);
            demo.NotifyLastGroup();
            Require(scroll.IsDragging && Mathf.Abs(scroll.velocity.y - 123f) < 0.01f, "Data submission interrupted drag or inertia");
            Vector2 afterSubmit = view.Content.anchoredPosition;
            pointer.delta = Vector2.zero;
            scroll.OnDrag(pointer);
            Require(Vector2.Distance(view.Content.anchoredPosition, afterSubmit) < 0.5f, "Data update did not compensate the drag origin");
            scroll.OnEndDrag(pointer);
            demo.JumpToLastItem();
            demo.NotifyLastGroup();
            Require(!view.IsJumping, "Data submission left an obsolete jump active");
            scroll.StopMovement();
        }

        private static bool HasVisibleTitle(VirtualScrollView view, string title)
        {
            for (int i = 0; i < view.VisibleItems.Count; ++i) {
                ScrollItemView item = view.VisibleItems[i];
                if (view.IsItemVisible(item.ItemIndex) && item.Root.Find("Title").GetComponent<Text>().text == title) return true;
            }
            return false;
        }

        private static void Capture(ExpandableListDemo demo)
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
                File.WriteAllBytes(Path.Combine(directory, "ExpandableList-Expanded.png"), image.EncodeToPNG());
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
