#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
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
using Training.Trigger.Components;
using Training.UI.Components;
using Training.UI.Controllers;
using Training.UI.Panels;

namespace Training.EditorTools
{
    /// <summary>
    /// One-click full demo: ScriptableObjects + scene hierarchy + wiring.
    /// Menu: Training → Setup Full Demo Scene
    /// </summary>
    public static class DemoTrainingSetup
    {
        private const string AssetsFolder = "Assets/TrainingDemo";
        private const string RootName = "TrainingSystem";

        [MenuItem("Training/Setup Full Demo Scene", priority = 0)]
        public static void Setup()
        {
            if (!EditorUtility.DisplayDialog(
                    "Setup Full Demo Scene",
                    "This will create:\n\n" +
                    "• ScriptableObjects (Assessment + Flow configs)\n" +
                    "• Full TrainingSystem hierarchy (all controllers)\n" +
                    "• Canvas + UIPanelFactory wired to package prefabs\n" +
                    "• Sample trigger button (Continue)\n" +
                    "• Camera + EventSystem if missing\n\n" +
                    "Assets are written under Assets/TrainingDemo/\n\nContinue?",
                    "Create Demo", "Cancel"))
                return;

            EnsureFolder(AssetsFolder);

            var assessment = CreateAssessmentConfig();
            var flow = CreateFlowConfig(assessment);

            EnsureCamera();
            EnsureEventSystem();

            // Remove previous root
            var existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create TrainingSystem");

            // Audio
            var audioGo = new GameObject("AudioSource");
            audioGo.transform.SetParent(root.transform, false);
            var audioSource = audioGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // Controllers container
            var controllers = new GameObject("Controllers");
            controllers.transform.SetParent(root.transform, false);

            var trigger = controllers.AddComponent<TriggerFlowController>();
            var question = controllers.AddComponent<QuestionController>();
            var assessmentCtrl = controllers.AddComponent<AssessmentController>();
            var score = controllers.AddComponent<TrainingScoreController>();
            var flowCtrl = controllers.AddComponent<TrainingFlowController>();
            var session = controllers.AddComponent<TrainingSessionController>();
            var runner = controllers.AddComponent<FlowRunner>();

            // Wire AssessmentController
            SetRef(assessmentCtrl, "_questionController", question);
            SetRef(assessmentCtrl, "_audioSource", audioSource);

            // Wire FlowController
            SetRef(flowCtrl, "_triggerFlowController", trigger);
            SetRef(flowCtrl, "_assessmentController", assessmentCtrl);
            SetRef(flowCtrl, "_scoreController", score);
            SetRef(flowCtrl, "_audioSource", audioSource);

            // Wire Session
            SetRef(session, "_flowController", flowCtrl);
            SetRef(session, "_scoreController", score);
            var sessionSo = new SerializedObject(session);
            var listProp = sessionSo.FindProperty("_availableFlowConfigs");
            listProp.ClearArray();
            listProp.InsertArrayElementAtIndex(0);
            listProp.GetArrayElementAtIndex(0).objectReferenceValue = flow;
            sessionSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire FlowRunner
            SetRef(runner, "_sessionController", session);
            var runnerSo = new SerializedObject(runner);
            runnerSo.FindProperty("_startOnAwake").boolValue = true;
            runnerSo.ApplyModifiedPropertiesWithoutUndo();

            // Canvas + panels parent
            var canvasGo = CreateCanvas(root.transform);
            var panelParent = canvasGo.transform;

            // UIPanelFactory
            var factoryGo = new GameObject("UIPanelFactory");
            factoryGo.transform.SetParent(root.transform, false);
            var factory = factoryGo.AddComponent<UIPanelFactory>();
            AssignPrefabs(factory, panelParent);

            // UIFlowController
            var uiGo = new GameObject("UIFlowController");
            uiGo.transform.SetParent(root.transform, false);
            var uiFlow = uiGo.AddComponent<UIFlowController>();
            SetRef(uiFlow, "_panelFactory", factory);
            SetRef(uiFlow, "_flowController", flowCtrl);
            SetRef(uiFlow, "_assessmentController", assessmentCtrl);
            SetRef(uiFlow, "_questionController", question);
            SetRef(uiFlow, "_sessionController", session);

            // Demo Continue button → raises Continue trigger
            CreateContinueButton(canvasGo.transform, trigger);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Demo Ready",
                "Full training demo created.\n\n" +
                "Assets:\n" +
                $"  {AssetsFolder}/DemoAssessment.asset\n" +
                $"  {AssetsFolder}/DemoFlow.asset\n\n" +
                "How to test:\n" +
                "1. Enter Play Mode (FlowRunner auto-starts)\n" +
                "2. Read the intro text — auto-advances or use flow\n" +
                "3. On the Trigger step, click Continue (or press T in Editor)\n" +
                "4. Answer the quiz questions\n" +
                "5. View results / completion\n\n" +
                "Hierarchy: TrainingSystem (all controllers wired).",
                "OK");
        }

