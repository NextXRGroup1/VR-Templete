#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Training.Core;
using Training.Flow.Data;
using Training.Flow.Controllers;
using Training.Flow.Components;
using Training.Assessment.Data;
using Training.Assessment.Controllers;
using Training.Question.Data;
using Training.Question.Controllers;
using Training.Trigger.Controllers;
using Training.UI.Components;
using Training.UI.Controllers;
using Training.UI.Panels;
using Training.UI.Checklist;
using Training.UI.Progress;
using Training.Integration;

namespace Training.EditorTools
{
    /// <summary>
    /// Builds Checklist + Progress prefabs and a mini sample scene flow.
    /// Menu: Training → Setup Checklist & Progress Demo
    /// </summary>
    public static class ChecklistProgressDemoSetup
    {
        private const string AssetsFolder = "Assets/TrainingDemo";
        private const string PrefabFolder = "Assets/TrainingDemo/Prefabs";

        [MenuItem("Training/Setup Checklist & Progress Demo", priority = 1)]
        public static void Setup()
        {
            if (!EditorUtility.DisplayDialog(
                    "Checklist & Progress Demo",
                    "Creates:\n" +
                    "• ChecklistPanel + Row + ProgressPanel prefabs\n" +
                    "• Sample flow: Text → Checklist wait → Progress wait → Quiz → End\n" +
                    "• Keys 1/2/3 mark checklist, +/- add progress\n\n" +
                    "Assets under Assets/TrainingDemo/\nContinue?",
                    "Create", "Cancel"))
                return;

            EnsureFolder(AssetsFolder);
            EnsureFolder(PrefabFolder);

            var rowPrefab = BuildAndSaveRowPrefab();
            var checklistPrefab = BuildAndSaveChecklistPrefab(rowPrefab);
            var progressPrefab = BuildAndSaveProgressPrefab();

            var assessment = CreateAssessment();
            var flow = CreateFlow(assessment);

            EnsureCamera();
            EnsureEventSystem();

            var existing = GameObject.Find("TrainingSystem_ChecklistProgress");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject("TrainingSystem_ChecklistProgress");
            Undo.RegisterCreatedObjectUndo(root, "CP Demo");

            var audioGo = new GameObject("AudioSource");
            audioGo.transform.SetParent(root.transform, false);
            var audio = audioGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            var ctrl = new GameObject("Controllers");
            ctrl.transform.SetParent(root.transform, false);

            var trigger = ctrl.AddComponent<TriggerFlowController>();
            var question = ctrl.AddComponent<QuestionController>();
            var assessmentCtrl = ctrl.AddComponent<AssessmentController>();
            var score = ctrl.AddComponent<TrainingScoreController>();
            var flowCtrl = ctrl.AddComponent<TrainingFlowController>();
            var session = ctrl.AddComponent<TrainingSessionController>();
            var runner = ctrl.AddComponent<FlowRunner>();

            SetRef(assessmentCtrl, "_questionController", question);
            SetRef(assessmentCtrl, "_audioSource", audio);
            SetRef(flowCtrl, "_triggerFlowController", trigger);
            SetRef(flowCtrl, "_assessmentController", assessmentCtrl);
            SetRef(flowCtrl, "_scoreController", score);
            SetRef(flowCtrl, "_audioSource", audio);
            SetRef(session, "_flowController", flowCtrl);
            SetRef(session, "_scoreController", score);

            var sessionSo = new SerializedObject(session);
            var list = sessionSo.FindProperty("_availableFlowConfigs");
            list.ClearArray();
            list.InsertArrayElementAtIndex(0);
            list.GetArrayElementAtIndex(0).objectReferenceValue = flow;
            sessionSo.ApplyModifiedPropertiesWithoutUndo();

            SetRef(runner, "_sessionController", session);
            var runnerSo = new SerializedObject(runner);
            runnerSo.FindProperty("_startOnAwake").boolValue = true;
            runnerSo.ApplyModifiedPropertiesWithoutUndo();

            // Checklist + Progress controllers
            var cp = new GameObject("ChecklistProgress");
            cp.transform.SetParent(root.transform, false);
            var checklist = cp.AddComponent<ChecklistController>();
            var progress = cp.AddComponent<ProgressController>();

            checklist.Setup(new List<ChecklistItemDef>
            {
                new ChecklistItemDef { Id = "Inspect", Label = "Inspect valve" },
                new ChecklistItemDef { Id = "Close", Label = "Close hatch" },
                new ChecklistItemDef { Id = "Confirm", Label = "Confirm clear" }
            });
            progress.Setup("Fill tank", 0f, 100f, completeAtMax: true, manualOnly: false);

            var bridge = cp.AddComponent<ChecklistProgressFlowBridge>();
            SetRef(bridge, "checklist", checklist);
            SetRef(bridge, "progress", progress);
            // completionTrigger default TaskCompleted — flow uses TaskCompleted then Continue
            // We'll use two triggers: first checklist → TaskCompleted, second progress → Continue
            // Bridge only has one trigger — use a dual-phase helper instead
            Object.DestroyImmediate(bridge);

            var phase = cp.AddComponent<ChecklistThenProgressBridge>();
            SetRef(phase, "checklist", checklist);
            SetRef(phase, "progress", progress);

            var input = cp.AddComponent<ChecklistProgressDemoInput>();
            input.Checklist = checklist;
            input.Progress = progress;
            input.ChecklistIds = new[] { "Inspect", "Close", "Confirm" };
            input.ProgressStep = 25f;

            // UI
            var canvas = CreateCanvas(root.transform);
            var factoryGo = new GameObject("UIPanelFactory");
            factoryGo.transform.SetParent(root.transform, false);
            var factory = factoryGo.AddComponent<UIPanelFactory>();
            AssignPrefabs(factory, canvas.transform, checklistPrefab, progressPrefab);

            var uiGo = new GameObject("UIFlowController");
            uiGo.transform.SetParent(root.transform, false);
            var uiFlow = uiGo.AddComponent<UIFlowController>();
            SetRef(uiFlow, "_panelFactory", factory);
            SetRef(uiFlow, "_flowController", flowCtrl);
            SetRef(uiFlow, "_assessmentController", assessmentCtrl);
            SetRef(uiFlow, "_questionController", question);
            SetRef(uiFlow, "_sessionController", session);

            // Live panels in scene (always visible for demo)
            var live = new GameObject("LiveStatusUI");
            live.transform.SetParent(canvas.transform, false);
            var clInstance = (ChecklistPanel)PrefabUtility.InstantiatePrefab(checklistPrefab, live.transform);
            var prInstance = (ProgressPanel)PrefabUtility.InstantiatePrefab(progressPrefab, live.transform);
            if (clInstance != null)
            {
                var rt = clInstance.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0.5f);
                    rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(24f, 80f);
                }
                clInstance.Bind(checklist, "Pre-checks");
                clInstance.Show();
            }
            if (prInstance != null)
            {
                var rt = prInstance.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0.5f);
                    rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(24f, -100f);
                }
                prInstance.Bind(progress);
                prInstance.Show();
            }

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Checklist & Progress Demo Ready",
                "Play Mode:\n" +
                "1) Read intro text\n" +
                "2) Press 1, 2, 3 → checklist items check → advances\n" +
                "3) Press = or + four times → progress 100% → advances\n" +
                "4) Quiz → complete\n\n" +
                "Prefabs:\n" + PrefabFolder,
                "OK");
        }

        // ---------- Prefabs ----------

        private static ChecklistRowView BuildAndSaveRowPrefab()
        {
            var path = PrefabFolder + "/ChecklistRow.prefab";
            var go = new GameObject("ChecklistRow", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360, 36);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.16f, 0.85f);

            var empty = new GameObject("Empty", typeof(RectTransform), typeof(Image));
            empty.transform.SetParent(go.transform, false);
            var ert = empty.GetComponent<RectTransform>();
            ert.anchorMin = ert.anchorMax = ert.pivot = new Vector2(0f, 0.5f);
            ert.anchoredPosition = new Vector2(18f, 0f);
            ert.sizeDelta = new Vector2(18f, 18f);
            empty.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.55f);

            var check = new GameObject("Check", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(go.transform, false);
            var crt = check.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 0.5f);
            crt.anchoredPosition = new Vector2(18f, 0f);
            crt.sizeDelta = new Vector2(18f, 18f);
            check.GetComponent<Image>().color = new Color(0.2f, 0.85f, 0.4f);
            check.SetActive(false);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(44f, 2f);
            lrt.offsetMax = new Vector2(-8f, -2f);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.text = "Item";

            var row = go.AddComponent<ChecklistRowView>();
            row.Wire(tmp, check, empty);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ChecklistRowView>();
        }

        private static ChecklistPanel BuildAndSaveChecklistPrefab(ChecklistRowView rowPrefab)
        {
            var path = PrefabFolder + "/ChecklistPanel.prefab";
            var go = new GameObject("ChecklistPanel", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(380, 200);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(go.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -8f);
            trt.sizeDelta = new Vector2(-16f, 28f);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.fontSize = 18;
            title.color = Color.white;
            title.alignment = TextAlignmentOptions.Center;
            title.text = "Checklist";

            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            list.transform.SetParent(go.transform, false);
            var lrt = list.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(12f, 12f);
            lrt.offsetMax = new Vector2(-12f, -40f);
            var vlg = list.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var panel = go.AddComponent<ChecklistPanel>();
            panel.Wire(title, list.transform, rowPrefab);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ChecklistPanel>();
        }

        private static ProgressPanel BuildAndSaveProgressPrefab()
        {
            var path = PrefabFolder + "/ProgressPanel.prefab";
            var go = new GameObject("ProgressPanel", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(380, 90);

            go.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(go.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -6f);
            trt.sizeDelta = new Vector2(-16f, 24f);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.fontSize = 16;
            title.color = Color.white;
            title.alignment = TextAlignmentOptions.Center;
            title.text = "Progress";

            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(go.transform, false);
            var vrt = valueGo.GetComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0f, 1f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(0.5f, 1f);
            vrt.anchoredPosition = new Vector2(0f, -30f);
            vrt.sizeDelta = new Vector2(-16f, 20f);
            var value = valueGo.AddComponent<TextMeshProUGUI>();
            value.fontSize = 14;
            value.color = new Color(0.8f, 0.9f, 1f);
            value.alignment = TextAlignmentOptions.Center;
            value.text = "0 / 100";

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(go.transform, false);
            var trackRt = track.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0f, 0f);
            trackRt.anchorMax = new Vector2(1f, 0f);
            trackRt.pivot = new Vector2(0.5f, 0f);
            trackRt.anchoredPosition = new Vector2(0f, 14f);
            trackRt.sizeDelta = new Vector2(-24f, 16f);
            track.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fill.GetComponent<Image>();
            fillImg.color = new Color(0.25f, 0.7f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;

            var panel = go.AddComponent<ProgressPanel>();
            panel.Wire(title, value, fillImg);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ProgressPanel>();
        }

        // ---------- Flow / assessment ----------

        private static TrainingFlowConfig CreateFlow(AssessmentConfig assessment)
        {
            var path = AssetsFolder + "/Flow_ChecklistProgress.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var f = ScriptableObject.CreateInstance<TrainingFlowConfig>();
            f.Title = "Checklist & Progress Demo";
            f.ShowCompletionScreen = true;
            f.CalculateScore = true;
            f.StartNodeId = "C01";
            f.Nodes = new List<FlowNodeData>
            {
                new FlowNodeData
                {
                    Id = "C01", NodeType = FlowNodeType.Text, NextNodeId = "C02",
                    Navigation = NavigationMode.None, AutoAdvanceDelaySeconds = 2.5f,
                    TextContent = "Checklist & Progress demo.\n\nPress 1, 2, 3 to complete pre-checks."
                },
                new FlowNodeData
                {
                    Id = "C02", NodeType = FlowNodeType.Trigger, NextNodeId = "C03",
                    Trigger = TrainingTrigger.TaskCompleted,
                    TextContent = "Waiting for all checklist items (keys 1 / 2 / 3)."
                },
                new FlowNodeData
                {
                    Id = "C03", NodeType = FlowNodeType.Text, NextNodeId = "C04",
                    Navigation = NavigationMode.None, AutoAdvanceDelaySeconds = 2f,
                    TextContent = "Pre-checks done.\n\nFill the tank: press = or + until 100%."
                },
                new FlowNodeData
                {
                    Id = "C04", NodeType = FlowNodeType.Trigger, NextNodeId = "C05",
                    Trigger = TrainingTrigger.Continue,
                    TextContent = "Waiting for progress to reach max (keys + / =)."
                },
                new FlowNodeData
                {
                    Id = "C05", NodeType = FlowNodeType.Assessment, NextNodeId = "C06",
                    AssessmentReference = assessment
                },
                new FlowNodeData
                {
                    Id = "C06", NodeType = FlowNodeType.Text, NextNodeId = "",
                    Navigation = NavigationMode.None, AutoAdvanceDelaySeconds = 2f,
                    TextContent = "Demo finished."
                }
            };
            AssetDatabase.CreateAsset(f, path);
            if (!f.Validate(out var err))
                Debug.LogWarning("Flow validation: " + err);
            return f;
        }

        private static AssessmentConfig CreateAssessment()
        {
            var path = AssetsFolder + "/Assessment_CP.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var c = ScriptableObject.CreateInstance<AssessmentConfig>();
            c.AssessmentId = "CP_Quiz";
            c.ShowResultsScreen = true;
            c.PassThresholdPercent = 50f;
            c.NavigationMode = AssessmentNavigationMode.Sequential;
            c.Questions = new List<QuestionData>
            {
                new QuestionData
                {
                    Id = "Q1", QuestionType = QuestionType.YesNo,
                    QuestionText = "Did you complete all checklist items?",
                    CorrectYesNoAnswer = true, Points = 1f
                }
            };
            AssetDatabase.CreateAsset(c, path);
            return c;
        }

        private static void AssignPrefabs(UIPanelFactory factory, Transform parent,
            ChecklistPanel checklistPrefab, ProgressPanel progressPrefab)
        {
            var so = new SerializedObject(factory);
            so.FindProperty("_panelParent").objectReferenceValue = parent;
            so.FindProperty("_textPanelPrefab").objectReferenceValue = FindPrefab<TextPanel>("TextPanel");
            so.FindProperty("_textImagePanelPrefab").objectReferenceValue = FindPrefab<TextImagePanel>("TextImagePanel");
            so.FindProperty("_assessmentPanelPrefab").objectReferenceValue = FindPrefab<AssessmentPanel>("AssessmentPanel");
            so.FindProperty("_assessmentResultPanelPrefab").objectReferenceValue =
                FindPrefab<AssessmentResultPanel>("AssementResultPanel") ??
                FindPrefab<AssessmentResultPanel>("AssessmentResultPanel");
            so.FindProperty("_trainingCompletePanelPrefab").objectReferenceValue =
                FindPrefab<TrainingCompletePanel>("TrainingCompletePanel");
            so.FindProperty("_trainingMenuPanelPrefab").objectReferenceValue =
                FindPrefab<TrainingMenuPanel>("TrainingMenuPanel");
            var cl = so.FindProperty("_checklistPanelPrefab");
            if (cl != null) cl.objectReferenceValue = checklistPrefab;
            var pr = so.FindProperty("_progressPanelPrefab");
            if (pr != null) pr.objectReferenceValue = progressPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T FindPrefab<T>(string name) where T : Component
        {
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains(name)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var c = go.GetComponent<T>() ?? go.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        private static GameObject CreateCanvas(Transform parent)
        {
            var go = new GameObject("TrainingCanvas");
            go.transform.SetParent(parent, false);
            go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 1.5f, -5f);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static void SetRef(Object obj, string field, Object value)
        {
            var so = new SerializedObject(obj);
            var p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"Missing field '{field}' on {obj.GetType().Name}");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
