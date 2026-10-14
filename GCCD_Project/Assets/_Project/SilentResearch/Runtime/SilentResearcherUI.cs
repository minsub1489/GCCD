using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GCCD.SilentResearch
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class SilentResearcherUI : MonoBehaviour
    {
        public SilentExperimentSession Session;
        public SilentScenario[] Scenarios;
        public bool ShowPracticeSimulator;
        public UnityEngine.TextCore.Text.FontAsset UIFont;
        VisualElement card;
        Label message, order, results;
        Button openResults;
        SilentPilotController pilot;
        VisualElement pilotCard, motorChoices, typeChoices, directionChoices;
        Label pilotMessage, pilotProgress, selection;
        void OnEnable()
        {
            if (!Application.isPlaying || !Session) return;
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;
            if (UIFont) root.style.unityFontDefinition = FontDefinition.FromSDFFont(UIFont);
            card = root.Q("researcherRoot");
            if (card == null) return;
            message = root.Q<Label>("message"); order = root.Q<Label>("order"); results = root.Q<Label>("results");
            var id = root.Q<TextField>("participant"); id.SetValueWithoutNotify(Session.ParticipantID);
            id.RegisterValueChangedCallback(e => Session.ParticipantID = e.newValue);
            BindInteger(root, "participantIndex", Session.ParticipantIndex, n => Session.ParticipantIndex = n);
            BindInteger(root, "block", Session.BlockIndex, n => Session.BlockIndex = n);
            BindInteger(root, "seed", Session.RandomSeed, n => Session.RandomSeed = n);
            var condition = root.Q<DropdownField>("condition"); condition.choices = Enum.GetNames(typeof(FeedbackCondition)).ToList();
            condition.SetValueWithoutNotify(Session.Condition.ToString());
            condition.RegisterValueChangedCallback(e => Session.Condition = (FeedbackCondition)Enum.Parse(typeof(FeedbackCondition), e.newValue));
            var scenario = root.Q<DropdownField>("scenario");
            scenario.choices = Scenarios.Where(s => s).Select(s => s.ScenarioID).ToList();
            scenario.SetValueWithoutNotify(Session.Scenario.ScenarioID);
            scenario.RegisterValueChangedCallback(e => Session.Scenario = Scenarios.First(s => s && s.ScenarioID == e.newValue));
            var hardware = root.Q<Toggle>("hardware"); hardware.SetValueWithoutNotify(Session.Stimulus.Mode == OutputMode.BHaptics);
            hardware.RegisterValueChangedCallback(e => Session.Stimulus.Mode = e.newValue ? OutputMode.BHaptics : OutputMode.Simulation);
            pilot = GetComponent<SilentPilotController>();
            BindPilots(root);
            var preview = root.Q<Toggle>("preview"); preview.SetValueWithoutNotify(ShowPracticeSimulator);
            preview.RegisterValueChangedCallback(e => ShowPracticeSimulator = e.newValue);
            root.Q<Button>("practice").clicked += () => StartBlock(true);
            root.Q<Button>("measure").clicked += () => StartBlock(false);
            root.Q<Button>("difficulty").clicked += () => Session.StartDifficultyPilot();
            openResults = root.Q<Button>("openResults");
            openResults.clicked += () => { if (!string.IsNullOrEmpty(Session.ResultDirectory)) Application.OpenURL(new Uri(Session.ResultDirectory + "/").AbsoluteUri); };
        }
        static void BindInteger(VisualElement root, string name, int value, Action<int> apply)
        { var field = root.Q<IntegerField>(name); field.SetValueWithoutNotify(value); field.RegisterValueChangedCallback(e => apply(e.newValue)); }
        void BindPilots(VisualElement root)
        {
            pilotCard = root.Q("pilotRoot"); motorChoices = root.Q("motorChoices"); typeChoices = root.Q("typeChoices"); directionChoices = root.Q("directionChoices");
            pilotMessage = root.Q<Label>("pilotMessage"); pilotProgress = root.Q<Label>("pilotProgress"); selection = root.Q<Label>("selection");
            root.Q<Button>("checkHardware").clicked += () => pilot.CheckHardware();
            root.Q<Button>("calibrate").clicked += () => pilot.Begin(PilotKind.Calibration);
            root.Q<Button>("typePilot").clicked += () => pilot.Begin(PilotKind.TypeIdentification);
            root.Q<Button>("directionPilot").clicked += () => pilot.Begin(PilotKind.DirectionIdentification);
            root.Q<Button>("pilotSubmit").clicked += () => pilot.SubmitComfortable();
            root.Q<Button>("pilotDiscomfort").clicked += () => pilot.Discomfort();
            root.Q<Button>("pilotStop").clicked += () => pilot.End("Participant aborted");
            root.Q<Button>("openPilotResults").clicked += () => { if (!string.IsNullOrEmpty(pilot.ResultDirectory)) Application.OpenURL(new Uri(pilot.ResultDirectory + "/").AbsoluteUri); };
            motorChoices.Clear();
            foreach (string panelName in new[] { "Front", "Back" })
            {
                int offset = panelName == "Front" ? 0 : 20;
                var panel = new VisualElement(); panel.AddToClassList("motor-panel");
                panel.Add(new Label(panelName + " — wearer's left to right"));
                for (int r = 0; r < 5; r++)
                {
                    var line = new VisualElement(); line.AddToClassList("row");
                    for (int c = 0; c < 4; c++) { int index = offset + r * 4 + c; line.Add(new Button(() => pilot.SelectMotor(index)) { text = index.ToString() }); }
                    panel.Add(line);
                }
                motorChoices.Add(panel);
            }
            for (int i = 0; i < 3; i++)
            {
                int type = i;
                typeChoices.Add(new Button(() => pilot.SelectType(type)) { text = ((EventType)type).ToString() });
                root.Q("training").Add(new Button(() => pilot.PlayReference((EventType)type, Session.Condition, 0)) { text = "TRAINING: " + ((EventType)type) });
            }
            string[] directions = { "Front", "Right", "Back", "Left" };
            for (int i = 0; i < 4; i++) { int d = i; directionChoices.Add(new Button(() => pilot.SelectDirection(d)) { text = directions[d] });
                root.Q("directionTraining").Add(new Button(() => pilot.PlayReference(EventType.Footsteps, FeedbackCondition.TypeDirectionHaptic, d)) { text = "TRAINING: " + directions[d] }); }
        }
        void StartBlock(bool practice)
        {
            if (Session.StartSession(practice))
            { card.AddToClassList("running"); GetComponent<UIDocument>().rootVisualElement.focusController.focusedElement?.Blur(); }
        }
        void Update()
        {
            if (!Session || card == null) return;
            card.EnableInClassList("running", Session.Running || pilot && pilot.Running);
            if (pilot)
            {
                pilotCard.style.display = pilot.Running ? DisplayStyle.Flex : DisplayStyle.None;
                if (pilot.Running)
                {
                    pilotProgress.text = pilot.Kind + " · " + (pilot.TrialIndex + 1) + "/" + pilot.Expected
                        + (Session.Stimulus.Mode == OutputMode.Simulation ? " · SIMULATION / no certification" : " · PHYSICAL");
                    motorChoices.style.display = pilot.Kind == PilotKind.Calibration ? DisplayStyle.Flex : DisplayStyle.None;
                    typeChoices.style.display = pilot.Kind != PilotKind.Calibration ? DisplayStyle.Flex : DisplayStyle.None;
                    directionChoices.style.display = pilot.Kind == PilotKind.DirectionIdentification ? DisplayStyle.Flex : DisplayStyle.None;
                    selection.text = pilot.Selected;
                    motorChoices.SetEnabled(pilot.AwaitingResponse); typeChoices.SetEnabled(pilot.AwaitingResponse); directionChoices.SetEnabled(pilot.AwaitingResponse);
                    return;
                }
                pilotMessage.text = pilot.Message;
                rootLabel("pilotInstructions", "Select what you felt, then confirm comfortable. First choices lock. Timeout is recorded as NoResponse.");
            }
            if (Session.Practice && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                ShowPracticeSimulator = !ShowPracticeSimulator;
            if (Session.Running) return;
            message.text = Session.Message;
            order.text = "Williams order: " + string.Join(" > ", SilentTrialPlan.ConditionOrder(Session.ParticipantIndex));
            openResults.SetEnabled(!string.IsNullOrEmpty(Session.ResultDirectory));
            var m = Session.Summary;
            results.text = Session.ResultDirectory + "\nValid trials " + m.ValidTrials + " · objective " + Display(m.ObjectiveRatePct)
                + "% · detection " + Display(m.DetectionAccuracyPct) + "%\nType " + Display(m.TypeAccuracyPct)
                + "% · false alarms " + Display(m.FalseAlarmRatePct) + "%\nDirection responses " + m.DirectionResponses
                + " · correct defense responses " + m.CorrectDefenseResponses;
        }
        void rootLabel(string name, string text) { GetComponent<UIDocument>().rootVisualElement.Q<Label>(name).text = text; }
        static string Display(string value) => value == "" ? "N/A" : value;
        // Passive debug drawing; all interactive controls use UI Toolkit and the existing Input System.
        void OnGUI()
        {
            if (!Session || !Session.Running || !Session.Practice || !ShowPracticeSimulator) return;
            var row = Session.Current;
            GUI.Box(new Rect(12, 148, 330, 270), "PRACTICE / SIMULATOR");
            GUI.Label(new Rect(22, 174, 315, 50), (row == null ? "" : row.EventType + " · onset bearing " + row.EventDirection) + "\n" + Session.Stimulus.Status);
            for (int i = 0; i < 40; i++)
            {
                var motor = GCCD.ContinuousHaptics.X40MotorLayout.Get(i);
                int column = i % 4 + (motor.IsFront ? 0 : 5), intensity = Session.Stimulus.Motor(i);
                GUI.color = Color.Lerp(new Color(.2f, .2f, .2f), new Color(1, .6f, .2f), intensity / 100f);
                GUI.Box(new Rect(26 + column * 32, 238 + motor.Row * 30, 28, 26), intensity.ToString());
            }
            GUI.color = Color.white;
        }
    }
}