        // -----------------------------------------------------------------
        // ScriptableObjects
        // -----------------------------------------------------------------

        private static AssessmentConfig CreateAssessmentConfig()
        {
            var path = $"{AssetsFolder}/DemoAssessment.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AssessmentConfig>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            var config = ScriptableObject.CreateInstance<AssessmentConfig>();
            config.AssessmentId = "DemoAssessment";
            config.ShowResultsScreen = true;
            config.NavigationMode = AssessmentNavigationMode.Sequential;
            config.PassThresholdPercent = 60f;
            config.Questions = new List<QuestionData>
            {
                new QuestionData
                {
                    Id = "Q1",
                    QuestionType = QuestionType.YesNo,
                    QuestionText = "Is PPE required before entering the work area?",
                    CorrectYesNoAnswer = true,
                    Points = 1f
                },
                new QuestionData
                {
                    Id = "Q2",
                    QuestionType = QuestionType.TrueFalse,
                    QuestionText = "True or False: You may skip the safety briefing if you have done this task before.",
                    CorrectTrueFalseAnswer = false,
                    Points = 1f
                },
                new QuestionData
                {
                    Id = "Q3",
                    QuestionType = QuestionType.SingleChoice,
                    QuestionText = "Which item must be worn at all times in the lab?",
                    Options = new List<string> { "Headphones", "Safety glasses", "Sandals", "Scarf" },
                    CorrectOptionIndices = new List<int> { 1 },
                    Points = 1f
                },
                new QuestionData
                {
                    Id = "Q4",
                    QuestionType = QuestionType.MultipleChoice,
                    QuestionText = "Select ALL required steps before starting the machine.",
                    Options = new List<string>
                    {
                        "Wear gloves",
                        "Check emergency stop",
                        "Ignore warning lights",
                        "Clear the work area"
                    },
                    CorrectOptionIndices = new List<int> { 0, 1, 3 },
                    Points = 2f
                },
                new QuestionData
                {
                    Id = "Q5",
                    QuestionType = QuestionType.QuestionAnswer,
                    QuestionText = "What is the emergency stop keyword? (type: STOP)",
                    CorrectAnswerText = "STOP",
                    Points = 1f
                }
            };

            AssetDatabase.CreateAsset(config, path);
            EditorUtility.SetDirty(config);
            return config;
        }

