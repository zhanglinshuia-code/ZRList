// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZRList.Samples.Editor
{
    /// <summary>Recreates the editable demo prefabs and scenes with native Unity APIs.</summary>
    public static partial class ScrollViewDemoAssetBuilder
    {
        private static Font s_font;
        private static string s_root;
        private static readonly Color s_background = ColorOf("#0d1521");
        private static readonly Color s_panel = ColorOf("#172436");
        private static readonly Color s_text = ColorOf("#e8eef8");
        private static readonly Color s_muted = ColorOf("#94a8c3");
        private static readonly Color s_accent = ColorOf("#73ddc8");
        [MenuItem("Tools/ZRList/Rebuild Showcase Assets")]
        public static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }

            InitializePaths();
            RectTransform vertical = SavePrefab(CreateCatalogPrefab(false), "VerticalCard");
            RectTransform horizontal = SavePrefab(CreateCatalogPrefab(true), "HorizontalCard");
            RectTransform slot = SavePrefab(CreateInventoryPrefab(), "InventorySlot");
            RectTransform dragSlot = SavePrefab(CreateItemDragPrefab(), "ItemDragSlot");
            RectTransform rewardSlot = SavePrefab(CreateRewardPrefab(), "RewardSlot");
            RectTransform row = SavePrefab(CreateRect("InventoryRow", null, Vector2.zero, new Vector2(1000f, 144f)).gameObject, "InventoryRow");
            RectTransform incoming = SavePrefab(CreateChatPrefab(false), "ChatIncoming");
            RectTransform outgoing = SavePrefab(CreateChatPrefab(true), "ChatOutgoing");
            RectTransform incomingInfo = SavePrefab(CreateChatPrefab(false, "ChatIncomingInfo", "#233f45"), "ChatIncomingInfo");
            RectTransform outgoingWarm = SavePrefab(CreateChatPrefab(true, "ChatOutgoingWarm", "#f0d6a4"), "ChatOutgoingWarm");
            RectTransform incomingAlert = SavePrefab(CreateChatPrefab(false, "ChatIncomingAlert", "#49313f"), "ChatIncomingAlert");
            CreateCatalogScene(false, vertical);
            CreateCatalogScene(true, horizontal);
            CreateInventoryScene(slot);
            CreateItemDragScene(row, dragSlot);
            CreateRewardScene(row, rewardSlot);
            CreateDirectRewardScene(rewardSlot);
            CreateChatScene(new[] { incoming, outgoing, incomingInfo, outgoingWarm, incomingAlert });
            GenerateNested();
            GenerateCurved();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string name in new[]
            {
                "VerticalList",
                "HorizontalList",
                "InventoryGrid",
                "ItemDrag",
                "RewardReveal",
                "DirectRewardReveal",
                "CurvedScroll",
                "Chat",
                "VerticalNestedHorizontal",
                "HorizontalNestedVertical"
            }
            ) {
                string path = ScenePath(name);
                if (!buildScenes.Exists(scene => scene.path == path)) {
                    buildScenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log("SHOWCASE_ASSETS_GENERATED root=" + s_root);
        }

        public static void GenerateAndCapture()
        {
            Generate();
            CaptureAll();
        }

        [MenuItem("Tools/ZRList/Rebuild Inventory Grid Assets")]
        public static void GenerateInventory()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }
            InitializePaths();
            RectTransform slot = AssetDatabase.LoadAssetAtPath<RectTransform>(s_root + "/Prefabs/Showcase/InventorySlot.prefab");
            CreateInventoryScene(slot);
            AssetDatabase.SaveAssets();
            Debug.Log("DIRECT_GRID_ASSETS_GENERATED");
        }

        [MenuItem("Tools/ZRList/Rebuild Item Drag Assets")]
        public static void GenerateItemDrag()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }

            InitializePaths();
            RectTransform row = AssetDatabase.LoadAssetAtPath<RectTransform>(s_root + "/Prefabs/Showcase/InventoryRow.prefab");
            RectTransform slot = SavePrefab(CreateItemDragPrefab(), "ItemDragSlot");
            CreateItemDragScene(row, slot);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string path = ScenePath("ItemDrag");
            if (!scenes.Exists(scene => scene.path == path)) {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("ITEM_DRAG_ASSETS_GENERATED");
        }

        [MenuItem("Tools/ZRList/Rebuild Reward Reveal Assets")]
        public static void GenerateRewards()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }

            InitializePaths();
            RectTransform row = AssetDatabase.LoadAssetAtPath<RectTransform>(s_root + "/Prefabs/Showcase/InventoryRow.prefab");
            if (row == null) {
                row = SavePrefab(CreateRect("InventoryRow", null, Vector2.zero, new Vector2(1000f, 144f)).gameObject, "InventoryRow");
            }

            RectTransform slot = SavePrefab(CreateRewardPrefab(), "RewardSlot");
            CreateRewardScene(row, slot);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string path = ScenePath("RewardReveal");
            if (!scenes.Exists(scene => scene.path == path)) {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("REWARD_REVEAL_ASSETS_GENERATED");
        }

        [MenuItem("Tools/ZRList/Rebuild Nested Assets")]
        public static void GenerateNested()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }

            InitializePaths();
            RectTransform horizontalChild = SavePrefab(CreateNestedChildPrefab(true), "NestedHorizontalItem");
            RectTransform verticalChild = SavePrefab(CreateNestedChildPrefab(false), "NestedVerticalItem");
            RectTransform verticalGroup = SavePrefab(CreateNestedGroupPrefab(false, horizontalChild), "NestedVerticalGroup");
            RectTransform horizontalGroup = SavePrefab(CreateNestedGroupPrefab(true, verticalChild), "NestedHorizontalGroup");
            CreateNestedScene(false, verticalGroup);
            CreateNestedScene(true, horizontalGroup);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string name in new[] { "VerticalNestedHorizontal", "HorizontalNestedVertical" }) {
                string path = ScenePath(name);
                if (!scenes.Exists(scene => scene.path == path)) {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("NESTED_ASSETS_GENERATED root=" + s_root);
        }

        public static void GenerateNestedAndCapture()
        {
            GenerateNested();
            CaptureAll();
        }

        public static void CaptureAll()
        {
            InitializePaths();
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Previews");
            Directory.CreateDirectory(output);
            foreach (string name in new[]
            {
                "VerticalList",
                "HorizontalList",
                "InventoryGrid",
                "ItemDrag",
                "RewardReveal",
                "DirectRewardReveal",
                "CurvedScroll",
                "Chat",
                "VerticalNestedHorizontal",
                "HorizontalNestedVertical"
            }
            ) {
                EditorSceneManager.OpenScene(ScenePath(name));
                Camera camera = UnityEngine.Object.FindObjectOfType<Camera>();
                var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                InitializeActiveDemo();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                screenshot.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), screenshot.EncodeToPNG());
                RenderTexture.active = previous;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(screenshot);
                UnityEngine.Object.DestroyImmediate(target);
            }

            Debug.Log("SHOWCASE_PREVIEWS_CAPTURED " + output);
        }

        private static void InitializeActiveDemo()
        {
            CatalogScrollDemo catalog = UnityEngine.Object.FindObjectOfType<CatalogScrollDemo>();
            InventoryGridDemo inventory = UnityEngine.Object.FindObjectOfType<InventoryGridDemo>();
            ChatScrollDemo chat = UnityEngine.Object.FindObjectOfType<ChatScrollDemo>();
            NestedScrollDemo nested = UnityEngine.Object.FindObjectOfType<NestedScrollDemo>();
            ItemDragDemo drag = UnityEngine.Object.FindObjectOfType<ItemDragDemo>();
            RewardRevealDemo rewards = UnityEngine.Object.FindObjectOfType<RewardRevealDemo>();
            DirectRewardRevealDemo directRewards = UnityEngine.Object.FindObjectOfType<DirectRewardRevealDemo>();
            CurvedScrollDemo curved = UnityEngine.Object.FindObjectOfType<CurvedScrollDemo>();
            if (curved != null) {
                curved.InitializeDemo();
                curved.ApplyCurve();
            }
            if (rewards != null) {
                rewards.InitializeDemo();
                rewards.ShowAllRewards();
            }
            if (directRewards != null) {
                directRewards.InitializeDemo();
                directRewards.ShowAllRewards();
            }
            if (drag != null) {
                drag.InitializeDemo();
            }
            if (catalog != null) {
                catalog.InitializeDemo();
            }

            if (inventory != null) {
                inventory.InitializeDemo();
            }

            if (chat != null) {
                chat.InitializeDemo();
            }

            if (nested != null) {
                nested.InitializeDemo();
            }
        }

        private static void InitializePaths()
        {
            string[] scripts = AssetDatabase.FindAssets("ScrollViewDemoAssetBuilder t:MonoScript");
            if (scripts.Length != 1) {
                throw new InvalidOperationException("Import exactly one copy of the showcase sample.");
            }

            s_root = Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(scripts[0]))).Replace('\\', '/');
            s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Directory.CreateDirectory(s_root + "/Prefabs/Showcase");
            Directory.CreateDirectory(s_root + "/Scenes");
            AssetDatabase.Refresh();
        }

        private static string ScenePath(string name)
        {
            return s_root + "/Scenes/" + name + ".unity";
        }

        private static RectTransform SavePrefab(GameObject root, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, s_root + "/Prefabs/Showcase/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return (RectTransform)prefab.transform;
        }

        private static GameObject CreateCatalogPrefab(bool horizontal)
        {
            RectTransform root = CreateRect(horizontal ? "HorizontalCard" : "VerticalCard", null, Vector2.zero, horizontal ? new Vector2(240f, 366f) : new Vector2(1000f, 120f));
            AddImage(root, s_panel);
            Image accent = DrawPanel("Accent", root, Vector2.zero, new Vector2(horizontal ? 240f : 4f, horizontal ? 4f : 120f), s_accent);
            if (horizontal) {
                StretchX(accent.rectTransform, 0f, 0f, 0f, 4f);
            }
            else {
                accent.rectTransform.anchorMin = Vector2.zero;
                accent.rectTransform.anchorMax = new Vector2(0f, 1f);
                accent.rectTransform.offsetMin = Vector2.zero;
                accent.rectTransform.offsetMax = new Vector2(4f, 0f);
            }

            RectTransform icon = CreateRect("Icon", root, horizontal ? new Vector2(16f, -18f) : new Vector2(20f, -20f), horizontal ? new Vector2(208f, 148f) : new Vector2(64f, 64f));
            RawImage iconImage = icon.gameObject.AddComponent<RawImage>();
            iconImage.raycastTarget = false;
            if (horizontal) {
                StretchX(icon, 16f, 16f, 18f, 148f);
            }

            Text title = DrawText("Title", root, horizontal ? new Vector2(16f, -194f) : new Vector2(108f, -22f), new Vector2(700f, 32f), "Adventure kit", horizontal ? 18 : 24, s_text, FontStyle.Bold);
            Text description = DrawText("Description", root, horizontal ? new Vector2(16f, -242f) : new Vector2(108f, -60f), new Vector2(700f, horizontal ? 96f : 58f), "Explore and collect", 16, s_muted);
            if (horizontal) {
                StretchX(title.rectTransform, 16f, 16f, 194f, 36f);
                StretchX(description.rectTransform, 16f, 16f, 242f, 96f);
            }

            Text index = DrawText("Index", root, new Vector2(-18f, -18f), new Vector2(64f, 20f), "001", 12, s_muted);
            index.rectTransform.anchorMin = index.rectTransform.anchorMax = new Vector2(1f, 1f);
            index.rectTransform.pivot = new Vector2(1f, 1f);
            index.alignment = TextAnchor.UpperRight;
            return root.gameObject;
        }

        private static GameObject CreateInventoryPrefab()
        {
            RectTransform root = CreateRect("InventorySlot", null, Vector2.zero, new Vector2(144f, 144f));
            Image background = AddImage(root, s_panel);
            background.raycastTarget = true;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            Image selection = DrawPanel("Selection", root, Vector2.zero, root.sizeDelta, new Color(s_accent.r, s_accent.g, s_accent.b, 0.18f));
            Stretch(selection.rectTransform);
            Image rarityStripe = DrawPanel("Rarity", root, Vector2.zero, new Vector2(144f, 4f), s_accent);
            StretchX(rarityStripe.rectTransform, 0f, 0f, 0f, 4f);
            Image iconPanel = DrawPanel("IconPanel", root, new Vector2(40f, -18f), new Vector2(64f, 64f), ColorOf("#29413d"));
            Text symbol = DrawText("Symbol", iconPanel.transform, Vector2.zero, new Vector2(64f, 64f), "HP", 22, s_text, FontStyle.Bold);
            symbol.alignment = TextAnchor.MiddleCenter;
            Text caption = DrawText("Name", root, new Vector2(8f, -92f), new Vector2(128f, 32f), "Health potion", 14, s_text);
            caption.alignment = TextAnchor.MiddleCenter;
            Text quantity = DrawText("Quantity", root, new Vector2(-10f, 8f), new Vector2(80f, 16f), "x1", 12, s_muted);
            quantity.rectTransform.anchorMin = quantity.rectTransform.anchorMax = new Vector2(1f, 0f);
            quantity.rectTransform.pivot = new Vector2(1f, 0f);
            quantity.alignment = TextAnchor.LowerRight;
            return root.gameObject;
        }

        private static GameObject CreateChatPrefab(bool outgoing, string name = null, string bubbleColor = null)
        {
            RectTransform root = CreateRect(name ?? (outgoing ? "ChatOutgoing" : "ChatIncoming"), null, Vector2.zero, new Vector2(1128f, 110f));
            Image bubble = DrawPanel("Bubble", root, new Vector2(outgoing ? -68f : 68f, -6f), new Vector2(540f, 96f), ColorOf(bubbleColor ?? (outgoing ? "#a2e9d8" : "#26384f")));
            bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = new Vector2(outgoing ? 1f : 0f, 1f);
            bubble.rectTransform.pivot = new Vector2(outgoing ? 1f : 0f, 1f);
            Color text = outgoing ? ColorOf("#183d35") : s_text;
            DrawText("Speaker", bubble.transform, new Vector2(16f, -10f), new Vector2(460f, 18f), outgoing ? "YOU" : "MORGAN", 11, outgoing ? ColorOf("#3f6b60") : s_muted, FontStyle.Bold);
            DrawText("Message", bubble.transform, new Vector2(16f, -32f), new Vector2(508f, 32f), "A message", 18, text);
            Text timeLabel = DrawText("Time", bubble.transform, new Vector2(-14f, 8f), new Vector2(80f, 14f), "09:00", 10, outgoing ? ColorOf("#3f6b60") : s_muted);
            timeLabel.rectTransform.anchorMin = timeLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            timeLabel.rectTransform.pivot = new Vector2(1f, 0f);
            timeLabel.alignment = TextAnchor.LowerRight;
            Image avatar = DrawPanel("Avatar", root, new Vector2(outgoing ? -12f : 12f, -6f), new Vector2(40f, 40f), outgoing ? ColorOf("#376d62") : ColorOf("#315787"));
            avatar.rectTransform.anchorMin = avatar.rectTransform.anchorMax = new Vector2(outgoing ? 1f : 0f, 1f);
            avatar.rectTransform.pivot = new Vector2(outgoing ? 1f : 0f, 1f);
            Text label = DrawText("Initial", avatar.transform, Vector2.zero, new Vector2(40f, 40f), outgoing ? "Y" : "M", 18, s_text, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleCenter;
            return root.gameObject;
        }

        private static GameObject CreateItemDragPrefab()
        {
            GameObject slot = CreateInventoryPrefab();
            slot.name = "ItemDragSlot";
            slot.GetComponent<Image>().raycastTarget = true;
            ((RectTransform)slot.transform.Find("IconPanel")).anchoredPosition = new Vector2(52f, -18f);
            Image handle = DrawPanel("DragHandle", slot.transform, new Vector2(8f, -18f), new Vector2(32f, 64f), ColorOf("#28453f"));
            handle.raycastTarget = true;
            Text grip = DrawText("Grip", handle.transform, Vector2.zero, new Vector2(32f, 44f), "=\n=", 18, s_accent, FontStyle.Bold);
            grip.alignment = TextAnchor.MiddleCenter;
            Text label = DrawText("Label", handle.transform, new Vector2(0f, -44f), new Vector2(32f, 20f), "MOVE", 9, s_accent, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleCenter;
            return slot;
        }

        private static GameObject CreateRewardPrefab()
        {
            RectTransform root = CreateRect("RewardSlot", null, Vector2.zero, new Vector2(144f, 144f));
            var visual = (RectTransform)CreateInventoryPrefab().transform;
            // Reward visuals do not accept input. A selectable under a fading
            // CanvasGroup starts unnecessary transition coroutines on activation.
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Button>());
            visual.name = "Visual";
            visual.SetParent(root, false);
            visual.anchorMin = visual.anchorMax = visual.pivot = new Vector2(0.5f, 0.5f);
            visual.anchoredPosition = Vector2.zero;
            CanvasGroup group = visual.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return root.gameObject;
        }

        private static RectTransform CreateShell(string number, string title, string subtitle)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("UI Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = s_background;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DrawText("Eyebrow", canvas.transform, new Vector2(56f, -24f), new Vector2(1150f, 20f), "ZRLIST  /  " + number, 12, s_accent, FontStyle.Bold);
            DrawText("Title", canvas.transform, new Vector2(56f, -52f), new Vector2(1150f, 46f), title, 36, s_text, FontStyle.Bold);
            DrawText("Subtitle", canvas.transform, new Vector2(56f, -104f), new Vector2(1150f, 24f), subtitle, 16, s_muted);
            return (RectTransform)canvas.transform;
        }

        private static GameObject CreateNestedChildPrefab(bool horizontal)
        {
            RectTransform root = CreateRect(horizontal ? "NestedHorizontalItem" : "NestedVerticalItem", null, Vector2.zero, horizontal ? new Vector2(176f, 154f) : new Vector2(272f, 84f));
            AddImage(root, ColorOf("#22364c"));
            Image accent = DrawPanel("Accent", root, Vector2.zero, new Vector2(176f, 4f), s_accent);
            StretchX(accent.rectTransform, 0f, 0f, 0f, 4f);
            Text title = DrawText("Title", root, new Vector2(14f, -18f), new Vector2(140f, 26f), "Item 001", 20, s_text, FontStyle.Bold);
            StretchX(title.rectTransform, 14f, 14f, 18f, 26f);
            Text description = DrawText("Description", root, new Vector2(14f, -50f), new Vector2(140f, horizontal ? 70f : 30f), "Group 001", 13, s_muted);
            StretchX(description.rectTransform, 14f, 14f, 50f, horizontal ? 70f : 30f);
            return root.gameObject;
        }

        private static GameObject CreateNestedGroupPrefab(bool horizontal, RectTransform child)
        {
            RectTransform root = CreateRect(horizontal ? "NestedHorizontalGroup" : "NestedVerticalGroup", null, Vector2.zero, horizontal ? new Vector2(320f, 410f) : new Vector2(1128f, 228f));
            AddImage(root, s_panel);
            Text title = DrawText("Title", root, new Vector2(16f, -12f), new Vector2(600f, 26f), "Group 001", 18, s_accent, FontStyle.Bold);
            StretchX(title.rectTransform, 16f, 16f, 12f, 26f);
            VirtualScrollView inner = CreateList(root, child, !horizontal);
            inner.name = "InnerList";
            RectTransform host = (RectTransform)inner.transform;
            host.anchorMin = Vector2.zero;
            host.anchorMax = Vector2.one;
            host.offsetMin = new Vector2(12f, 12f);
            host.offsetMax = new Vector2(-12f, -48f);
            inner.ContentPadding = new RectOffset(8, 8, 8, 8);
            inner.ContentSpacing = 10f;
            inner.ScrollRect.scrollSensitivity = 28f;
            return root.gameObject;
        }

        private static void CreateNestedScene(bool horizontal, RectTransform group)
        {
            RectTransform canvas = CreateShell(horizontal ? "06" : "05", horizontal ? "Columns with their own stories" : "Shelves you can explore", horizontal ? "Drag sideways between groups / drag up and down within each column" : "Drag up and down between groups / drag sideways within each shelf");
            NestedScrollDemo demo = canvas.gameObject.AddComponent<NestedScrollDemo>();
            demo.ScrollView = CreateList(canvas, group, horizontal);
            demo.Status = DrawText("Status", canvas, new Vector2(56f, -614f), new Vector2(1150f, 22f), "", 14, s_muted);
            DrawButton(canvas, new Vector2(56f, -656f), "First group", demo.JumpToFirst);
            DrawButton(canvas, new Vector2(214f, -656f), "Last group", demo.JumpToLast);
            DrawButton(canvas, new Vector2(372f, -656f), "Inner lists: last", demo.JumpInnerToLast, 190f);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath(horizontal ? "HorizontalNestedVertical" : "VerticalNestedHorizontal"));
        }

        private static VirtualScrollView CreateList(RectTransform canvas, RectTransform prefab, bool horizontal, float height = 450f)
        {
            RectTransform host = CreateRect("ScrollView", canvas, new Vector2(56f, -150f), new Vector2(1168f, height));
            AddImage(host, ColorOf("#101c2b")).raycastTarget = true;
            RectTransform viewport = CreateRect("Viewport", host, Vector2.zero, host.sizeDelta);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect("Content", viewport, Vector2.zero, new Vector2(1168f, height));
            VirtualScrollRect scrollRect = host.gameObject.AddComponent<VirtualScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = horizontal;
            scrollRect.vertical = !horizontal;
            scrollRect.scrollSensitivity = 36f;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            VirtualScrollView list = host.gameObject.AddComponent<VirtualScrollView>();
            list.ItemPrefabs = new[] { prefab };
            list.Content = content;
            list.Viewport = viewport;
            list.ScrollRect = scrollRect;
            list.ContentPadding = new RectOffset(20, 20, 20, 20);
            list.ContentSpacing = 12f;
            list.Orientation = horizontal ? ScrollOrientation.Horizontal : ScrollOrientation.Vertical;
            list.StretchItems = true;
            return list;
        }

        private static void CreateCatalogScene(bool horizontal, RectTransform prefab)
        {
            RectTransform canvas = CreateShell(horizontal ? "02" : "01", horizontal ? "A collection in motion" : "Room for every adventure", horizontal ? "Horizontal list / variable card widths / drag or use the mouse wheel" : "Vertical list / variable row heights / 1,000 items with recycled views");
            CatalogScrollDemo demo = canvas.gameObject.AddComponent<CatalogScrollDemo>();
            demo.ScrollView = CreateList(canvas, prefab, horizontal);
            demo.Icons = LoadIcons();
            demo.Status = DrawText("Status", canvas, new Vector2(56f, -614f), new Vector2(1150f, 22f), "", 14, s_muted);
            DrawButton(canvas, new Vector2(56f, -656f), "First item", demo.JumpToFirst);
            DrawButton(canvas, new Vector2(214f, -656f), "Last item", demo.JumpToLast);
            DrawButton(canvas, new Vector2(372f, -656f), "Resize + jump", demo.ResizeMiddleItem, 190f);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath(horizontal ? "HorizontalList" : "VerticalList"));
        }

        private static void CreateInventoryScene(RectTransform slot)
        {
            RectTransform canvas = CreateShell("03", "Pack for the next chapter", "Inventory grid / click a slot to select / automatic columns / 503 items");
            DirectGridView grid = CreateDirectGrid(canvas, slot);
            grid.CellSize = new Vector2(144f, 144f);
            grid.CellSpacing = new Vector2(12f, 12f);
            grid.ColumnCount = 0;
            InventoryGridDemo demo = canvas.gameObject.AddComponent<InventoryGridDemo>();
            demo.Grid = grid;
            demo.Status = DrawText("Status", canvas, new Vector2(56f, -614f), new Vector2(520f, 22f), "", 14, s_muted);
            demo.SelectedItem = DrawText("Selected", canvas, new Vector2(620f, -614f), new Vector2(600f, 24f), "Select a slot to inspect or use an item", 14, s_accent);
            DrawButton(canvas, new Vector2(56f, -656f), "First slot", demo.JumpToFirst);
            DrawButton(canvas, new Vector2(214f, -656f), "Last slot", demo.JumpToLast);
            DrawButton(canvas, new Vector2(372f, -656f), "Use selected", demo.UseSelectedItem, 190f);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath("InventoryGrid"));
        }

        private static void CreateItemDragScene(RectTransform row, RectTransform slot)
        {
            RectTransform canvas = CreateShell("07", "Move items, make room", "Drag the MOVE handle to swap items / drag the rest of a slot to scroll the list");
            VirtualScrollView list = CreateList(canvas, row, false);
            VirtualGridView grid = list.gameObject.AddComponent<VirtualGridView>();
            grid.RowScrollView = list;
            grid.CellPrefab = slot;
            grid.CellSize = new Vector2(144f, 144f);
            grid.CellSpacing = new Vector2(12f, 12f);
            ItemDragDemo demo = canvas.gameObject.AddComponent<ItemDragDemo>();
            demo.Grid = grid;
            demo.Status = DrawText("Status", canvas, new Vector2(56f, -624f), new Vector2(1168f, 48f), "Drag the MOVE handle to swap items / drag the rest of a slot to scroll / drop outside to cancel", 16, s_accent);
            demo.DragLayer = CreateRect("DragLayer", canvas, Vector2.zero, canvas.sizeDelta);
            Stretch(demo.DragLayer);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath("ItemDrag"));
        }

        private static void CreateRewardScene(RectTransform row, RectTransform slot)
        {
            RectTransform canvas = CreateShell("08", "A little celebration for every reward", "Rewards pop in one by one / fade, rise and bounce / receive again to replay");
            VirtualScrollView list = CreateList(canvas, row, false);
            VirtualGridView grid = list.gameObject.AddComponent<VirtualGridView>();
            grid.RowScrollView = list;
            grid.CellPrefab = slot;
            grid.CellSize = new Vector2(144f, 144f);
            grid.CellSpacing = new Vector2(18f, 18f);
            // The default batch extends beyond the viewport so scrolling is visible.
            grid.ColumnCount = 6;
            RewardRevealDemo demo = canvas.gameObject.AddComponent<RewardRevealDemo>();
            demo.Grid = grid;
            demo.Status = DrawText("Status", canvas, new Vector2(56f, -614f), new Vector2(1168f, 24f), "", 16, s_accent);
            DrawButton(canvas, new Vector2(56f, -656f), "Receive rewards", demo.ReceiveRewards, 190f);
            DrawButton(canvas, new Vector2(264f, -656f), "Show all now", demo.ShowAllRewards, 172f);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath("RewardReveal"));
        }

        private static void CreateChatScene(RectTransform[] itemPrefabs)
        {
            RectTransform canvas = CreateShell("04", "Keep the conversation flowing", "Configurable message styles / incoming on the left, your messages on the right / dynamic bubble heights");
            ChatScrollDemo demo = canvas.gameObject.AddComponent<ChatScrollDemo>();
            demo.ScrollView = CreateList(canvas, itemPrefabs[0], false, 410f);
            demo.ScrollView.ItemPrefabs = itemPrefabs;
            demo.Status = DrawText("Status", canvas, new Vector2(56f, -574f), new Vector2(1100f, 20f), "", 14, s_muted);
            demo.MessageInput = DrawInput(canvas, new Vector2(56f, -610f), new Vector2(810f, 44f));
            DrawButton(canvas, new Vector2(882f, -610f), "Send", demo.SendMessage, 140f);
            DrawButton(canvas, new Vector2(1038f, -610f), "Add reply", demo.AddReceivedMessage, 186f);
            DrawButton(canvas, new Vector2(56f, -666f), "First message", demo.JumpToFirst, 172f);
            DrawButton(canvas, new Vector2(244f, -666f), "Latest", demo.JumpToLast, 140f);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath("Chat"));
        }

        private static Texture2D[] LoadIcons()
        {
            var icons = new Texture2D[5];
            for (var index = 0; index < icons.Length; ++index) {
                icons[index] = AssetDatabase.LoadAssetAtPath<Texture2D>(s_root + "/Textures/DemoIcon" + (index + 1) + ".png");
            }

            return icons;
        }

        private static void DrawButton(Transform parent, Vector2 position, string label, UnityAction callback, float width = 142f)
        {
            Image image = DrawPanel(label, parent, position, new Vector2(width, 36f), ColorOf("#28453f"));
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text text = DrawText("Label", image.transform, Vector2.zero, new Vector2(width, 36f), label, 14, s_accent, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            UnityEventTools.AddPersistentListener(button.onClick, callback);
        }

        private static InputField DrawInput(Transform parent, Vector2 position, Vector2 size)
        {
            Image image = DrawPanel("Message input", parent, position, size, s_panel);
            image.raycastTarget = true;
            InputField input = image.gameObject.AddComponent<InputField>();
            Text text = DrawText("Text", image.transform, new Vector2(14f, -8f), size - new Vector2(28f, 16f), "", 16, s_text);
            Text placeholder = DrawText("Placeholder", image.transform, new Vector2(14f, -8f), size - new Vector2(28f, 16f), "Write a message...", 16, s_muted);
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = image;
            return input;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            root.layer = 5;
            return rect;
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image DrawPanel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            return AddImage(CreateRect(name, parent, position, size), color);
        }

        private static Text DrawText(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color, FontStyle style = FontStyle.Normal)
        {
            Text text = CreateRect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = s_font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void StretchX(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Color ColorOf(string value)
        {
            ColorUtility.TryParseHtmlString(value, out Color color);
            return color;
        }
    }
}
