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
    /// One scene, multiple flow configs, all systems connected.
    /// Menu: Training → Setup Multi-Flow Connected Scene
    ///
    /// Creates:
    ///   Flow A — PPE sequence activity (TaskCompleted)
    ///   Flow B — walk into zone activity (Continue)
    ///   Shared assessment, full UI, menu on start, completion Retry/Choose Another
    /// </summary>
    public static class MultiFlowConnectedSetup
    {
        private const string AssetsFolder = "Assets/TrainingDemo";
        private const string RootName = "TrainingSystem";

        [MenuItem("Training/Setup Multi-Flow Connected Scene", priority = 0)]
        public static void Setup()
        {
            if (!EditorUtility.DisplayDialog(
                    "Multi-Flow Connected Scene",
                    "Creates ONE scene with:\n\n" +
                    "• 2 TrainingFlowConfigs (PPE path + Zone path)\n" +
                    "• Shared assessment\n" +
                    "• Sequence activity (TaskCompleted) + bridge\n" +
                    "• Trigger zone (Continue)\n" +
                    "• Training menu on start (pick a flow)\n" +
                    "• Completion: Retry / Choose Another\n" +
                    "• All controllers + UI wired\n\n" +
                    "Assets → Assets/TrainingDemo/\n\nContinue?",
                    "Create", "Cancel"))
                return;

            EnsureFolder(AssetsFolder);

            var assessment = CreateAssessment();
            var flowA = CreateFlowA_PPE(assessment);
            var flowB = CreateFlowB_Zone(assessment);

            EnsureCamera();
            EnsureEventSystem();
            EnsurePlayerTagObject();

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "TrainingSystem");

            // Audio
            var audioGo = new GameObject("AudioSource");
            audioGo.transform.SetParent(root.transform, false);
            var audio = audioGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            // Controllers
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

            // Multiple flow configs
            var sessionSo = new SerializedObject(session);
            var list = sessionSo.FindProperty("_availableFlowConfigs");
            list.ClearArray();
            list.InsertArrayElementAtIndex(0);
            list.GetArrayElementAtIndex(0).objectReferenceValue = flowA;
            list.InsertArrayElementAtIndex(1);
            list.GetArrayElementAtIndex(1).objectReferenceValue = flowB;
            sessionSo.ApplyModifiedPropertiesWithoutUndo();

            // Do NOT auto-start — menu picks first
            SetRef(runner, "_sessionController", session);
            var runnerSo = new SerializedObject(runner);
            runnerSo.FindProperty("_startOnAwake").boolValue = false;
            runnerSo.ApplyModifiedPropertiesWithoutUndo();

            // UI
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

            // Menu bootstrap (multi-flow)
            var bootGo = new GameObject("MultiFlowSceneBootstrap");
            bootGo.transform.SetParent(root.transform, false);
            var boot = bootGo.AddComponent<MultiFlowSceneBootstrap>();
            SetRef(boot, "session", session);
            SetRef(boot, "panelFactory", factory);
            SetRef(boot, "flowRunner", runner);
            var bootSo = new SerializedObject(boot);
            bootSo.FindProperty("showMenuOnStart").boolValue = true;
            bootSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Sequence activity (Flow A uses TaskCompleted) ---
            var seqRoot = BuildSequenceActivity(root.transform, out var sequence);

            var bridgeGo = new GameObject("SequenceToFlowBridge");
            bridgeGo.transform.SetParent(root.transform, false);
            var bridge = bridgeGo.AddComponent<SequenceToFlowBridge>();
            SetRef(bridge, "sequence", sequence);
            SetRef(bridge, "flowController", flowCtrl);
            var bridgeSo = new SerializedObject(bridge);
            bridgeSo.FindProperty("completionTrigger").enumValueIndex = (int)TrainingTrigger.TaskCompleted;
            bridgeSo.FindProperty("startSequenceWhenFlowWaits").boolValue = true;
            bridgeSo.FindProperty("bindFlowWaitEvent").boolValue = true;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            // Generic activity helper
            var actGo = new GameObject("ActivityToFlowBridge");
            actGo.transform.SetParent(root.transform, false);
            var act = actGo.AddComponent<ActivityToFlowBridge>();
            var actSo = new SerializedObject(act);
            actSo.FindProperty("completionTrigger").enumValueIndex = (int)TrainingTrigger.TaskCompleted;
            actSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Zone activity (Flow B uses Continue) ---
            var zoneGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zoneGo.name = "ContinueTriggerZone";
            Undo.RegisterCreatedObjectUndo(zoneGo, "Zone");
            zoneGo.transform.SetParent(root.transform, false);
            zoneGo.transform.position = new Vector3(0f, 0.5f, 2f);
            zoneGo.transform.localScale = new Vector3(2f, 1f, 2f);
            var zoneCol = zoneGo.GetComponent<Collider>();
            zoneCol.isTrigger = true;
            var zoneRend = zoneGo.GetComponent<Renderer>();
            if (zoneRend != null)
            {
                var m = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
                if (m != null) { m.color = new Color(1f, 0.4f, 0.2f, 0.4f); zoneRend.sharedMaterial = m; }
            }
            var zone = zoneGo.AddComponent<SimpleFlowTriggerZone>();
            var zoneSo = new SerializedObject(zone);
            zoneSo.FindProperty("triggerToRaise").enumValueIndex = (int)TrainingTrigger.Continue;
            zoneSo.FindProperty("requiredTag").stringValue = "Player";
            zoneSo.FindProperty("fireOnlyOnce").boolValue = false; // allow retry sessions
            zoneSo.ApplyModifiedPropertiesWithoutUndo();

            // Label
            var zoneLabel = new GameObject("Label");
            zoneLabel.transform.SetParent(zoneGo.transform, false);
            zoneLabel.transform.localPosition = Vector3.up * 1.2f;
            var tmp = zoneLabel.AddComponent<TextMeshPro>();
            tmp.text = "ZONE\nContinue";
            tmp.fontSize = 3;
            tmp.alignment = TextAlignmentOptions.Center;

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Multi-Flow Scene Ready",
                "Same scene — two trainings:\n\n" +
                "1) PPE Training (Flow A)\n" +
                "   Text → Sequence (TaskCompleted) → Quiz → End\n" +
                "   Click Helmet → Gloves → Glasses\n\n" +
                "2) Zone Training (Flow B)\n" +
                "   Text → Trigger (Continue) → Text → Quiz → End\n" +
                "   Walk Player into orange ZONE\n\n" +
                "Play Mode:\n" +
                "• Menu appears → pick a training\n" +
                "• On complete → Retry or Choose Another\n\n" +
                "Remember: trigger = TaskCompleted / Continue\n" +
                "NOT the flow title.",
                "OK");
        }

        // ---------- Flow A: PPE sequence ----------

        private static TrainingFlowConfig CreateFlowA_PPE(AssessmentConfig assessment)
        {
            var path = $"{AssetsFolder}/FlowA_PPE.asset";
            DeleteIfExists(path);

            var f = ScriptableObject.CreateInstance<TrainingFlowConfig>();
            f.Title = "PPE Hands-On Training";
            f.ShowCompletionScreen = true;
            f.CalculateScore = true;
            f.StartNodeId = "A01";
            f.Nodes = new List<FlowNodeData>
            {
                new FlowNodeData
                {
                    Id = "A01", NodeType = FlowNodeType.Text, NextNodeId = "A02",
                    Navigation = NavigationMode.None, AutoAdvanceDelaySeconds = 2.5f,
                    TextContent = "PPE Training\n\nYou will put on helmet, gloves, then glasses."
                },
                new FlowNodeData
                {
                    Id = "A02", NodeType = FlowNodeType.Trigger, NextNodeId = "A03",
                    Trigger = TrainingTrigger.TaskCompleted,
                    TextContent = "ACTIVITY: Complete the PPE sequence.\nClick the highlighted objects."
                },
                new FlowNodeData
                {
                    Id = "A03", NodeType = FlowNodeType.Assessment, NextNodeId = "A04",
                    AssessmentReference = assessment
                },
                new FlowNodeData
                {
                    Id = "A04", NodeType = FlowNodeType.Text, NextNodeId = "",
                    Navigation = NavigationMode.None, AutoAdvanceDelaySeconds = 2f,
                    TextContent = "PPE path finished."
                }
            };
            AssetDatabase.CreateAsset(f, path);
            ValidateFlow(f);
            return f;
        }

        // ---------- Flow B: Zone ----------

        private static TrainingFlowConfig CreateFlowB_Zone(AssessmentConfig assessment)
        {
            var path = $"{AssetsFolder}/FlowB_Zone.asset";
            DeleteIfExists(path);

            var f = ScriptableObject.CreateInstance<TrainingFlowConfig>();
            f.Title = "Zone Walk-Through Training";
            f.ShowCompletionScreen = true;
            f.CalculateScore = true;
            f.StartNodeId = "B01";
            f.Nodes = new List<FlowNodeData>
            {
                new FlowNodeData
                {
                    Id = "B01", NodeType = FlowNodeType.Text, NextNodeId = "B02",
                    Navigation = NavigationMode.None, AutoAdvanceDelaySeconds = 2.5f,
                    TextContent = "Zone Training\n\nWalk into the orange ZONE to continue."
                },
                new FlowNodeData
                {
                    Id = "B02", NodeType = FlowNodeType.Trigger, NextNodeId = "B03",
                    Trigger = TrainingTrigger.Continue,
                    TextContent = "WAITING: Enter the Continue zone (orange cube)."
                },
                new FlowNodeData
                {
                    Id = "B03", NodeType = FlowNodeType.Text, NextNodeId = "B04",
                    Navigation = NavigationMode.Next,
                    TextContent = "Zone cleared. Press Next for the quiz."
                },
                new FlowNodeData
                {
                    Id = "B04", NodeType = FlowNodeType.Assessment, NextNodeId = "B05",
                    AssessmentReference = assessment
                },
                new FlowNodeData
                {
                    Id = "B05", NodeType = FlowNodeType.Text, NextNodeId = "",
                    Navigation = NavigationMode.None, AutoAdvanceDelaySeconds = 2f,
                    TextContent = "Zone path finished."
                }
            };
            AssetDatabase.CreateAsset(f, path);
            ValidateFlow(f);
            return f;
        }

        private static AssessmentConfig CreateAssessment()
        {
            var path = $"{AssetsFolder}/SharedAssessment.asset";
            DeleteIfExists(path);

            var c = ScriptableObject.CreateInstance<AssessmentConfig>();
            c.AssessmentId = "SharedAssessment";
            c.ShowResultsScreen = true;
            c.PassThresholdPercent = 50f;
            c.NavigationMode = AssessmentNavigationMode.Sequential;
            c.Questions = new List<QuestionData>
            {
                new QuestionData
                {
                    Id = "Q1", QuestionType = QuestionType.YesNo,
                    QuestionText = "Is PPE required in the work area?",
                    CorrectYesNoAnswer = true, Points = 1f
                },
                new QuestionData
                {
                    Id = "Q2", QuestionType = QuestionType.SingleChoice,
                    QuestionText = "Which item is required?",
                    Options = new List<string> { "Sandals", "Safety glasses", "Scarf" },
                    CorrectOptionIndices = new List<int> { 1 }, Points = 1f
                }
            };
            AssetDatabase.CreateAsset(c, path);
            return c;
        }

        // ---------- Sequence hierarchy ----------

        private static GameObject BuildSequenceActivity(Transform parent, out GuidedSequenceController sequence)
        {
            var seqRoot = new GameObject("SequenceActivity");
            seqRoot.transform.SetParent(parent, false);

            var seqUi = CreateSequenceUI(seqRoot.transform);
            var highlighter = seqRoot.AddComponent<SimpleHighlighter>();
            sequence = seqRoot.AddComponent<GuidedSequenceController>();

            var seqSo = new SerializedObject(sequence);
            seqSo.FindProperty("highlighterComponent").objectReferenceValue = highlighter;
            seqSo.FindProperty("uiComponent").objectReferenceValue = seqUi;
            seqSo.FindProperty("Mode").enumValueIndex = (int)GuidanceMode.Guided;
            seqSo.ApplyModifiedPropertiesWithoutUndo();

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
            SetRef(selectHelmet, "target", helmet);
            var equipGloves = actions.AddComponent<EquipAction>();
            SetRef(equipGloves, "item", gloves);
            SetRef(equipGloves, "socket", socket.transform);
            var selectGlasses = actions.AddComponent<SelectAction>();
            SetRef(selectGlasses, "target", glasses);

            seqSo = new SerializedObject(sequence);
            var steps = seqSo.FindProperty("Steps");
            steps.ClearArray();
            AddSeqStep(steps, 0, "Select helmet", "Helmet", "Select the helmet.", "Blue cube", selectHelmet);
            AddSeqStep(steps, 1, "Equip gloves", "Gloves", "Equip the gloves.", "Green cube", equipGloves);
            AddSeqStep(steps, 2, "Select glasses", "Glasses", "Select the glasses.", "Orange cube", selectGlasses);
            seqSo.ApplyModifiedPropertiesWithoutUndo();

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

            return seqRoot;
        }

        // ---------- Helpers ----------

        private static void ValidateFlow(TrainingFlowConfig f)
        {
            if (!f.Validate(out var err))
                Debug.LogWarning($"{f.Title} validation: {err}");
            else
                Debug.Log($"{f.Title} validation: OK");
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        private static SequenceUIPanel CreateSequenceUI(Transform parent)
        {
            var canvasGo = new GameObject("SequenceCanvas");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("SequencePanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 1f);
            pr.anchoredPosition = new Vector2(0, -20);
            pr.sizeDelta = new Vector2(520, 150);
            panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

            var title = CreateTmp("Title", panel.transform, 22, new Vector2(0, -14), new Vector2(480, 28));
            var desc = CreateTmp("Description", panel.transform, 15, new Vector2(0, -50), new Vector2(480, 40));
            var hint = CreateTmp("Hint", panel.transform, 13, new Vector2(0, -100), new Vector2(480, 24));
            hint.color = new Color(0.7f, 0.85f, 1f);
            var task = CreateTmp("Task", panel.transform, 17, new Vector2(0, -50), new Vector2(480, 36));
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
            Debug.LogWarning($"MultiFlowConnectedSetup: prefab '{name}' not found.");
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

        /// <summary>Simple Player capsule so zone OnTriggerEnter works in editor without XR.</summary>
        private static void EnsurePlayerTagObject()
        {
            if (GameObject.FindGameObjectWithTag("Player") != null) return;

            // Tag may not exist in project — try/catch
            try
            {
                var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player";
                player.tag = "Player";
                player.transform.position = new Vector3(0f, 1f, -1f);
                // CharacterController-like move with arrows optional — for zone test, drag in Scene
                var rb = player.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                Undo.RegisterCreatedObjectUndo(player, "Player");
            }
            catch (UnityException)
            {
                Debug.LogWarning(
                    "MultiFlowConnectedSetup: Tag 'Player' is not defined. " +
                    "Add it in Tags & Layers, or clear Required Tag on ContinueTriggerZone.");
            }
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
