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
        [MenuItem("Tools/ZRList/Rebuild Business Sorted Tree Assets")]
        public static void GenerateBusinessSortedTree()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            InitializePaths();
            RectTransform branch = SavePrefab(CreateBusinessSortPrefab(true), "BusinessSortBranch");
            RectTransform task = SavePrefab(CreateBusinessSortPrefab(false), "BusinessSortTask");
            RectTransform canvas = CreateShell("13", "Watch priorities change the order",
                "Four-level task tree / compare independent business flags / click a task to select it");
            VirtualScrollView list = CreateList(canvas, branch, false, 430f, showScrollbar: true);
            ((RectTransform)list.transform).sizeDelta = new Vector2(760f, 430f);
            list.ItemPrefabs = new[] { branch, task };
            list.ContentSpacing = 8f;
            var demo = canvas.gameObject.AddComponent<BusinessSortedTreeDemo>();
            demo.ScrollView = list;
            demo.BranchPrefab = branch;
            demo.TaskPrefab = task;
            DrawPanel("BusinessControls", canvas, new Vector2(840f, -150f), new Vector2(384f, 430f), s_panel);
            DrawText("RuleTitle", canvas, new Vector2(860f, -166f), new Vector2(344f, 24f), "BUSINESS PRIORITY", 15, s_accent, FontStyle.Bold);
            DrawText("Rules", canvas, new Vector2(860f, -196f), new Vector2(344f, 86f),
                "1  Red dot first\n2  Unlocked first\n3  Unread first\n4  ID ascending", 16, s_text);
            demo.Selection = DrawText("Selection", canvas, new Vector2(860f, -291f), new Vector2(344f, 43f), "", 14, s_text);
            DrawButton(canvas, new Vector2(860f, -344f), "Toggle notification", demo.ToggleNotification, 344f);
            demo.NotificationLabel = canvas.Find("Toggle notification/Label").GetComponent<Text>();
            DrawButton(canvas, new Vector2(860f, -388f), "Toggle unlock", demo.ToggleUnlock, 344f);
            demo.UnlockLabel = canvas.Find("Toggle unlock/Label").GetComponent<Text>();
            DrawButton(canvas, new Vector2(860f, -432f), "Toggle read", demo.ToggleRead, 344f);
            demo.ReadLabel = canvas.Find("Toggle read/Label").GetComponent<Text>();
            demo.OrderSummary = DrawText("RootOrder", canvas, new Vector2(860f, -489f), new Vector2(344f, 70f), "", 16, s_muted);
            demo.LastAction = DrawText("LastAction", canvas, new Vector2(56f, -594f), new Vector2(1168f, 32f), "", 15, s_accent);
            DrawButton(canvas, new Vector2(56f, -638f), "Root overview", demo.ShowRoots, 156f);
            DrawButton(canvas, new Vector2(228f, -638f), "Find selected task", demo.ShowSelected, 190f);
            DrawButton(canvas, new Vector2(434f, -638f), "Expand all", demo.ExpandAll, 142f);
            DrawButton(canvas, new Vector2(592f, -638f), "Reset scenario", demo.ResetScenario, 170f);
            string path = ScenePath("BusinessSortedTree");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(scene => scene.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("BUSINESS_SORT_ASSETS_GENERATED");
        }

        private static GameObject CreateBusinessSortPrefab(bool branch)
        {
            GameObject root = CreateExpandablePrefab(branch);
            root.transform.Find("Detail").GetComponent<Text>().fontSize = 13;
            return root;
        }
    }
}
