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
using Training.Sequence;
using Training.Integration;

namespace Training.EditorTools
{
    /// <summary>
    /// v0.0.2 — One scene that exercises every major module.
    /// Menu: Training → Setup Full Module Demo (v0.0.2)
    /// </summary>
    public static class FullModuleDemoSetup
    {
        private const string RootFolder = "Assets/TrainingDemo_v002";
        private const string PrefabFolder = "Assets/TrainingDemo_v002/Prefabs";

        [MenuItem("Training/Setup Full Module Demo (v0.0.2)", priority = 0)]
        public static void Setup()
        {
            if (!EditorUtility.DisplayDialog(
                    "Full Module Demo v0.0.2",
                    "Creates a scene that uses:\n" +
                    "• Text → Active → Animation → Sequence (PPE)\n" +
                    "• Zone → Checklist → Progress → Assessment → End\n\n" +
                    "Assets: Assets/TrainingDemo_v002/\nContinue?",
                    "Create", "Cancel"))
                return;

            EnsureFolder(RootFolder);
            EnsureFolder(PrefabFolder);

            var rowPrefab = BuildRowPrefab();
            var checklistPrefab = BuildChecklistPrefab(rowPrefab);
            var progressPrefab = BuildProgressPrefab();

            var assessment = CreateAssessment();
            var flow = CreateFullFlow(assessment);

            EnsureCamera();
            EnsureEventSystem();

            var old = GameObject.Find("TrainingSystem_FullModule");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject("TrainingSystem_FullModule");
            Undo.RegisterCreatedObjectUndo(root, "Full Module Demo");

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

            var sessionSo = new SerializedObject(session);
            var listProp = sessionSo.FindProperty("_availableFlowConfigs");
            listProp.ClearArray();
            listProp.InsertArrayElementAtIndex(0);
            listProp.GetArrayElementAtIndex(0).objectReferenceValue = flow;
            sessionSo.ApplyModifiedPropertiesWithoutUndo();

            SetRef(runner, "_sessionController", session);
            var runnerSo = new SerializedObject(runner);
            runnerSo.FindProperty("_startOnAwake").boolValue = true;
            runnerSo.ApplyModifiedPropertiesWithoutUndo();

            // World props
            var world = new GameObject("World");
            world.transform.SetParent(root.transform, false);

            var closedBay = CreateCube("ClosedBay", world.transform, new Vector3(-2f, 0.5f, 0f), new Color(0.4f, 0.2f, 0.2f));
            var openBay = new GameObject("OpenBay");
            openBay.transform.SetParent(world.transform, false);
            openBay.SetActive(false);

            var helmet = CreateCube("Helmet", openBay.transform, new Vector3(-1f, 0.5f, 1f), Color.yellow);
            var gloves = CreateCube("Gloves", openBay.transform, new Vector3(0f, 0.5f, 1f), Color.cyan);
            var glasses = CreateCube("Glasses", openBay.transform, new Vector3(1f, 0.5f, 1f), Color.magenta);

            var hatch = CreateCube("Hatch", world.transform, new Vector3(0f, 1.2f, 0f), new Color(0.7f, 0.7f, 0.3f));
            hatch.transform.localScale = new Vector3(1.5f, 0.15f, 1f);

            var zone = CreateCube("CheckZone", world.transform, new Vector3(3f, 0.05f, 0f), new Color(1f, 0.5f, 0f, 0.5f));
            zone.transform.localScale = new Vector3(2f, 0.1f, 2f);
            var zoneCol = zone.GetComponent<BoxCollider>();
            zoneCol.isTrigger = true;
            var zoneTrig = zone.AddComponent<SimpleFlowTriggerZone>();
            SetEnum(zoneTrig, "triggerToRaise", (int)TrainingTrigger.Continue);
            SetString(zoneTrig, "requiredTag", "Player");

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.SetParent(world.transform, false);
            player.transform.position = new Vector3(0f, 1f, -3f);
            if (player.GetComponent<Rigidbody>() == null)
            {
                var rb = player.AddComponent<Rigidbody>();
                rb.isKinematic = true;
            }

            // Active node controller
            var activeGo = new GameObject("ActiveNodeController");
            activeGo.transform.SetParent(root.transform, false);
            var active = activeGo.AddComponent<ActiveNodeController>();
            SetRef(active, "flowController", flowCtrl);
            AddActiveBinding(active, "N_ACTIVE",
                new[] { openBay },
                new[] { closedBay });

            // Animation spin (no AnimatorController needed)
            var spinGo = new GameObject("FullDemoSpinHandler");
            spinGo.transform.SetParent(root.transform, false);
            var spin = spinGo.AddComponent<FullDemoSpinHandler>();
            SetRef(spin, "flowController", flowCtrl);
            SetRef(spin, "spinTarget", hatch.transform);
            SetString(spin, "animationNodeId", "N_ANIM");

            // Sequence
            var seqGo = new GameObject("SequenceActivity");
            seqGo.transform.SetParent(root.transform, false);
            var highlighter = seqGo.AddComponent<SimpleHighlighter>();
            var seqUi = seqGo.AddComponent<SequenceUIPanel>();
            BuildSequenceUI(seqUi, seqGo.transform);

            var seq = seqGo.AddComponent<GuidedSequenceController>();
            seq.Mode = GuidanceMode.Guided;
            var stepHelmet = MakeSelectStep("Put on helmet", "Helmet", "Select the yellow helmet cube.", "Look for yellow", helmet, seqGo.transform);
            var stepGloves = MakeSelectStep("Put on gloves", "Gloves", "Select the cyan gloves cube.", "Look for cyan", gloves, seqGo.transform);
            var stepGlasses = MakeSelectStep("Put on glasses", "Glasses", "Select the magenta glasses cube.", "Look for magenta", glasses, seqGo.transform);
            seq.Steps = new List<GuidedStep> { stepHelmet, stepGloves, stepGlasses };
            SetRef(seq, "highlighterComponent", highlighter);
            SetRef(seq, "uiComponent", seqUi);

            var seqInput = seqGo.AddComponent<CombinedSequenceInput>();
            seqInput.SelectActions = new[]
            {
                (SelectAction)stepHelmet.Action,
                (SelectAction)stepGloves.Action,
                (SelectAction)stepGlasses.Action
            };
            seqInput.Helmet = helmet;
            seqInput.Gloves = gloves;
            seqInput.Glasses = glasses;
            seqInput.SelectHelmet = (SelectAction)stepHelmet.Action;
            seqInput.SelectGlasses = (SelectAction)stepGlasses.Action;

            var seqBridge = seqGo.AddComponent<SequenceToFlowBridge>();
            SetRef(seqBridge, "sequence", seq);
            SetRef(seqBridge, "flowController", flowCtrl);
            SetEnum(seqBridge, "completionTrigger", (int)TrainingTrigger.TaskCompleted);
            SetBool(seqBridge, "startSequenceWhenFlowWaits", true);
            SetBool(seqBridge, "bindFlowWaitEvent", true);

            // Checklist + Progress
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
            progress.Setup("Fill tank", 0f, 100f, true, false);

            var modBridge = cp.AddComponent<FullModuleDemoBridge>();
            SetRef(modBridge, "checklist", checklist);
            SetRef(modBridge, "progress", progress);

            var demoInput = cp.AddComponent<ChecklistProgressDemoInput>();
            demoInput.Checklist = checklist;
            demoInput.Progress = progress;
            demoInput.ChecklistIds = new[] { "Inspect", "Close", "Confirm" };
            demoInput.ProgressStep = 25f;

            // UI
            var canvas = CreateCanvas(root.transform);
            var factoryGo = new GameObject("UIPanelFactory");
            factoryGo.transform.SetParent(root.transform, false);
            var factory = factoryGo.AddComponent<UIPanelFactory>();
            AssignFactory(factory, canvas.transform, checklistPrefab, progressPrefab);

            var uiGo = new GameObject("UIFlowController");
            uiGo.transform.SetParent(root.transform, false);
            var uiFlow = uiGo.AddComponent<UIFlowController>();
            SetRef(uiFlow, "_panelFactory", factory);
            SetRef(uiFlow, "_flowController", flowCtrl);
            SetRef(uiFlow, "_assessmentController", assessmentCtrl);
            SetRef(uiFlow, "_questionController", question);
            SetRef(uiFlow, "_sessionController", session);

            // Live checklist/progress
            var live = new GameObject("LiveStatusUI");
            live.transform.SetParent(canvas.transform, false);
            var clInst = (ChecklistPanel)PrefabUtility.InstantiatePrefab(checklistPrefab, live.transform);
            var prInst = (ProgressPanel)PrefabUtility.InstantiatePrefab(progressPrefab, live.transform);
            if (clInst != null)
            {
                Place(clInst.GetComponent<RectTransform>(), new Vector2(24f, 100f));
                clInst.Bind(checklist, "Pre-checks");
                clInst.Show();
            }
            if (prInst != null)
            {
                Place(prInst.GetComponent<RectTransform>(), new Vector2(24f, -80f));
                prInst.Bind(progress);
                prInst.Show();
            }

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Full Module Demo Ready (v0.0.2)",
                "Play and follow the flow:\n\n" +
                "1 Text → 2 Active (bay opens) → 3 Hatch spins\n" +
                "4 PPE brief → 5 Click Helmet, Gloves, Glasses\n" +
                "6 Text → 7 Move Player capsule into orange zone\n" +
                "8 Text → 9 Keys 1,2,3 checklist\n" +
                "10 Text → 11 Keys +/= progress to 100\n" +
                "12 Quiz → 13 End\n\n" +
                "Sequence mode = Guided (title/description/hint).",
                "OK");
        }