        private static TrainingFlowConfig CreateFlowConfig(AssessmentConfig assessment)
        {
            var path = $"{AssetsFolder}/DemoFlow.asset";
            if (AssetDatabase.LoadAssetAtPath<TrainingFlowConfig>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var flow = ScriptableObject.CreateInstance<TrainingFlowConfig>();
            flow.Title = "Demo Safety Training";
            flow.ShowCompletionScreen = true;
            flow.CalculateScore = true;
            flow.ShowFinalQuizPanel = false;
            flow.StartNodeId = "NODE_01";
            flow.Nodes = new List<FlowNodeData>
            {
                new FlowNodeData
                {
                    Id = "NODE_01",
                    NodeType = FlowNodeType.Text,
                    NextNodeId = "NODE_02",
                    Navigation = NavigationMode.None,
                    AutoAdvanceDelaySeconds = 3f,
                    TextContent = "Welcome to the Demo Safety Training.\n\nYou will review a short briefing, confirm a step, then take a short quiz."
                },
                new FlowNodeData
                {
                    Id = "NODE_02",
                    NodeType = FlowNodeType.Text,
                    NextNodeId = "NODE_03",
                    Navigation = NavigationMode.Next,
                    TextContent = "Before entering the work area you must wear the required PPE and complete the safety checks.\n\nPress Next when you are ready to continue."
                },
                new FlowNodeData
                {
                    Id = "NODE_03",
                    NodeType = FlowNodeType.Trigger,
                    NextNodeId = "NODE_04",
                    Trigger = TrainingTrigger.Continue,
                    TextContent = "Waiting for Continue…\n\nClick the Continue button on screen, or press T in the Editor."
                },
                new FlowNodeData
                {
                    Id = "NODE_04",
                    NodeType = FlowNodeType.Assessment,
                    NextNodeId = "NODE_05",
                    AssessmentReference = assessment
                },
                new FlowNodeData
                {
                    Id = "NODE_05",
                    NodeType = FlowNodeType.Text,
                    NextNodeId = "",
                    Navigation = NavigationMode.None,
                    AutoAdvanceDelaySeconds = 2f,
                    TextContent = "You have finished the demo training content.\n\nThe completion screen will appear next with your score."
                }
            };

            AssetDatabase.CreateAsset(flow, path);
            EditorUtility.SetDirty(flow);

            // Validate
            if (!flow.Validate(out var err))
                Debug.LogWarning($"DemoFlow validation: {err}");
            else
                Debug.Log("DemoFlow validation: OK");

            return flow;
        }

        // -----------------------------------------------------------------
        // Scene helpers
        // -----------------------------------------------------------------

        private static void AssignPrefabs(UIPanelFactory factory, Transform panelParent)
        {
            var so = new SerializedObject(factory);
            so.FindProperty("_panelParent").objectReferenceValue = panelParent;

            so.FindProperty("_textPanelPrefab").objectReferenceValue =
                FindPrefab<TextPanel>("TextPanel");
            so.FindProperty("_textImagePanelPrefab").objectReferenceValue =
                FindPrefab<TextImagePanel>("TextImagePanel");
            so.FindProperty("_assessmentPanelPrefab").objectReferenceValue =
                FindPrefab<AssessmentPanel>("AssessmentPanel");
            so.FindProperty("_assessmentResultPanelPrefab").objectReferenceValue =
                FindPrefab<AssessmentResultPanel>("AssementResultPanel")
                ?? FindPrefab<AssessmentResultPanel>("AssessmentResultPanel");
            so.FindProperty("_trainingCompletePanelPrefab").objectReferenceValue =
                FindPrefab<TrainingCompletePanel>("TrainingCompletePanel");
            so.FindProperty("_trainingMenuPanelPrefab").objectReferenceValue =
                FindPrefab<TrainingMenuPanel>("TrainingMenuPanel");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T FindPrefab<T>(string name) where T : Component
        {
            // Search project + packages
            var guids = AssetDatabase.FindAssets($"{name} t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains(name)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var c = go.GetComponent<T>();
                if (c != null) return c;
                c = go.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            Debug.LogWarning($"DemoTrainingSetup: could not find prefab '{name}' with component {typeof(T).Name}. Assign it manually on UIPanelFactory.");
            return null;
        }

        private static GameObject CreateCanvas(Transform parent)
        {
            var canvasGo = new GameObject("TrainingCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            canvasGo.transform.SetParent(parent, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            return canvasGo;
        }

        private static void CreateContinueButton(Transform canvas, TriggerFlowController trigger)
        {
            var btnGo = new GameObject("ContinueButton", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(btnGo, "Create Continue");
            btnGo.transform.SetParent(canvas, false);

            var rect = btnGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.sizeDelta = new Vector2(200f, 48f);

            var image = btnGo.AddComponent<Image>();
            image.color = new Color(0.15f, 0.45f, 0.85f, 1f);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = image;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Continue";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 22;
            tmp.color = Color.white;

            // TrainingTriggerSource fires Continue
            var source = btnGo.AddComponent<TrainingTriggerSource>();
            SetRef(source, "_triggerFlowController", trigger);
            var srcSo = new SerializedObject(source);
            srcSo.FindProperty("_trigger").enumValueIndex = (int)TrainingTrigger.Continue;
            srcSo.ApplyModifiedPropertiesWithoutUndo();

            // Button onClick → RaiseTrigger via source public API if available
            // TrainingTriggerSource typically has Raise() or similar — wire via UnityEvent in code
            btn.onClick.AddListener(() =>
            {
                if (trigger != null)
                    trigger.RaiseTrigger(TrainingTrigger.Continue);
            });
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var camGo = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 1f, -10f);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetRef(Object obj, string field, Object value)
        {
            var so = new SerializedObject(obj);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"DemoTrainingSetup: field '{field}' not found on {obj.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
