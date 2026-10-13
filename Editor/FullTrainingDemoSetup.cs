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
using Training.Sequence;
using Training.Integration;

namespace Training.EditorTools
{
    /// <summary>
    /// Full combined demo: VR Template flow + Sequence activity between nodes + bridge.
    /// Menu: Training → Setup Full Combined Demo
    /// </summary>
    public static class FullTrainingDemoSetup
    {
        private const string AssetsFolder = "Assets/TrainingDemo";
        private const string RootName = "TrainingSystem";

        [MenuItem("Training/Setup Full Combined Demo", priority = 0)]
        public static void Setup()
        {
            if (!EditorUtility.DisplayDialog(
                    "Setup Full Combined Demo",
                    "Creates a complete training with:\n\n" +
                    "• Flow: Text → Activity (Sequence) → Assessment → End\n" +
                    "• Guided sequence (Helmet Select, Gloves Equip, Glasses Select)\n" +
                    "• SequenceToFlowBridge connecting both systems\n" +
                    "• ScriptableObjects under Assets/TrainingDemo/\n" +
                    "• Full hierarchy + UI + Continue for non-sequence triggers\n\nContinue?",
                    "Create", "Cancel"))
                return;

            EnsureFolder(AssetsFolder);

            var assessment = CreateAssessment();
            var flow = CreateFlowWithActivity(assessment);

            EnsureCamera();
            EnsureEventSystem();

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create TrainingSystem");

            // --- Audio ---
            var audioGo = new GameObject("AudioSource");
            audioGo.transform.SetParent(root.transform, false);
            var audio = audioGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            // --- Flow controllers ---
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

            // --- UI ---
            var canvas = CreateCanvas(root.transform);
            var factoryGo = new GameObject("UIPanelFactory");
            factoryGo.transform.SetParent(root.transform, false);
            var factory = factoryGo.AddComponent<UIPanelFactory>();
            AssignPrefabs(factory, canvas.transform);

            var uiGo = new GameObject("UIFlowController");
            uiGo.transform.SetParent(root.transform, false);
            var uiFlow = uiGo.AddComponent<UIFlowController>();
            SetRef(uiFlow, "_panelFactory", factory);
            SetRef(uiFlow, "_flowController", flowCtrl);
            SetRef(uiFlow, "_assessmentController", assessmentCtrl);
            SetRef(uiFlow, "_questionController", question);
            SetRef(uiFlow, "_sessionController", session);

            // --- Sequence activity ---
            var seqRoot = new GameObject("SequenceActivity");
            seqRoot.transform.SetParent(root.transform, false);

            var seqUi = CreateSequenceUI(seqRoot.transform);
            var highlighter = seqRoot.AddComponent<SimpleHighlighter>();
            var sequence = seqRoot.AddComponent<GuidedSequenceController>();

            var seqSo = new SerializedObject(sequence);
            seqSo.FindProperty("highlighterComponent").objectReferenceValue = highlighter;
            seqSo.FindProperty("uiComponent").objectReferenceValue = seqUi;
            seqSo.FindProperty("Mode").enumValueIndex = (int)GuidanceMode.Guided;
            seqSo.ApplyModifiedPropertiesWithoutUndo();

            // Demo objects + actions
            var objects = new GameObject("ActivityObjects");
            objects.transform.SetParent(seqRoot.transform, false);

            var helmet = CreateCube(objects.transform, "Helmet", new Vector3(-1.5f, 1f, 0f), new Color(0.2f, 0.6f, 1f));
            var gloves = CreateCube(objects.transform, "Gloves", new Vector3(0f, 1f, 0f), new Color(0.2f, 0.9f, 0.4f));
            var glasses = CreateCube(objects.transform, "Glasses", new Vector3(1.5f, 1f, 0f), new Color(1f, 0.7f, 0.2f));
            var socket = new GameObject("GlovesSocket");
            socket.transform.SetParent(objects.transform, false);
            socket.transform.position = gloves.transform.position + Vector3.up * 0.3f;

            var actions = new GameObject("Actions");
            actions.transform.SetParent(seqRoot.transform, false);

            var selectHelmet = actions.AddComponent<SelectAction>();
            SetPrivate(selectHelmet, "target", helmet);
            var equipGloves = actions.AddComponent<EquipAction>();
            SetPrivate(equipGloves, "item", gloves);
            SetPrivate(equipGloves, "socket", socket.transform);
            var selectGlasses = actions.AddComponent<SelectAction>();
            SetPrivate(selectGlasses, "target", glasses);

            // Steps
            seqSo = new SerializedObject(sequence);
            var steps = seqSo.FindProperty("Steps");
            steps.ClearArray();
            AddSeqStep(steps, 0, "Select the helmet", "Safety Helmet",
                "Select the highlighted helmet.", "Click the blue cube.", selectHelmet);
            AddSeqStep(steps, 1, "Equip the gloves", "Safety Gloves",
                "Equip gloves on the socket.", "Click the green cube.", equipGloves);
            AddSeqStep(steps, 2, "Select the glasses", "Safety Glasses",
                "Select the safety glasses.", "Click the orange cube.", selectGlasses);
            seqSo.ApplyModifiedPropertiesWithoutUndo();

            // Input bridge for mouse testing
            var inputGo = new GameObject("SequenceInputBridge");
            inputGo.transform.SetParent(seqRoot.transform, false);
            var input = inputGo.AddComponent<CombinedSequenceInput>();
            input.SelectHelmet = selectHelmet;
            input.EquipGloves = equipGloves;
            input.SelectGlasses = selectGlasses;
            input.Helmet = helmet;
            input.Gloves = gloves;
            input.Glasses = glasses;
            input.Socket = socket.transform;

            // --- Bridge ---
            var bridgeGo = new GameObject("SequenceToFlowBridge");
            bridgeGo.transform.SetParent(root.transform, false);
            var bridge = bridgeGo.AddComponent<SequenceToFlowBridge>();
            SetRef(bridge, "sequence", sequence);
            SetRef(bridge, "triggerFlow", trigger);
            SetRef(bridge, "flowController", flowCtrl);
            var bridgeSo = new SerializedObject(bridge);
            bridgeSo.FindProperty("completionTrigger").enumValueIndex = (int)TrainingTrigger.TaskCompleted;
            bridgeSo.FindProperty("startSequenceWhenFlowWaits").boolValue = true;
            bridgeSo.FindProperty("bindFlowWaitEvent").boolValue = true;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            // Also generic activity bridge for custom scripts
            var actGo = new GameObject("ActivityToFlowBridge");
            actGo.transform.SetParent(root.transform, false);
            var act = actGo.AddComponent<ActivityToFlowBridge>();
            SetRef(act, "triggerFlow", trigger);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Combined Demo Ready",
                "Created full training with activity between nodes.\n\n" +
                "Flow:\n" +
                "  1. Welcome text\n" +
                "  2. Trigger (PPE sequence activity)\n" +
                "  3. Assessment quiz\n" +
                "  4. Closing text → completion screen\n\n" +
                "Play Mode:\n" +
                "  • Flow auto-starts\n" +
                "  • On activity node, sequence starts automatically\n" +
                "  • Click Helmet → Gloves → Glasses\n" +
                "  • Sequence completes → flow continues to quiz\n\n" +
                "Assets: Assets/TrainingDemo/",
                "OK");
        }