        // ---------- flow asset ----------

        private static TrainingFlowConfig CreateFullFlow(AssessmentConfig assessment)
        {
            var path = RootFolder + "/Flow_FullModule.asset";
            DeleteIfExists(path);
            var f = ScriptableObject.CreateInstance<TrainingFlowConfig>();
            f.Title = "Full Module Demo v0.0.2";
            f.ShowCompletionScreen = true;
            f.CalculateScore = true;
            f.StartNodeId = "N01";
            f.Nodes = new List<FlowNodeData>
            {
                T("N01", "N02", "Welcome to the full-module safety bay demo."),
                A("N02", "N03"),
                Anim("N03", "N04"),
                T("N04", "N05", "Put on PPE: helmet, gloves, then glasses (Guided sequence)."),
                Trig("N05", "N06", TrainingTrigger.TaskCompleted, "Waiting for PPE sequence (click the three cubes)."),
                T("N06", "N07", "Walk to the orange check zone."),
                Trig("N07", "N08", TrainingTrigger.Continue, "Waiting for zone entry."),
                T("N08", "N09", "Complete the pre-check list (keys 1, 2, 3)."),
                Trig("N09", "N10", TrainingTrigger.InteractionCompleted, "Waiting for all checklist items."),
                T("N10", "N11", "Fill the tank to 100% (press = or +)."),
                Trig("N11", "N12", TrainingTrigger.ConditionMet, "Waiting for progress max."),
                new FlowNodeData
                {
                    Id = "N12", NodeType = FlowNodeType.Assessment, NextNodeId = "N13",
                    AssessmentReference = assessment
                },
                T("N13", "", "Full module demo complete.")
            };
            AssetDatabase.CreateAsset(f, path);
            if (!f.Validate(out var err))
                Debug.LogWarning("Flow validation: " + err);
            return f;
        }

