using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GCCD.SilentResearch
{
    [Serializable]
    public sealed class TrialRecord
    {
        public string SchemaVersion = "1", ParticipantID, SessionID, TrialID, Condition, ScenarioID, RandomSeed;
        public string EventType = "", EventDirection = "", CorrectResponse = "", PlayerResponse = "NoResponse";
        public string IsCorrect = "", ReactionTimeMs = "", DetectionSuccess = "", AvoidanceSuccess = "";
        public string ObjectiveSuccess = "", CompletionTimeSec = "", Timestamp;
        public string RowKind = "checkpoint", Status = "Pending", Valid = "false", Practice, OutputMode;
        public string BlockIndex, ParticipantIndex, ConditionOrder, ConfigHash, IsThreat;
        public string DetectionResponse = "", DetectionTimeMs = "", TypeResponse = "", TypeTimeMs = "";
        public string DirectionResponse = "", DirectionTruthAtResponseDeg = "", DirectionErrorDeg = "", DirectionTimeMs = "";
        public string OutcomeTimeSec = "", OnsetMonotonicSec = "", ScheduledOnsetSec = "", OnsetJitterMs = "";
        public string FirstThreatVisibleMs = "", PrematureResponseCount = "0", CorrectDefense = "";
        public string HardwareStatus = "", AbortReason = "";
        public string ControlHash = "", MaxFrameGapMs = "0", MaxHapticDispatchJitterMs = "";
        public string InputSeed = "";
        public string Purpose = "Measurement", PilotStage = "", MotorIndex = "", PerceivedMotor = "";
        public string DeliveryAccepted = "", Comfortable = "", DeviceKey = "", Intensity = "", DurationMs = "", Difficulty = "";
        public static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
        public static string Bool(bool value) => value ? "true" : "false";
    }

    // JSONL is an append-only recovery journal; each final CSV row has a unique session/trial key.
    public sealed class SilentDataRecorder : IDisposable
    {
        static readonly System.Reflection.FieldInfo[] Fields = typeof(TrialRecord).GetFields();
        readonly HashSet<string> completed = new HashSet<string>();
        readonly StreamWriter csv, journal;
        readonly FileStream csvFile, journalFile;
        public string DirectoryPath { get; }
        public string CsvPath { get; }
        bool disposed;

        public SilentDataRecorder(string root, string session, string configurationJson)
        {
            DirectoryPath = Path.Combine(root, session);
            Directory.CreateDirectory(DirectoryPath);
            CsvPath = Path.Combine(DirectoryPath, "trials.csv");
            // CreateNew refuses accidental session overwrite.
            csvFile = new FileStream(CsvPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            try
            {
                journalFile = new FileStream(Path.Combine(DirectoryPath, "journal.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                csv = new StreamWriter(csvFile, new UTF8Encoding(false));
                journal = new StreamWriter(journalFile, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(DirectoryPath, "configuration.json"), configurationJson, new UTF8Encoding(false));
                csv.WriteLine(string.Join(",", Fields.Select(f => f.Name)));
                Flush();
            }
            catch { csvFile.Dispose(); journalFile?.Dispose(); throw; }
        }
        public void Checkpoint(TrialRecord row)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SilentDataRecorder));
            row.Timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            journal.WriteLine(UnityEngine.JsonUtility.ToJson(row));
            journal.Flush(); journalFile.Flush(true);
        }
        public bool Complete(TrialRecord row)
        {
            if (completed.Contains(row.TrialID)) return false;
            row.RowKind = "final";
            // A durable final journal entry permits recovery even if the CSV write fails.
            Checkpoint(row);
            csv.WriteLine(string.Join(",", Fields.Select(f => Escape((string)f.GetValue(row) ?? ""))));
            csv.Flush(); csvFile.Flush(true); completed.Add(row.TrialID);
            return true;
        }
        public static string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        public void Flush() { csv.Flush(); journal.Flush(); csvFile.Flush(true); journalFile.Flush(true); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { csv?.Dispose(); } finally { journal?.Dispose(); }
        }
    }

    [Serializable]
    public sealed class MetricSummary
    {
        public int ValidTrials, ThreatTrials, CatchTrials, TypeResponses, DirectionResponses, CorrectDefenseResponses, CatchResponses, DetectionResponses;
        public string ObjectiveRatePct = "", DetectionAccuracyPct = "", TypeAccuracyPct = "", AvoidanceRatePct = "", FalseAlarmRatePct = "";
        public string MeanDirectionErrorDeg = "", MeanCorrectDefenseTimeMs = "", MeanCompletionTimeSec = "";
    }

    public static class SilentMetrics
    {
        public static MetricSummary Calculate(IEnumerable<TrialRecord> records, string purpose = "Measurement")
        {
            var rows = records.Where(r => r.RowKind == "final" && r.Valid == "true" && r.Practice == "false" && r.Purpose == purpose)
                .GroupBy(r => r.SessionID + "/" + r.TrialID).Select(g => g.Last()).ToArray();
            var threats = rows.Where(r => r.IsThreat == "true").ToArray();
            var catches = rows.Where(r => r.IsThreat == "false").ToArray();
            return new MetricSummary {
                ValidTrials = rows.Length, ThreatTrials = threats.Length, CatchTrials = catches.Length,
                CatchResponses = catches.Count(r => r.DetectionResponse != ""), DetectionResponses = threats.Count(r => r.DetectionResponse != ""),
                TypeResponses = threats.Count(r => r.TypeResponse != ""), DirectionResponses = threats.Count(r => r.DirectionResponse != ""),
                CorrectDefenseResponses = threats.Count(r => r.ReactionTimeMs != ""),
                ObjectiveRatePct = Rate(rows.Count(r => r.ObjectiveSuccess == "true"), rows.Length),
                DetectionAccuracyPct = Rate(threats.Count(r => r.DetectionSuccess == "true"), threats.Length),
                TypeAccuracyPct = Rate(threats.Count(r => r.IsCorrect == "true"), threats.Length),
                AvoidanceRatePct = Rate(threats.Count(r => r.AvoidanceSuccess == "true"), threats.Length),
                FalseAlarmRatePct = Rate(catches.Count(r => r.DetectionResponse == "Threat"), catches.Length),
                MeanDirectionErrorDeg = Mean(threats.Select(r => r.DirectionErrorDeg)),
                MeanCorrectDefenseTimeMs = Mean(threats.Select(r => r.ReactionTimeMs)),
                MeanCompletionTimeSec = Mean(rows.Select(r => r.CompletionTimeSec))
            };
        }
        static string Rate(int numerator, int denominator) => denominator == 0 ? "" : TrialRecord.Number(100.0 * numerator / denominator);
        static string Mean(IEnumerable<string> values)
        {
            var numbers = values.Where(s => !string.IsNullOrEmpty(s)).Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            return numbers.Length == 0 ? "" : TrialRecord.Number(numbers.Average());
        }
    }
}
