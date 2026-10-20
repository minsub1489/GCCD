using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GCCD.Research;
using UnityEditor;
using UnityEngine;

namespace GCCD.Editor
{
    /// <summary>Fast editor-only checks of serializer and hardware-safe output boundaries.</summary>
    public static class StudyValidation
    {
        [MenuItem("GCCD / Run Research Core Smoke Tests")]
        public static void Run()
        {
            string spatialEvidence = SpatialMotorMappingTests.RunAll();
            Debug.Log("GCCD_360_MOTOR_MAPPING_VALIDATION_SUCCESS " + spatialEvidence);
            // Check the actual session order generator (not a separately reimplemented formula).
            var orderMethod = typeof(ResearchController).GetMethod("BuildOrder", BindingFlags.Static | BindingFlags.NonPublic);
            if (orderMethod == null) throw new Exception("Unable to inspect counterbalanced study order");
            var positionCounts = new Dictionary<string, int>();
            var transitions = new HashSet<string>();
            for (int row = 0; row < 8; ++row)
            {
                var order = (ResearchCondition[])orderMethod.Invoke(null, new object[] { row });
                if (order.Length != 8 || order.Select(c => c.code).Distinct().Count() != 8)
                    throw new Exception("Condition missing/duplicate in row " + row);
                for (int position = 0; position < 8; ++position)
                {
                    string key = position + "|" + order[position].code;
                    if (positionCounts.ContainsKey(key)) throw new Exception("Williams position imbalance: " + key);
                    positionCounts[key] = 1;
                    if (position < 7)
                    {
                        string pair = order[position].code + "->" + order[position + 1].code;
                        if (!transitions.Add(pair)) throw new Exception("Repeated directional predecessor pair: " + pair);
                    }
                }
            }
            if (positionCounts.Count != 64 || transitions.Count != 56)
                throw new Exception("Incomplete Williams 8×8 position/predecessor balance");

            var disabled = new ResearchDisabledX40Haptics();
            var nonzero = new float[40];
            nonzero[3] = .5f;
            if (disabled.IsVerifiedReady || disabled.SubmitFrame(nonzero, .05f, "smoke").accepted)
                throw new Exception("Unvalidated real X40 adapter unexpectedly allowed output");
            var mock = new ResearchMockHaptics();
            var result = mock.SubmitFrame(nonzero, .05f, "smoke");
            if (!result.accepted || result.physicallyDelivered || result.source != "mock_40_channel")
                throw new Exception("Mock output misreported physical delivery");
            var zero = new float[40];
            result = mock.SubmitFrame(zero, .05f, "baseline");
            if (!result.accepted || result.physicallyDelivered || mock.Latest[3] != 0)
                throw new Exception("Zero-intensity baseline retained active haptics");

            var snapshot = new ResearchSnapshot
            {
                participant_id = "SYNTHETIC_SMOKE",
                session_id = "synthetic_" + Guid.NewGuid().ToString("N"),
                mode = "Demo",
                started_at_utc = DateTime.UtcNow.ToString("O"),
                hardware_verified = false,
                haptic_backend = "Mock",
                trials_per_block = 1,
                assigned_order = new [] { new ResearchCondition { code = "S0", threat_count = 1, intensity_level = "none" } }
            };
            using (var writer = new ResearchJsonlWriter())
            {
                if (!writer.Open(snapshot)) throw new Exception(writer.LastError);
                var entries = new[]
                {
                    Make(snapshot.session_id, "session_start", "", 0, "", "Demo"),
                    Make(snapshot.session_id, "trial_start", "synthetic_trial", 0, "", "Demo"),
                    Make(snapshot.session_id, "spawn", "synthetic_trial", 0, "threat_01", "Demo", 5),
                    Make(snapshot.session_id, "warning", "synthetic_trial", 0, "", "Demo"),
                    Make(snapshot.session_id, "first_acquired", "synthetic_trial", 2, "threat_01", "Demo"),
                    Make(snapshot.session_id, "response_attempt", "synthetic_trial", 3, "threat_01", "Demo"),
                    Make(snapshot.session_id, "neutralized", "synthetic_trial", 4, "threat_01", "Demo"),
                    Make(snapshot.session_id, "trial_end", "synthetic_trial", 8, "", "Demo")
                };
                foreach (var entry in entries)
                    if (!writer.Write(entry)) throw new Exception(writer.LastError);
                var log = Path.Combine(writer.Folder, "events.jsonl");
                var lines = File.ReadAllLines(log);
                if (lines.Length != entries.Length) throw new Exception("Missing synthetic log lines");
                for (int i = 0; i < lines.Length; i++)
                {
                    var deserialized = JsonUtility.FromJson<ResearchEvent>(lines[i]);
                    if (deserialized.sequence_number != i + 1) throw new Exception("Broken event ordering");
                }
                Debug.Log("GCCD_EDITOR_CORE_VALIDATION_SUCCESS " + log);
            }
        }

        private static ResearchEvent Make(string sessionId, string type, string trialId, double at, string target, string mode, double deadline = 0)
        {
            return new ResearchEvent
            {
                event_type = type,
                session_id = sessionId,
                participant_id = "SYNTHETIC_SMOKE",
                trial_id = trialId,
                condition = "S0",
                threat_count = 1,
                intensity_level = "none",
                trial_time = at,
                session_time_monotonic = 1 + at,
                target_id = target,
                payload = new ResearchEventPayload
                {
                    mode = mode,
                    zone = "FrontLeft",
                    deadline_time = deadline,
                    event_source = "editor_synthetic_smoke",
                    attempted = type == "response_attempt",
                    valid = true
                }
            };
        }
    }
}