        private static FlowNodeData T(string id, string next, string text) => new FlowNodeData
        {
            Id = id, NodeType = FlowNodeType.Text, NextNodeId = next,
            Navigation = NavigationMode.None, AutoAdvanceDelaySeconds = 2.2f,
            TextContent = text
        };

        private static FlowNodeData A(string id, string next) => new FlowNodeData
        {
            Id = id, NodeType = FlowNodeType.Active, NextNodeId = next,
            AutoAdvanceDelaySeconds = 0.3f
        };

        private static FlowNodeData Anim(string id, string next) => new FlowNodeData
        {
            Id = id, NodeType = FlowNodeType.Animation, NextNodeId = next
        };

        private static FlowNodeData Trig(string id, string next, TrainingTrigger tr, string help) => new FlowNodeData
        {
            Id = id, NodeType = FlowNodeType.Trigger, NextNodeId = next,
            Trigger = tr, TextContent = help
        };

        private static AssessmentConfig CreateAssessment()
        {
            var path = RootFolder + "/Assessment_FullModule.asset";
            DeleteIfExists(path);
            var c = ScriptableObject.CreateInstance<AssessmentConfig>();
            c.AssessmentId = "FullModuleQuiz";
            c.ShowResultsScreen = true;
            c.PassThresholdPercent = 50f;
            c.NavigationMode = AssessmentNavigationMode.Sequential;
            c.Questions = new List<QuestionData>
            {
                new QuestionData
                {
                    Id = "Q1", QuestionType = QuestionType.YesNo,
                    QuestionText = "Did you complete PPE, zone, checklist, and progress?",
                    CorrectYesNoAnswer = true, Points = 1f
                },
                new QuestionData
                {
                    Id = "Q2", QuestionType = QuestionType.SingleChoice,
                    QuestionText = "What opened the bay props?",
                    Options = new List<string> { "Active node", "Animation only", "Quiz" },
                    CorrectOptionIndices = new List<int> { 0 },
                    Points = 1f
                }
            };
            AssetDatabase.CreateAsset(c, path);
            return c;
        }

