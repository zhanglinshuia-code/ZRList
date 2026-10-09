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
        [MenuItem("Tools/ZRList/Rebuild Expandable List Assets")]
        public static void GenerateExpandable()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            InitializePaths();
            RectTransform header = SavePrefab(CreateExpandablePrefab(true), "ExpandableHeader");
            RectTransform item = SavePrefab(CreateExpandablePrefab(false), "ExpandableItem");
            RectTransform canvas = CreateShell("11", "Your progress, one chapter at a time",
                "Click chapter headers to fold / click objectives to change state / groups reorder while keeping your place");
            VirtualScrollView list = CreateList(canvas, header, false, 430f, showScrollbar: true);
            list.ItemPrefabs = new[] { header, item };
            list.ContentSpacing = 8f;
            ExpandableListDemo demo = canvas.gameObject.AddComponent<ExpandableListDemo>();
            demo.ScrollView = list;
            demo.HeaderPrefab = header;
            demo.ItemPrefab = item;
            demo.Status = DrawText("Status", canvas, new Vector2(56f, -596f), new Vector2(1168f, 24f), "", 14, s_muted);
            DrawButton(canvas, new Vector2(56f, -638f), "Expand all", demo.ExpandAll, 130f);
            DrawButton(canvas, new Vector2(198f, -638f), "Collapse all", demo.CollapseAll, 130f);
            DrawButton(canvas, new Vector2(340f, -638f), "Notify chapter 24", demo.NotifyLastGroup, 184f);
            DrawButton(canvas, new Vector2(536f, -638f), "Mark all read", demo.MarkAllRead, 154f);
            DrawButton(canvas, new Vector2(702f, -638f), "Find objective 2411", demo.JumpToLastItem, 200f);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath("ExpandableList"));
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string path = ScenePath("ExpandableList");
            if (!scenes.Exists(scene => scene.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("EXPANDABLE_ASSETS_GENERATED");
        }

        private static GameObject CreateExpandablePrefab(bool header)
        {
            float height = header ? 64f : 80f;
            RectTransform root = CreateRect(header ? "ExpandableHeader" : "ExpandableItem", null, Vector2.zero, new Vector2(1128f, height));
            Image background = AddImage(root, header ? ColorOf("#29443f") : s_panel);
            background.raycastTarget = true;
            root.gameObject.AddComponent<Button>().targetGraphic = background;
            DrawText("Arrow", root, new Vector2(16f, -12f), new Vector2(26f, 34f), "-", 26, s_accent, FontStyle.Bold);
            Text title = DrawText("Title", root, new Vector2(header ? 50f : 68f, -8f), new Vector2(940f, 28f), "", 20, s_text, FontStyle.Bold);
            StretchX(title.rectTransform, header ? 50f : 68f, 80f, 8f, 28f);
            Text detail = DrawText("Detail", root, new Vector2(header ? 50f : 68f, -37f), new Vector2(940f, 22f), "", 14, s_muted);
            StretchX(detail.rectTransform, header ? 50f : 68f, 80f, 37f, 22f);
            Image dot = DrawPanel("Dot", root, new Vector2(-35f, -24f), new Vector2(12f, 12f), ColorOf("#ff817b"));
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(1f, 1f);
            return root.gameObject;
        }

        public static void GenerateExpandableAndCapture()
        {
            GenerateExpandable();
            ExpandableListDemo demo = FindFirstSceneObject<ExpandableListDemo>();
            Canvas.ForceUpdateCanvases();
            demo.InitializeDemo();
            demo.CollapseAll();
            Canvas.ForceUpdateCanvases();
            Camera camera = FindFirstSceneObject<Camera>();
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            screenshot.Apply();
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Previews");
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "ExpandableList.png"), screenshot.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(screenshot);
            Object.DestroyImmediate(target);
            Debug.Log("EXPANDABLE_PREVIEW_CAPTURED " + output);
        }
    }
}
