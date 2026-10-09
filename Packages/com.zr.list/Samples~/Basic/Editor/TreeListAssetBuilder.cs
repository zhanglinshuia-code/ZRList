// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZRList.Samples.Editor
{
    public static partial class ScrollViewDemoAssetBuilder
    {
        [MenuItem("Tools/ZRList/Rebuild Tree List Assets")]
        public static void GenerateTree()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            InitializePaths();
            RectTransform branch = SavePrefab(CreateExpandablePrefab(true), "TreeBranch");
            RectTransform leaf = SavePrefab(CreateExpandablePrefab(false), "TreeLeaf");
            RectTransform canvas = CreateShell("12", "Every task has a place",
                "Campaign > chapter > section > task / fold any branch / activity reorders siblings at every level");
            VirtualScrollView list = CreateList(canvas, branch, false, 430f, showScrollbar: true);
            list.ItemPrefabs = new[] { branch, leaf };
            list.ContentSpacing = 6f;
            TreeListDemo demo = canvas.gameObject.AddComponent<TreeListDemo>();
            demo.ScrollView = list;
            demo.BranchPrefab = branch;
            demo.LeafPrefab = leaf;
            demo.Status = DrawText("Status", canvas, new Vector2(56f, -596f), new Vector2(1168f, 24f), "", 14, s_muted);
            DrawButton(canvas, new Vector2(56f, -638f), "Expand all", demo.ExpandAll, 130f);
            DrawButton(canvas, new Vector2(198f, -638f), "Collapse all", demo.CollapseAll, 130f);
            DrawButton(canvas, new Vector2(340f, -638f), "Notify task 6435", demo.NotifyLastTask, 184f);
            DrawButton(canvas, new Vector2(536f, -638f), "Mark all read", demo.MarkAllRead, 154f);
            DrawButton(canvas, new Vector2(702f, -638f), "Find task 6435", demo.FindLastTask, 200f);
            string path = ScenePath("TreeList");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(scene => scene.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("TREE_ASSETS_GENERATED");
        }
    }
}