        private static GuidedStep MakeSelectStep(string task, string title, string desc, string hint, GameObject target, Transform parent)
        {
            var go = new GameObject("Action_" + title);
            go.transform.SetParent(parent, false);
            var action = go.AddComponent<SelectAction>();
            SetRef(action, "target", target);
            return new GuidedStep
            {
                TaskText = task,
                Title = title,
                Description = desc,
                Hint = hint,
                Action = action
            };
        }

        private static void BuildSequenceUI(SequenceUIPanel panel, Transform parent)
        {
            var root = new GameObject("SequenceUIRoot", typeof(RectTransform));
            // Sequence UI is often world or overlay — use simple TMP under sequence object for non-canvas
            // SequenceUIPanel uses GameObject root + TMP fields — create hierarchy without canvas
            var title = NewTmp(parent, "SeqTitle");
            var desc = NewTmp(parent, "SeqDesc");
            var hint = NewTmp(parent, "SeqHint");
            var task = NewTmp(parent, "SeqTask");
            SetRef(panel, "root", title.transform.parent != null ? parent.gameObject : parent.gameObject);
            SetRef(panel, "titleText", title);
            SetRef(panel, "descriptionText", desc);
            SetRef(panel, "hintText", hint);
            SetRef(panel, "taskText", task);
        }

        private static TextMeshProUGUI NewTmp(Transform parent, string name)
        {
            // Need canvas for TMP mesh - parent under TrainingCanvas later is better
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // Without canvas TMP may warn; for demo attach to a screen canvas instead in Setup
            return go.AddComponent<TextMeshProUGUI>();
        }

        // --- prefabs (same as checklist setup) ---

        private static ChecklistRowView BuildRowPrefab()
        {
            var path = PrefabFolder + "/ChecklistRow.prefab";
            var go = new GameObject("ChecklistRow", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 36);
            go.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.85f);