        // ---------- ScriptableObjects ----------

        private static AssessmentConfig CreateAssessment()
        {
            var path = $"{AssetsFolder}/CombinedAssessment.asset";
            if (AssetDatabase.LoadAssetAtPath<AssessmentConfig>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var c = ScriptableObject.CreateInstance<AssessmentConfig>();
            c.AssessmentId = "CombinedAssessment";
            c.ShowResultsScreen = true;
            c.PassThresholdPercent = 60f;
            c.NavigationMode = AssessmentNavigationMode.Sequential;
            c.Questions = new List<QuestionData>
            {
                new QuestionData
                {
                    Id = "Q1", QuestionType = QuestionType.YesNo,
                    QuestionText = "Is PPE required before the task?",
                    CorrectYesNoAnswer = true, Points = 1f
                },
                new QuestionData
                {
                    Id = "Q2", QuestionType = QuestionType.SingleChoice,
                    QuestionText = "Which must be worn?",
                    Options = new List<string> { "Sandals", "Safety glasses", "Scarf" },
                    CorrectOptionIndices = new List<int> { 1 }, Points = 1f
                },
                new QuestionData
                {
                    Id = "Q3", QuestionType = QuestionType.TrueFalse,
                    QuestionText = "You can skip PPE if in a hurry.",
                    CorrectTrueFalseAnswer = false, Points = 1f
                }
            };
            AssetDatabase.CreateAsset(c, path);
            return c;
        }

        private static TrainingFlowConfig CreateFlowWithActivity(AssessmentConfig assessment)
        {
            var path = $"{AssetsFolder}/CombinedFlow.asset";
            if (AssetDatabase.LoadAssetAtPath<TrainingFlowConfig>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var f = ScriptableObject.CreateInstance<TrainingFlowConfig>();
            f.Title = "Combined Safety Training";
            f.ShowCompletionScreen = true;
            f.CalculateScore = true;
            f.StartNodeId = "NODE_01";
            f.Nodes = new List<FlowNodeData>
            {
                new FlowNodeData
                {
                    Id = "NODE_01",
                    NodeType = FlowNodeType.Text,
                    NextNodeId = "NODE_02",
                    Navigation = NavigationMode.None,
                    AutoAdvanceDelaySeconds = 3f,
                    TextContent =
                        "Welcome.\n\nYou will complete a hands-on PPE activity, then a short quiz."
                },
                // ACTIVITY between nodes — flow waits; sequence runs via bridge
                new FlowNodeData
                {
                    Id = "NODE_02",
                    NodeType = FlowNodeType.Trigger,
                    NextNodeId = "NODE_03",
                    Trigger = TrainingTrigger.TaskCompleted,
                    TextContent =
                        "ACTIVITY: Complete the PPE sequence (Helmet → Gloves → Glasses).\n\n" +
                        "Click the highlighted objects in the scene."
                },
                new FlowNodeData
                {
                    Id = "NODE_03",
                    NodeType = FlowNodeType.Assessment,
                    NextNodeId = "NODE_04",
                    AssessmentReference = assessment
                },
                new FlowNodeData
                {
                    Id = "NODE_04",
                    NodeType = FlowNodeType.Text,
                    NextNodeId = "",
                    Navigation = NavigationMode.None,
                    AutoAdvanceDelaySeconds = 2f,
                    TextContent = "Training content finished. Opening results…"
                }
            };
            AssetDatabase.CreateAsset(f, path);
            if (!f.Validate(out var err))
                Debug.LogWarning("CombinedFlow validation: " + err);
            else
                Debug.Log("CombinedFlow validation: OK");
            return f;
        }

        // ---------- Sequence UI / objects ----------

        private static SequenceUIPanel CreateSequenceUI(Transform parent)
        {
            var canvasGo = new GameObject("SequenceCanvas");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("SequencePanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.5f, 1f);
            pr.anchorMax = new Vector2(0.5f, 1f);
            pr.pivot = new Vector2(0.5f, 1f);
            pr.anchoredPosition = new Vector2(0f, -20f);
            pr.sizeDelta = new Vector2(520f, 160f);
            panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

            var title = CreateTmp("Title", panel.transform, 22, new Vector2(0, -16), new Vector2(480, 30));
            var desc = CreateTmp("Description", panel.transform, 16, new Vector2(0, -55), new Vector2(480, 45));
            var hint = CreateTmp("Hint", panel.transform, 14, new Vector2(0, -110), new Vector2(480, 28));
            hint.color = new Color(0.7f, 0.85f, 1f);
            var task = CreateTmp("Task", panel.transform, 18, new Vector2(0, -55), new Vector2(480, 40));
            task.color = new Color(1f, 0.9f, 0.4f);

            var ui = panel.AddComponent<SequenceUIPanel>();
            var so = new SerializedObject(ui);
            so.FindProperty("root").objectReferenceValue = panel;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("descriptionText").objectReferenceValue = desc;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.FindProperty("taskText").objectReferenceValue = task;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.SetActive(false);
            return ui;
        }

        private static TextMeshProUGUI CreateTmp(string name, Transform parent, float size, Vector2 pos, Vector2 sz)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = sz;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            return t;
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.4f;
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
                if (mat != null) { mat.color = color; rend.sharedMaterial = mat; }
            }
            var hl = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hl.name = "Highlight";
            hl.transform.SetParent(go.transform, false);
            hl.transform.localScale = Vector3.one * 1.25f;
            Object.DestroyImmediate(hl.GetComponent<Collider>());
            hl.SetActive(false);
            return go;
        }

        private static void AddSeqStep(SerializedProperty steps, int i, string task, string title,
            string desc, string hint, SequenceActionBase action)
        {
            steps.InsertArrayElementAtIndex(i);
            var s = steps.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("TaskText").stringValue = task;
            s.FindPropertyRelative("Title").stringValue = title;
            s.FindPropertyRelative("Description").stringValue = desc;
            s.FindPropertyRelative("Hint").stringValue = hint;
            s.FindPropertyRelative("Action").objectReferenceValue = action;
        }

        // ---------- Shared helpers ----------

        private static void AssignPrefabs(UIPanelFactory factory, Transform panelParent)
        {
            var so = new SerializedObject(factory);
            so.FindProperty("_panelParent").objectReferenceValue = panelParent;
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
            Debug.LogWarning($"FullTrainingDemoSetup: prefab '{name}' ({typeof(T).Name}) not found.");
            return null;
        }

        private static GameObject CreateCanvas(Transform parent)
        {
            var go = new GameObject("TrainingCanvas");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
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
            go.transform.position = new Vector3(0f, 1.2f, -4f);
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
            if (p == null) { Debug.LogWarning($"Missing field {field} on {obj.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivate(Object obj, string field, Object value)
        {
            SetRef(obj, field, value);
        }
    }

}
#endif
