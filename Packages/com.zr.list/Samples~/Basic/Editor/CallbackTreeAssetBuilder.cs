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
        /// <summary>重建 OnItemRender 折叠树示例，复用现有分支与叶子预制体。</summary>
        [MenuItem("Tools/ZRList/Rebuild Callback Tree Assets")]
        public static void GenerateCallbackTree()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }
            InitializePaths();
            RectTransform branch = AssetDatabase.LoadAssetAtPath<RectTransform>(s_root + "/Prefabs/Showcase/TreeBranch.prefab");
            RectTransform lesson = AssetDatabase.LoadAssetAtPath<RectTransform>(s_root + "/Prefabs/Showcase/TreeLeaf.prefab");
            if (branch == null) {
                branch = SavePrefab(CreateExpandablePrefab(true), "TreeBranch");
            }
            if (lesson == null) {
                lesson = SavePrefab(CreateExpandablePrefab(false), "TreeLeaf");
            }

            RectTransform canvas = CreateShell("14", "One tree, simple callbacks",
                "Chapters > sections > lessons / click to fold or mark read / rendered with OnItemRender");
            VirtualScrollView list = CreateList(canvas, branch, false, 430f);
            list.ItemPrefabs = new[] { branch, lesson };
            list.ContentSpacing = 6f;
            var demo = canvas.gameObject.AddComponent<CallbackTreeDemo>();
            demo.ScrollView = list;
            demo.BranchPrefab = branch;
            demo.LessonPrefab = lesson;
            demo.SortStatus = DrawText("SortStatus", canvas, new Vector2(56f, -596f), new Vector2(1168f, 24f), "", 15, s_accent);
            DrawButton(canvas, new Vector2(56f, -638f), "Expand all", demo.ExpandAll, 130f);
            DrawButton(canvas, new Vector2(198f, -638f), "Collapse all", demo.CollapseAll, 130f);
            DrawButton(canvas, new Vector2(340f, -638f), "Toggle unread priority", demo.ToggleUnreadPriority, 218f);
            DrawButton(canvas, new Vector2(570f, -638f), "Find lesson 8408", demo.FindLastLesson, 180f);

            string path = ScenePath("CallbackTree");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(scene => scene.path == path)) {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("CALLBACK_TREE_ASSETS_GENERATED");
        }
    }
}
