// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZRList.Samples.Editor
{
    public static partial class ScrollViewDemoAssetBuilder
    {
        [MenuItem("Tools/ZRList/Rebuild Direct Reward Reveal Assets")]
        public static void GenerateDirectRewards()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }

            InitializePaths();
            RectTransform slot = AssetDatabase.LoadAssetAtPath<RectTransform>(s_root + "/Prefabs/Showcase/RewardSlot.prefab");
            if (slot == null) {
                slot = SavePrefab(CreateRewardPrefab(), "RewardSlot");
            }
            CreateDirectRewardScene(slot);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string path = ScenePath("DirectRewardReveal");
            if (!scenes.Exists(scene => scene.path == path)) {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("DIRECT_REWARD_REVEAL_ASSETS_GENERATED");
        }

        private static DirectGridView CreateDirectGrid(RectTransform canvas, RectTransform slot)
        {
            RectTransform host = CreateRect("ScrollView", canvas, new Vector2(56f, -150f), new Vector2(1168f, 450f));
            AddImage(host, ColorOf("#101c2b")).raycastTarget = true;
            RectTransform viewport = CreateRect("Viewport", host, Vector2.zero, host.sizeDelta);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect("Content", viewport, Vector2.zero, host.sizeDelta);
            ScrollRect scrollRect = host.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 36f;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            DirectGridView grid = host.gameObject.AddComponent<DirectGridView>();
            grid.ScrollRect = scrollRect;
            grid.ContentPadding = new RectOffset(20, 20, 20, 20);
            grid.CellPrefab = slot;
            return grid;
        }

        private static void CreateDirectRewardScene(RectTransform slot)
        {
            RectTransform canvas = CreateShell("10", "One reward, then another", "24 rewards / pop in one by one / drag or use the mouse wheel to explore");
            DirectGridView grid = CreateDirectGrid(canvas, slot);
            grid.CellSize = new Vector2(144f, 144f);
            // Leave room for both adjacent visuals at their largest bounce.
            grid.CellSpacing = new Vector2(28f, 28f);
            grid.ContentPadding = new RectOffset(32, 32, 32, 32);
            grid.ColumnCount = 6;
            DirectRewardRevealDemo demo = canvas.gameObject.AddComponent<DirectRewardRevealDemo>();
            demo.Grid = grid;
            demo.Status = DrawText("Status", canvas, new Vector2(56f, -614f), new Vector2(1168f, 24f), "", 16, s_accent);
            DrawButton(canvas, new Vector2(56f, -656f), "Receive rewards", demo.ReceiveRewards, 190f);
            DrawButton(canvas, new Vector2(264f, -656f), "Show all now", demo.ShowAllRewards, 172f);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath("DirectRewardReveal"));
        }
    }
}
