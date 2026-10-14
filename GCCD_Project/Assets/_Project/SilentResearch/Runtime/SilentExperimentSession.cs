using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GCCD.ContinuousHaptics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GCCD.SilentResearch
{
    public sealed class SilentExperimentSession : MonoBehaviour
    {
        public SilentScenario Scenario;
        public SilentStimulusOutput Stimulus;
        public SilentGameWorld World;
        public FeedbackCondition Condition = FeedbackCondition.TypeDirectionHaptic;
        public string ParticipantID = "P001";
        public int ParticipantIndex, BlockIndex, RandomSeed = 1489;
        [Tooltip("Empty uses Application.persistentDataPath/SilentStudy. Anonymous IDs only.")]
        public string ResultRoot = "";
        public bool AutomaticTicks = true;
        public bool Running { get; private set; }
        public bool Practice { get; private set; }
        public string Purpose { get; private set; } = "Measurement";
        public string Message { get; private set; } = "Ready";
        public string ResultDirectory { get; private set; } = "";
        public int TrialIndex { get; private set; }
        public IReadOnlyList<SilentTrial> Plan => plan;
        public IReadOnlyList<TrialRecord> Records => records;
        public TrialRecord Current => row;
        public MetricSummary Summary => SilentMetrics.Calculate(records, Purpose);
        public float SafeReportAfterSec => (liveScenario ? liveScenario.MaximumForeperiod : Scenario ? Scenario.MaximumForeperiod : 3) + .1f;
        readonly List<TrialRecord> records = new List<TrialRecord>();
        List<SilentTrial> plan;
        SilentTrial trial;
        TrialRecord row;
        SilentDataRecorder recorder;
        SilentScenario liveScenario;
        SilentHapticPatterns sourcePatterns, livePatterns;
        FeedbackCondition fixedCondition;
        string session, configHash, fixedConfiguration, controlHash;
        double maxFrameGap;
        double started, scheduledOnset, onset, nextTrial, lastTick, checkAt;
        bool emitted, finalized, lastHidden;
        int premature;
        int actualSeed;
        [Serializable]
        sealed class Configuration
        {
            public string SchemaVersion = "1", ScenarioJson, PatternJson, Scene, Geometry;
            public string ParticipantID, Condition, OutputMode, ConditionOrder, Purpose;
            public int ParticipantIndex, BlockIndex, RandomSeed, InputSeed;
            public bool Practice, CalibrationVerified;
            public float Sensitivity, AudioGain, ExposureSec;
            public string[] AudioClips;
            public float[] AudioLengths;
        }
        string ConfigurationJson()
        {
            return JsonUtility.ToJson(new Configuration {
                ScenarioJson = JsonUtility.ToJson(Scenario), PatternJson = JsonUtility.ToJson(sourcePatterns ? sourcePatterns : Stimulus.Patterns),
                ParticipantID = ParticipantID, ParticipantIndex = ParticipantIndex, BlockIndex = BlockIndex, RandomSeed = actualSeed, InputSeed = RandomSeed,
                Practice = Practice, Purpose = Purpose, Condition = Condition.ToString(), OutputMode = Stimulus.Mode.ToString(),
                CalibrationVerified = Stimulus.X40CalibrationVerified, Sensitivity = World.Player.Sensitivity, AudioGain = Stimulus.AudioGain,
                ExposureSec = Stimulus.ExposureSec,
                ConditionOrder = string.Join(" > ", SilentTrialPlan.ConditionOrder(ParticipantIndex)),
                Scene = gameObject.scene.path, Geometry = Geometry(),
                AudioClips = new[] { Stimulus.Footsteps ? Stimulus.Footsteps.name : "", Stimulus.Door ? Stimulus.Door.name : "", Stimulus.Growl ? Stimulus.Growl.name : "" },
                AudioLengths = new[] { Stimulus.Footsteps ? Stimulus.Footsteps.length : 0, Stimulus.Door ? Stimulus.Door.length : 0, Stimulus.Growl ? Stimulus.Growl.length : 0 }
            }, true);
        }
        string Geometry()
        {
            // Static colliders and hiding bounds enter the configuration; moving actors do not.
            return string.Join(";", FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).OrderBy(c => c.name)
                .Select(c => c.name + ":" + c.transform.position.ToString("F4") + ":" + c.transform.rotation.ToString("F4")
                    + ":" + c.transform.lossyScale.ToString("F4") + ":" + c.size.ToString("F4") + ":" + c.isTrigger));
        }
        public bool StartSession(bool practice) => StartSession(practice, "Measurement");
        public bool StartDifficultyPilot() => StartSession(false, "DifficultyPilot");
        bool StartSession(bool practice, string purpose)
        {
            if (Running) return false;
            if (Stimulus && Stimulus.Pilot && Stimulus.Pilot.Running) { Message = "Finish the hardware pilot first"; return false; }
            if (!Regex.IsMatch(ParticipantID ?? "", @"^[A-Za-z0-9_-]{1,24}$"))
            { Message = "Use an anonymous ID: 1–24 letters, digits, _ or -"; return false; }
            if (!Scenario || !Scenario.Valid || !World || !World.Player || !World.Monster || !World.MonsterRenderer || !World.Exit
                || !Stimulus || !World.Player.View || Stimulus.View != World.Player.View.transform || !Enum.IsDefined(typeof(FeedbackCondition), Condition))
            { Message = "Invalid scenario / scene wiring"; return false; }
            if (!Stimulus.Ready(Condition, practice, out string reason)) { Message = reason; return false; }
            if (!practice && (Condition == FeedbackCondition.TypeHaptic || Condition == FeedbackCondition.TypeDirectionHaptic)
                && (!Stimulus.Pilot || !Stimulus.Pilot.PatternsPassed(ParticipantID, Condition == FeedbackCondition.TypeDirectionHaptic)))
            { Message = "This participant must pass the physical type / required direction identification pilot"; return false; }
            if (Stimulus.ExposureSec > Scenario.ResponseWindowSec) { Message = "Exposure must end before the response window closes"; return false; }
            // The participant scene must have exactly one audio source, owned by the condition router.
            if (FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(s => s.gameObject != Stimulus.gameObject))
            { Message = "Remove ambient/player/monster audio sources from this experiment scene"; return false; }
            Practice = practice; Purpose = practice ? "Practice" : purpose; fixedCondition = Condition;
            actualSeed = SilentTrialPlan.EffectiveSeed(RandomSeed, practice) ^ (purpose == "DifficultyPilot" ? 0x40D1FF : 0);
            sourcePatterns = Stimulus.Patterns;
            fixedConfiguration = ConfigurationJson(); configHash = Hash(fixedConfiguration);
            controlHash = Hash(JsonUtility.ToJson(Scenario) + Geometry() + actualSeed + "/" + World.Player.Sensitivity);
            liveScenario = Instantiate(Scenario); liveScenario.hideFlags = HideFlags.DontSave;
            livePatterns = Instantiate(sourcePatterns); livePatterns.hideFlags = HideFlags.DontSave; Stimulus.Patterns = livePatterns;
            plan = SilentTrialPlan.Create(actualSeed, liveScenario.Repetitions, liveScenario.MinimumForeperiod, liveScenario.MaximumForeperiod);
            records.Clear(); TrialIndex = -1; row = null;
            session = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                string root = string.IsNullOrWhiteSpace(ResultRoot) ? Path.Combine(Application.persistentDataPath, "SilentStudy") : ResultRoot;
                recorder = new SilentDataRecorder(root, session, fixedConfiguration); ResultDirectory = recorder.DirectoryPath;
            }
            catch (Exception e) { Cleanup(); Message = "Cannot open result files: " + e.GetType().Name; return false; }
            Running = true; Message = Purpose;
            World.Player.enabled = true; World.Player.CaptureCursor(true);
            lastTick = StudyClock.Now; checkAt = lastTick + .5;
            BeginTrial(lastTick); return Running;
        }
        static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
        void BeginTrial(double now)
        {
            if (++TrialIndex >= plan.Count) { StopSession("Block complete"); return; }
            Stimulus.Stop(); trial = plan[TrialIndex]; World.Prepare(liveScenario, trial);
            started = now; scheduledOnset = now + trial.Foreperiod; onset = double.NaN;
            emitted = finalized = lastHidden = false; premature = 0; maxFrameGap = 0;
            row = new TrialRecord {
                ParticipantID = ParticipantID, SessionID = session, TrialID = (TrialIndex + 1).ToString(),
                Condition = fixedCondition.ToString(), ScenarioID = liveScenario.ScenarioID, RandomSeed = actualSeed.ToString(), InputSeed = RandomSeed.ToString(),
                Practice = TrialRecord.Bool(Practice), OutputMode = Stimulus.Mode.ToString(), ParticipantIndex = ParticipantIndex.ToString(),
                BlockIndex = BlockIndex.ToString(), ConditionOrder = string.Join(" > ", SilentTrialPlan.ConditionOrder(ParticipantIndex)),
                ConfigHash = configHash, ControlHash = controlHash, Purpose = Purpose, Difficulty = liveScenario.ScenarioID,
                IsThreat = TrialRecord.Bool(trial.Threat), EventType = trial.Type.ToString(),
                CorrectResponse = trial.Threat ? "Threat;" + trial.Type + ";source bearing;" +
                    (trial.Type == EventType.Footsteps ? "Hide" : trial.Type == EventType.Growl ? "Evade" : "HideOrEvade") : "Safe",
                ScheduledOnsetSec = TrialRecord.Number(scheduledOnset),
                Valid = TrialRecord.Bool(!Practice), HardwareStatus = Stimulus.Status
            };
            Checkpoint();
        }
        void Update()
        {
            if (!AutomaticTicks || !Running) return;
            double now = StudyClock.Now;
            Tick(now, (float)(now - lastTick));
            if (!Running || finalized) return;
            var k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame) { StopSession("Participant aborted"); return; }
            World.SetCrouch(k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed);
            if (k.fKey.wasPressedThisFrame) Detect(true, now);
            if (k.gKey.wasPressedThisFrame) Detect(false, now);
            if (k.digit1Key.wasPressedThisFrame) Classify(EventType.Footsteps, now);
            if (k.digit2Key.wasPressedThisFrame) Classify(EventType.Door, now);
            if (k.digit3Key.wasPressedThisFrame) Classify(EventType.Growl, now);
            if (k.upArrowKey.wasPressedThisFrame) Direction(0, now);
            if (k.rightArrowKey.wasPressedThisFrame) Direction(90, now);
            if (k.downArrowKey.wasPressedThisFrame) Direction(180, now);
            if (k.leftArrowKey.wasPressedThisFrame) Direction(270, now);
            if (k.spaceKey.wasPressedThisFrame)
            {
                Vector3 source = trial.Threat ? liveScenario.EventAnchors[trial.DirectionIndex] : World.Player.transform.position + Vector3.forward;
                bool correct = World.TryEvade(source);
                if (correct) Defense("Evade", now);
            }
            if (World.Hidden && !lastHidden) Defense("Hide", now);
            lastHidden = World.Hidden;
        }
        public void Tick(double now, float dt)
        {
            if (!Running) return;
            if (!StudyClock.Finite(now) || now < lastTick) { StopSession("Non-monotonic time"); return; }
            if (!StudyClock.Finite(dt) || dt < 0) { StopSession("Invalid frame interval"); return; }
            lastTick = now;
            if (!finalized && row != null)
            {
                maxFrameGap = Math.Max(maxFrameGap, dt * 1000); row.MaxFrameGapMs = TrialRecord.Number(maxFrameGap);
                if (!Practice && dt > .25f) { StopSession("Frame interval exceeded 250ms"); return; }
            }
            if (now >= checkAt)
            {
                checkAt = now + .5;
                if (ConfigurationJson() != fixedConfiguration) { StopSession("Configuration changed"); return; }
                if (!Practice && fixedCondition != FeedbackCondition.AudioOnly &&
                    (!(Stimulus.Vest is BHapticsVestOutput output) || !output.CheckConnection()))
                { StopSession("X40 disconnected"); return; }
            }
            if (finalized) { if (now >= nextTrial) BeginTrial(now); return; }
            if (!emitted && now >= scheduledOnset)
            {
                onset = now; emitted = true; lastHidden = World.Hidden;
                row.OnsetMonotonicSec = TrialRecord.Number(now); row.OnsetJitterMs = TrialRecord.Number((now - scheduledOnset) * 1000);
                Vector3 position = trial.Threat ? liveScenario.EventAnchors[trial.DirectionIndex] : Vector3.zero;
                if (trial.Threat && SilentTrialPlan.TryBearing(Stimulus.View, position, out var direction))
                    row.EventDirection = TrialRecord.Number(direction.Azimuth);
                World.Onset(now);
                Stimulus.Begin(new SoundEvent { Type = trial.Type, IsThreat = trial.Threat, Position = position, Onset = now }, fixedCondition);
                row.HardwareStatus = Stimulus.Status; Checkpoint();
            }
            Stimulus.Tick(now);
            if (Stimulus.HardwareFailed) { StopSession("Stimulus delivery failed / missed pulse deadline"); return; }
            World.Tick(now, dt);
            if (World.ThreatVisible && row.FirstThreatVisibleMs == "" && emitted)
            { row.FirstThreatVisibleMs = TrialRecord.Number((now - onset) * 1000); Checkpoint(); }
            if (World.Caught) Finish(TrialOutcome.Caught, now);
            else if (emitted && World.GoalAvailable(now) && World.AtGoal) Finish(TrialOutcome.Success, now);
            else if (now - started >= liveScenario.TrialLimitSec) Finish(TrialOutcome.Timeout, now);
        }
        bool ResponseAllowed(double now)
        {
            if (!Running || finalized || !StudyClock.Finite(now) || now < started) return false;
            if (!emitted) { premature++; row.PrematureResponseCount = premature.ToString(); Checkpoint(); return false; }
            return now >= onset && now < onset + liveScenario.ResponseWindowSec;
        }
        public void Detect(bool threat, double now)
        {
            if (!Running || finalized || row.DetectionResponse != "") return;
            bool allowed = ResponseAllowed(now);
            if (allowed && !threat && now < started + SafeReportAfterSec)
            { premature++; row.PrematureResponseCount = premature.ToString(); Checkpoint(); return; }
            // An anticipatory threat report is a false alarm in a catch and is not a valid hit in a threat trial.
            if (!allowed && !(StudyClock.Finite(now) && now >= started && !emitted && threat)) return;
            row.DetectionResponse = threat ? "Threat" : "Safe";
            row.DetectionSuccess = TrialRecord.Bool(allowed && threat == trial.Threat);
            if (allowed) row.DetectionTimeMs = TrialRecord.Number((now - onset) * 1000);
            Checkpoint();
        }
        public void Classify(EventType type, double now)
        {
            if (type == EventType.None || !Enum.IsDefined(typeof(EventType), type) || !ResponseAllowed(now) || row.TypeResponse != "") return;
            row.TypeResponse = type.ToString(); row.TypeTimeMs = TrialRecord.Number((now - onset) * 1000);
            if (trial.Threat) row.IsCorrect = TrialRecord.Bool(type == trial.Type);
            Checkpoint();
        }
        public void Direction(float localBearing, double now)
        {
            if (!StudyClock.Finite(localBearing) || !ResponseAllowed(now) || row.DirectionResponse != "") return;
            if (!trial.Threat) return;
            if (!SilentTrialPlan.TryBearing(Stimulus.View, liveScenario.EventAnchors[trial.DirectionIndex], out var direction)) return;
            row.DirectionResponse = TrialRecord.Number(Mathf.Repeat(localBearing, 360));
            row.DirectionTruthAtResponseDeg = TrialRecord.Number(direction.Azimuth);
            row.DirectionErrorDeg = TrialRecord.Number(SilentTrialPlan.CircularError(direction.Azimuth, localBearing));
            row.DirectionTimeMs = TrialRecord.Number((now - onset) * 1000); Checkpoint();
        }
        public void Defense(string action, double now)
        {
            if (action != "Hide" && action != "Evade" || action == "Hide" && !World.Hidden
                || action == "Evade" && !World.LastEvadeCorrect) return;
            if (action == "Hide" && trial.Type == EventType.Growl || action == "Evade" && trial.Type == EventType.Footsteps) return;
            if (!ResponseAllowed(now) || !trial.Threat || row.ReactionTimeMs != "") return;
            row.CorrectDefense = action; row.ReactionTimeMs = TrialRecord.Number((now - onset) * 1000); Checkpoint();
        }
        void Finish(TrialOutcome outcome, double now)
        {
            if (finalized || row == null) return;
            finalized = true; row.Status = outcome.ToString();
            row.HardwareStatus = Stimulus.Status;
            if (emitted && trial.Threat && fixedCondition != FeedbackCondition.AudioOnly)
                row.MaxHapticDispatchJitterMs = TrialRecord.Number(Stimulus.MaxDispatchJitterMs);
            Stimulus.Stop();
            row.OutcomeTimeSec = TrialRecord.Number(now - started);
            row.ObjectiveSuccess = outcome == TrialOutcome.Aborted ? "" : TrialRecord.Bool(outcome == TrialOutcome.Success);
            if (outcome == TrialOutcome.Success) row.CompletionTimeSec = TrialRecord.Number(now - started);
            if (trial.Threat)
            {
                if (outcome != TrialOutcome.Aborted && row.DetectionSuccess == "") row.DetectionSuccess = "false";
                if (outcome != TrialOutcome.Aborted && row.IsCorrect == "") row.IsCorrect = "false";
                if (outcome != TrialOutcome.Aborted || World.AvoidanceEvaluated)
                    row.AvoidanceSuccess = TrialRecord.Bool(World.AvoidanceEvaluated && World.AvoidanceSuccess);
            }
            else if (outcome != TrialOutcome.Aborted && row.DetectionSuccess == "") row.DetectionSuccess = "false";
            row.PlayerResponse = string.Join(";", new[] { row.DetectionResponse, row.TypeResponse, row.DirectionResponse, row.CorrectDefense }
                .Where(s => !string.IsNullOrEmpty(s)));
            if (row.PlayerResponse == "") row.PlayerResponse = "NoResponse";
            try { recorder.Complete(row); records.Add(row); }
            catch (Exception e) { Message = "Recording failed: " + e.GetType().Name; StopSession(Message); return; }
            nextTrial = now + 1.5; Message = outcome.ToString();
        }
        void Checkpoint()
        {
            try { recorder?.Checkpoint(row); }
            catch (Exception e) { StopSession("Journal write failed: " + e.GetType().Name); }
        }
        public void StopSession(string reason = "Researcher aborted")
        {
            if (!Running) { Stimulus?.Stop(); return; }
            if (!finalized && row != null)
            {
                row.Valid = "false"; row.AbortReason = reason;
                Finish(TrialOutcome.Aborted, Math.Max(lastTick, started));
            }
            Running = false; Stimulus.Stop(); World.Stop();
            try
            {
                recorder?.Dispose(); recorder = null;
                File.WriteAllText(Path.Combine(ResultDirectory, "summary.json"), JsonUtility.ToJson(Summary, true));
                if (Purpose == "DifficultyPilot") File.WriteAllText(Path.Combine(ResultDirectory, "difficulty-review.json"),
                    JsonUtility.ToJson(SilentDifficultyReview.Evaluate(records, plan.Count), true));
            }
            catch (Exception e) { reason += "; final save failed: " + e.GetType().Name; }
            Cleanup(); Message = reason;
        }
        void Cleanup()
        {
            if (sourcePatterns && Stimulus) Stimulus.Patterns = sourcePatterns;
            if (livePatterns) Destroy(livePatterns);
            if (liveScenario) Destroy(liveScenario);
            livePatterns = null; liveScenario = null; sourcePatterns = null;
        }
        void OnDisable() => StopSession("Session disabled");
        void OnApplicationQuit() => StopSession("Application quit");
        void OnApplicationPause(bool pause) { if (pause) StopSession("Application paused"); }
        void OnApplicationFocus(bool focus) { if (!focus) StopSession("Focus lost"); }
    }
}