            var empty = new GameObject("Empty", typeof(RectTransform), typeof(Image));
            empty.transform.SetParent(go.transform, false);
            empty.GetComponent<RectTransform>().anchoredPosition = new Vector2(18, 0);
            empty.GetComponent<RectTransform>().sizeDelta = new Vector2(18, 18);
            empty.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.55f);

            var check = new GameObject("Check", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(go.transform, false);
            check.GetComponent<RectTransform>().anchoredPosition = new Vector2(18, 0);
            check.GetComponent<RectTransform>().sizeDelta = new Vector2(18, 18);
            check.GetComponent<Image>().color = new Color(0.2f, 0.85f, 0.4f);
            check.SetActive(false);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(44, 2); lrt.offsetMax = new Vector2(-8, -2);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 16; tmp.color = Color.white; tmp.text = "Item";

            var row = go.AddComponent<ChecklistRowView>();
            row.Wire(tmp, check, empty);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ChecklistRowView>();
        }

        private static ChecklistPanel BuildChecklistPrefab(ChecklistRowView rowPrefab)
        {
            var path = PrefabFolder + "/ChecklistPanel.prefab";
            var go = new GameObject("ChecklistPanel", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(380, 200);
            go.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(go.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.fontSize = 18; title.color = Color.white; title.alignment = TextAlignmentOptions.Center;
            title.text = "Checklist";
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1); trt.anchoredPosition = new Vector2(0, -8); trt.sizeDelta = new Vector2(-16, 28);

            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(go.transform, false);
            var lrt = list.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12, 12); lrt.offsetMax = new Vector2(-12, -40);
            var vlg = list.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.childControlWidth = true; vlg.childForceExpandWidth = true;

            var panel = go.AddComponent<ChecklistPanel>();
            panel.Wire(title, list.transform, rowPrefab);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ChecklistPanel>();
        }

        private static ProgressPanel BuildProgressPrefab()
        {
            var path = PrefabFolder + "/ProgressPanel.prefab";
            var go = new GameObject("ProgressPanel", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(380, 90);
            go.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(go.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.fontSize = 16; title.color = Color.white; title.alignment = TextAlignmentOptions.Center;
            title.text = "Progress";

            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(go.transform, false);
            var value = valueGo.AddComponent<TextMeshProUGUI>();
            value.fontSize = 14; value.color = Color.white; value.alignment = TextAlignmentOptions.Center;
            value.text = "0 / 100";

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(go.transform, false);
            track.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
            var trackRt = track.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0, 0); trackRt.anchorMax = new Vector2(1, 0);
            trackRt.pivot = new Vector2(0.5f, 0); trackRt.anchoredPosition = new Vector2(0, 14);
            trackRt.sizeDelta = new Vector2(-24, 16);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            var fillImg = fill.GetComponent<Image>();
            fillImg.color = new Color(0.25f, 0.7f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;

            var panel = go.AddComponent<ProgressPanel>();
            panel.Wire(title, value, fillImg);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ProgressPanel>();
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default"));
                mat.color = color;
                r.sharedMaterial = mat;
            }
            return go;
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

        private static void Place(RectTransform rt, Vector2 anchored)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = anchored;
        }

        private static void AssignFactory(UIPanelFactory factory, Transform parent,
            ChecklistPanel checklistPrefab, ProgressPanel progressPrefab)
        {
            var so = new SerializedObject(factory);
            so.FindProperty("_panelParent").objectReferenceValue = parent;
            so.FindProperty("_textPanelPrefab").objectReferenceValue = FindPrefab<TextPanel>("TextPanel");
            so.FindProperty("_textImagePanelPrefab").objectReferenceValue = FindPrefab<TextImagePanel>("TextImagePanel");
            so.FindProperty("_assessmentPanelPrefab").objectReferenceValue = FindPrefab<AssessmentPanel>("AssessmentPanel");
            so.FindProperty("_assessmentResultPanelPrefab").objectReferenceValue =
                FindPrefab<AssessmentResultPanel>("AssementResultPanel") ?? FindPrefab<AssessmentResultPanel>("AssessmentResultPanel");
            so.FindProperty("_trainingCompletePanelPrefab").objectReferenceValue = FindPrefab<TrainingCompletePanel>("TrainingCompletePanel");
            so.FindProperty("_trainingMenuPanelPrefab").objectReferenceValue = FindPrefab<TrainingMenuPanel>("TrainingMenuPanel");
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
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var c = go.GetComponent<T>() ?? go.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        private static void AddActiveBinding(ActiveNodeController active, string nodeId,
            GameObject[] enable, GameObject[] disable)
        {
            var so = new SerializedObject(active);
            var bindings = so.FindProperty("bindings");
            if (bindings == null) return;
            bindings.InsertArrayElementAtIndex(bindings.arraySize);
            var el = bindings.GetArrayElementAtIndex(bindings.arraySize - 1);
            el.FindPropertyRelative("NodeId").stringValue = nodeId;
            var en = el.FindPropertyRelative("ObjectsToEnable");
            en.arraySize = enable.Length;
            for (int i = 0; i < enable.Length; i++)
                en.GetArrayElementAtIndex(i).objectReferenceValue = enable[i];
            var dis = el.FindPropertyRelative("ObjectsToDisable");
            dis.arraySize = disable.Length;
            for (int i = 0; i < disable.Length; i++)
                dis.GetArrayElementAtIndex(i).objectReferenceValue = disable[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0, 1.6f, -6f);
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

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        private static void SetRef(Object obj, string field, Object value)
        {
            var so = new SerializedObject(obj);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"Missing {field} on {obj.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void TrySetRef(Object obj, string field, Object value)
        {
            var so = new SerializedObject(obj);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object obj, string field, string value)
        {
            var so = new SerializedObject(obj);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object obj, string field, bool value)
        {
            var so = new SerializedObject(obj);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object obj, string field, int value)
        {
            var so = new SerializedObject(obj);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
