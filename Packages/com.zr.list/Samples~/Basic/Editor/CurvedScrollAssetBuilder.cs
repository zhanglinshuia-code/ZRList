// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZRList.Samples.Editor
{
    public static partial class ScrollViewDemoAssetBuilder
    {
        [MenuItem("Tools/ZRList/Rebuild Curved Scroll Assets")]
        public static void GenerateCurved()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }

            InitializePaths();
            RectTransform prefab = SavePrefab(CreateCurvedPrefab(), "CurvedCard");
            CreateCurvedScene(prefab);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string path = ScenePath("CurvedScroll");
            if (!scenes.Exists(scene => scene.path == path)) {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("CURVED_SCROLL_ASSETS_GENERATED");
        }

        public static void GenerateCurvedAndCapture()
        {
            GenerateCurved();
            CaptureCurved();
        }

        public static void CaptureCurved()
        {
            InitializePaths();
            EditorSceneManager.OpenScene(ScenePath("CurvedScroll"));
            Canvas.ForceUpdateCanvases();
            CurvedScrollDemo demo = FindFirstSceneObject<CurvedScrollDemo>();
            demo.InitializeDemo();
            demo.ScrollView.JumpToDataItem(12);
            Camera camera = FindFirstSceneObject<Camera>();
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Previews");
            Directory.CreateDirectory(output);
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            foreach (bool custom in new[] { false, true }) {
                if (custom) {
                    demo.UseCustomCurve();
                }
                else {
                    demo.UseArc();
                }

                Canvas.ForceUpdateCanvases();
                demo.ApplyCurve();
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                screenshot.Apply();
                File.WriteAllBytes(Path.Combine(output, custom ? "CurvedScroll-Custom.png" : "CurvedScroll-Arc.png"), screenshot.EncodeToPNG());
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(screenshot);
            }

            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("CURVED_SCROLL_PREVIEWS_CAPTURED " + output);
        }

        private static GameObject CreateCurvedPrefab()
        {
            // The slot contains the visual's diagonal, including tangent rotation at edges.
            RectTransform root = CreateRect("CurvedCard", null, Vector2.zero, new Vector2(224f, 224f));
            RectTransform visual = CreateRect("Visual", root, Vector2.zero, new Vector2(148f, 160f));
            visual.anchorMin = visual.anchorMax = visual.pivot = new Vector2(0.5f, 0.5f);
            visual.anchoredPosition = Vector2.zero;
            AddImage(visual, s_panel).raycastTarget = true;
            DrawPanel("Accent", visual, Vector2.zero, new Vector2(148f, 4f), s_accent);
            RawImage icon = CreateRect("Icon", visual, new Vector2(42f, -18f), new Vector2(64f, 64f)).gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            Text title = DrawText("Title", visual, new Vector2(8f, -99f), new Vector2(132f, 26f), "CARD 0001", 18, s_text, FontStyle.Bold);
            title.alignment = TextAnchor.MiddleCenter;
            Text caption = DrawText("Caption", visual, new Vector2(8f, -131f), new Vector2(132f, 20f), "Follow the flow", 12, s_muted);
            caption.alignment = TextAnchor.MiddleCenter;
            return root.gameObject;
        }

        private static void CreateCurvedScene(RectTransform prefab)
        {
            RectTransform canvas = CreateShell("09", "A collection that follows your curve", "Drag to explore / circular arc with a precise radius / custom AnimationCurve / 1,000 recycled cards");
            VirtualScrollView list = CreateList(canvas, prefab, true, 434f);
            list.StretchItems = false;
            list.ContentPadding = new RectOffset(28, 28, 0, 0);
            list.ContentSpacing = 12f;
            CurvedScrollDemo demo = canvas.gameObject.AddComponent<CurvedScrollDemo>();
            demo.ScrollView = list;
            demo.Icons = LoadIcons();
            demo.Curve.ArcRadius = 1100f;
            demo.Curve.Amplitude = 65f;
            demo.CrossAxisOffset = -55f;
            demo.Curve.CustomCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(0.5f, 0f), new Keyframe(0.75f, -1f), new Keyframe(1f, 0f));
            demo.Status = DrawText("Status", canvas, new Vector2(56f, -600f), new Vector2(1168f, 24f), "", 14, s_muted);
            DrawButton(canvas, new Vector2(56f, -638f), "Arc", demo.UseArc, 80f);
            DrawButton(canvas, new Vector2(148f, -638f), "Custom curve", demo.UseCustomCurve, 150f);
            DrawButton(canvas, new Vector2(310f, -638f), "Radius -", demo.DecreaseRadius, 104f);
            DrawButton(canvas, new Vector2(426f, -638f), "Radius +", demo.IncreaseRadius, 104f);
            DrawButton(canvas, new Vector2(542f, -638f), "Flip", demo.FlipCurve, 80f);
            DrawButton(canvas, new Vector2(634f, -638f), "Rotate", demo.ToggleRotation, 104f);
            DrawButton(canvas, new Vector2(750f, -638f), "First", demo.JumpToFirst, 80f);
            DrawButton(canvas, new Vector2(842f, -638f), "Last", demo.JumpToLast, 80f);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath("CurvedScroll"));
        }
    }
}
