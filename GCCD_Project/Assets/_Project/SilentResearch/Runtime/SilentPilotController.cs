using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GCCD.ContinuousHaptics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GCCD.SilentResearch
{
    public sealed class SilentPilotController : MonoBehaviour
    {
        public SilentExperimentSession Session;
        public bool AutomaticTicks = true;
        public string ResultRoot = "";
        public bool Running { get; private set; }
        public bool AwaitingResponse => Running && delivered && StudyClock.Now < deadline;
        public PilotKind Kind { get; private set; }
        public string Message { get; private set; } = "Physical calibration and identification pending";
        public string ResultDirectory { get; private set; } = "";
        public PilotSummary Summary { get; private set; }
        public int TrialIndex { get; private set; }
        public int Expected => Kind == PilotKind.Calibration ? 40 : SilentPilotEvidence.IdentificationTrials;
        public IReadOnlyList<TrialRecord> Records => rows;
        public TrialRecord Current => row;
        public string Selected => selectedMotor >= 0 ? "Location " + selectedMotor : selectedType >= 0 ? ((EventType)selectedType) + (selectedDirection < 0 ? "" : " / " + selectedDirection * 90 + " deg") : "No selection";
        readonly List<TrialRecord> rows = new List<TrialRecord>();
        readonly List<PilotEvidence> certificates = new List<PilotEvidence>();
        PilotEvidence evidence;
        SilentDataRecorder recorder;
        TrialRecord row;
        int[] channels;
        SilentTrial[] plan;
        int selectedMotor = -1, selectedType = -1, selectedDirection = -1;
        double scheduled, onset, deadline, advanceAt, lastTick, checkAt;
        bool delivered, finished, physical;
        OutputMode mode;
        string fingerprint, device, participant;
        float exposure;
        BHapticsVestOutput Vest => Session && Session.Stimulus ? Session.Stimulus.Vest as BHapticsVestOutput : null;
        string Root => string.IsNullOrWhiteSpace(ResultRoot) ? Path.Combine(Application.persistentDataPath, "SilentPilots") : ResultRoot;
        void Start() { LoadCertificates(); }
        public void LoadCertificates()
        {
            certificates.Clear();
            if (!Directory.Exists(Root)) return;
            foreach (var file in Directory.GetFiles(Root, "evidence.json", SearchOption.AllDirectories))
                try { var saved = JsonUtility.FromJson<PilotEvidence>(File.ReadAllText(file)); if (saved != null && SilentPilotEvidence.Evaluate(saved).Passed) certificates.Add(saved); }
                catch (Exception) { /* An incomplete or malformed file never unlocks measurement. */ }
        }
        public bool CalibrationMatches()
        {
            if (!Vest || !Vest.Physical || !Session.Stimulus.Patterns) return false;
            string hash = SilentPilotEvidence.Fingerprint(Session.Stimulus.Patterns);
            return certificates.Any(e => SilentPilotEvidence.Matches(e, PilotKind.Calibration, Vest.DeviceKey, hash));
        }
        public bool PatternsPassed(string id, bool direction)
        {
            if (!CalibrationMatches()) return false;
            string hash = SilentPilotEvidence.Fingerprint(Session.Stimulus.Patterns);
            bool type = certificates.Any(e => SilentPilotEvidence.Matches(e, PilotKind.TypeIdentification, Vest.DeviceKey, hash, id));
            return type && (!direction || certificates.Any(e => SilentPilotEvidence.Matches(e, PilotKind.DirectionIdentification, Vest.DeviceKey, hash, id)));
        }
        public bool CheckHardware()
        {
            bool connected = Vest && Vest.CheckConnection();
            Message = Vest ? Vest.Status + (CalibrationMatches() ? " / calibration passed" : " / physical calibration pending") : "Missing X40 adapter";
            return connected;
        }
        public void PlayReference(EventType type, FeedbackCondition condition, int direction)
        {
            if (Running || Session.Running) return;
            if (Session.Stimulus.Mode == OutputMode.BHaptics && (!CheckHardware() || !CalibrationMatches()))
            { Message = "Physical training requires the 40-channel calibration first"; return; }
            if (condition != FeedbackCondition.TypeHaptic && condition != FeedbackCondition.TypeDirectionHaptic) condition = FeedbackCondition.TypeHaptic;
            var view = Session.Stimulus.View;
            Session.Stimulus.Begin(new SoundEvent { Type = type, IsThreat = true, Onset = StudyClock.Now,
                Position = view.position + Quaternion.Euler(0, view.eulerAngles.y + direction * 90, 0) * Vector3.forward * 4 }, condition);
            Message = "LABELLED TRAINING (excluded from evidence): " + type + " / " + direction * 90 + " deg";
        }
        public bool Begin(PilotKind kind)
        {
            if (Running || !Session || Session.Running) return false;
            if (!Regex.IsMatch(Session.ParticipantID ?? "", @"^[A-Za-z0-9_-]{1,24}$")) { Message = "Set an anonymous participant ID first"; return false; }
            Kind = kind; mode = Session.Stimulus.Mode; physical = mode == OutputMode.BHaptics;
            if (!Session.Stimulus.Patterns || !Session.Stimulus.Patterns.Valid || !Session.Stimulus.View)
            { Message = "Invalid haptic patterns / view"; return false; }
            if (!Session.Stimulus.Ready(FeedbackCondition.TypeDirectionHaptic, true, out string reason)) { Message = reason; return false; }
            if (physical && (!CheckHardware() || !Vest.Physical)) return false;
            if (physical && kind != PilotKind.Calibration && !CalibrationMatches()) { Message = "Complete physical 40-channel calibration first"; return false; }
            participant = Session.ParticipantID; fingerprint = SilentPilotEvidence.Fingerprint(Session.Stimulus.Patterns);
            exposure = Session.Stimulus.ExposureSec;
            device = physical ? Vest.DeviceKey : "";
            int seed = Session.RandomSeed ^ (0x234500 + (int)kind);
            channels = SilentPilotEvidence.Channels(seed); plan = SilentPilotEvidence.IdentificationPlan(seed);
            evidence = new PilotEvidence { Kind = kind, Seed = seed, ParticipantID = participant, DeviceKey = device, PatternHash = fingerprint,
                OutputMode = mode.ToString(), CreatedUtc = DateTimeOffset.UtcNow.ToString("O") };
            try
            {
                string id = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                recorder = new SilentDataRecorder(Root, id, JsonUtility.ToJson(evidence, true)); ResultDirectory = recorder.DirectoryPath;
            }
            catch (Exception e) { Message = "Cannot open pilot files: " + e.GetType().Name; return false; }
            Session.World.Stop(); Session.World.Player.ResetPose(Session.Scenario.PlayerStart, Quaternion.identity);
            Session.World.Player.CaptureCursor(false); Session.Stimulus.Stop();
            rows.Clear(); Summary = null; TrialIndex = -1; Running = true; lastTick = StudyClock.Now; checkAt = lastTick + .5;
            Next(lastTick); return true;
        }
        void Next(double now)
        {
            if (++TrialIndex >= Expected) { End("Pilot complete", true); return; }
            delivered = finished = false; selectedMotor = selectedType = selectedDirection = -1;
            scheduled = now + (Kind == PilotKind.Calibration ? 1.5 : plan[TrialIndex].Foreperiod);
            advanceAt = deadline = double.PositiveInfinity;
            row = new TrialRecord { Purpose = "HardwarePilot", PilotStage = Kind.ToString(), ParticipantID = participant,
                SessionID = Path.GetFileName(ResultDirectory), TrialID = (TrialIndex + 1).ToString(), RandomSeed = evidence.Seed.ToString(),
                ConfigHash = fingerprint, DeviceKey = device, OutputMode = mode.ToString(), Practice = "false", Valid = "false",
                ScheduledOnsetSec = TrialRecord.Number(scheduled), Condition = Kind == PilotKind.TypeIdentification ? "TypeHaptic" : "TypeDirectionHaptic" };
            if (Kind == PilotKind.Calibration)
            { row.MotorIndex = channels[TrialIndex].ToString(); row.Intensity = "35"; row.DurationMs = "150"; row.CorrectResponse = row.MotorIndex; }
            else
            { row.EventType = plan[TrialIndex].Type.ToString(); row.EventDirection = (plan[TrialIndex].DirectionIndex * 90).ToString(); }
            SaveCheckpoint(); Message = "Wait for the cue, then select what you felt. " + (physical ? "Physical pilot" : "SIMULATION — cannot certify hardware");
        }
        void Update()
        {
            if (!Running || !AutomaticTicks) return;
            Tick(StudyClock.Now);
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) End("Participant aborted");
        }
        public void Tick(double now)
        {
            if (!Running) return;
            double gap = now - lastTick; lastTick = now;
            if (!StudyClock.Finite(now) || gap < 0 || physical && gap > .25) { End("Timing failure / frame gap >250ms"); return; }
            if (now >= checkAt)
            {
                checkAt = now + .5;
                if (mode != Session.Stimulus.Mode || participant != Session.ParticipantID || exposure != Session.Stimulus.ExposureSec || fingerprint != SilentPilotEvidence.Fingerprint(Session.Stimulus.Patterns))
                { End("Pilot configuration changed"); return; }
                if (physical && (!Vest.CheckConnection() || device != Vest.DeviceKey)) { End("X40 disconnected / changed"); return; }
            }
            if (finished) { if (now >= advanceAt) Next(now); return; }
            if (!delivered && now >= scheduled)
            {
                delivered = true; onset = now; deadline = now + (Kind == PilotKind.Calibration ? 8 : 4);
                row.OnsetMonotonicSec = TrialRecord.Number(now); row.OnsetJitterMs = TrialRecord.Number((now - scheduled) * 1000);
                bool accepted = true;
                if (Kind == PilotKind.Calibration)
                {
                    var motors = new int[40]; motors[channels[TrialIndex]] = 35;
                    if (physical) accepted = Vest.TrySend(motors, 150);
                }
                else
                {
                    var t = plan[TrialIndex]; var view = Session.Stimulus.View;
                    Vector3 position = view.position + Quaternion.Euler(0, view.eulerAngles.y + t.DirectionIndex * 90, 0) * Vector3.forward * 4;
                    Session.Stimulus.Begin(new SoundEvent { IsThreat = true, Type = t.Type, Position = position, Onset = now },
                        Kind == PilotKind.TypeIdentification ? FeedbackCondition.TypeHaptic : FeedbackCondition.TypeDirectionHaptic);
                    accepted = !Session.Stimulus.HardwareFailed;
                }
                row.DeliveryAccepted = TrialRecord.Bool(accepted && physical);
                row.HardwareStatus = physical ? Vest.Status : "Simulation";
                SaveCheckpoint();
                if (!accepted) { End("SDK rejected pilot output"); return; }
            }
            Session.Stimulus.Tick(now);
            if (Session.Stimulus.HardwareFailed && Kind != PilotKind.Calibration) { End("Pattern delivery / timing failed"); return; }
            if (delivered && now >= deadline) Complete(now, false);
        }
        bool CanAnswer(double now) => Running && delivered && !finished && now >= onset && now < deadline;
        public void SelectMotor(int index)
        {
            double now = StudyClock.Now;
            if (Kind != PilotKind.Calibration || selectedMotor >= 0 || index < 0 || index >= 40 || !CanAnswer(now)) return;
            selectedMotor = index; row.PerceivedMotor = index.ToString(); row.TypeTimeMs = TrialRecord.Number((now - onset) * 1000); SaveCheckpoint();
        }
        public void SelectType(int type)
        {
            double now = StudyClock.Now;
            if (Kind == PilotKind.Calibration || selectedType >= 0 || type < 0 || type >= 3 || !CanAnswer(now)) return;
            selectedType = type; row.TypeResponse = ((EventType)type).ToString(); row.TypeTimeMs = TrialRecord.Number((now - onset) * 1000); SaveCheckpoint();
        }
        public void SelectDirection(int direction)
        {
            double now = StudyClock.Now;
            if (Kind != PilotKind.DirectionIdentification || selectedDirection >= 0 || direction < 0 || direction >= 4 || !CanAnswer(now)) return;
            selectedDirection = direction; row.DirectionResponse = (direction * 90).ToString(); row.DirectionTimeMs = TrialRecord.Number((now - onset) * 1000);
            row.DirectionErrorDeg = TrialRecord.Number(SilentTrialPlan.CircularError(plan[TrialIndex].DirectionIndex * 90, direction * 90)); SaveCheckpoint();
        }
        public void SubmitComfortable()
        {
            double now = StudyClock.Now; if (!CanAnswer(now)) return;
            if (Kind == PilotKind.Calibration ? selectedMotor < 0 : selectedType < 0 || Kind == PilotKind.DirectionIdentification && selectedDirection < 0) return;
            Complete(now, true);
        }
        void Complete(double now, bool comfortable)
        {
            if (finished) return;
            finished = true; row.Status = "Completed"; row.Comfortable = TrialRecord.Bool(comfortable);
            row.Valid = TrialRecord.Bool(physical && row.DeliveryAccepted == "true");
            if (comfortable)
            {
                row.ReactionTimeMs = TrialRecord.Number((now - onset) * 1000);
                if (Kind == PilotKind.Calibration) { row.PerceivedMotor = selectedMotor.ToString(); row.PlayerResponse = row.PerceivedMotor; row.IsCorrect = TrialRecord.Bool(row.MotorIndex == row.PerceivedMotor); }
                else
                {
                    row.TypeResponse = ((EventType)selectedType).ToString();
                    row.IsCorrect = TrialRecord.Bool(row.TypeResponse == row.EventType);
                    if (Kind == PilotKind.DirectionIdentification)
                    { row.DirectionResponse = (selectedDirection * 90).ToString(); }
                    row.PlayerResponse = row.TypeResponse + ";" + row.DirectionResponse;
                }
            }
            else { row.IsCorrect = TrialRecord.Bool(Kind == PilotKind.Calibration ? row.PerceivedMotor == row.MotorIndex : row.TypeResponse == row.EventType);
                row.PlayerResponse = Kind == PilotKind.Calibration ? row.PerceivedMotor : row.TypeResponse + ";" + row.DirectionResponse;
                if (row.PlayerResponse == "" || row.PlayerResponse == ";") row.PlayerResponse = "NoResponse"; }
            Session.Stimulus.Stop(); Vest?.Stop(); SaveFinal(); advanceAt = now + .75;
            // No truth / score is shown between blind trials.
            Message = "Response saved. Wait for the next cue.";
        }
        public void End(string reason = "Researcher aborted", bool complete = false)
        {
            if (!Running) return;
            Running = false; Session.Stimulus.Stop(); Vest?.Stop();
            if (!finished && row != null)
            { row.Status = "Aborted"; row.Valid = "false"; row.AbortReason = reason; SaveFinal(); }
            evidence.Complete = complete; evidence.Trials = rows.ToArray();
            Summary = SilentPilotEvidence.Evaluate(evidence);
            try
            {
                recorder?.Dispose(); recorder = null;
                File.WriteAllText(Path.Combine(ResultDirectory, "evidence.json"), JsonUtility.ToJson(evidence, true));
                File.WriteAllText(Path.Combine(ResultDirectory, "summary.json"), JsonUtility.ToJson(Summary, true));
                if (Summary.Passed) certificates.Add(evidence);
                Message = reason + ". " + Summary.Decision;
            }
            catch (Exception e) { Message = "Pilot final save failed: " + e.GetType().Name; }
        }
        void SaveCheckpoint()
        { try { recorder.Checkpoint(row); } catch (Exception e) { End("Pilot journal failed: " + e.GetType().Name); } }
        void SaveFinal()
        { try { if (recorder.Complete(row)) rows.Add(row); } catch (Exception e) { Message = "Pilot record failed: " + e.GetType().Name; Running = false; recorder?.Dispose(); recorder = null; } }
        public void Discomfort() => End("Discomfort reported; recalibration / intensity review required");
        void OnDisable() => End("Pilot disabled");
        void OnApplicationQuit() => End("Application quit");
        void OnApplicationFocus(bool focused) { if (!focused) End("Focus lost"); }
        void OnApplicationPause(bool paused) { if (paused) End("Application paused"); }
    }
}
