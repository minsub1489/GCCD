"""Synthetic event contracts. Run: python3 -m unittest discover -s analysis/tests -v"""

import json
import tempfile
import unittest
from pathlib import Path

from analysis.replay import CONDITIONS, evaluate, main, reconstruct, rotation, summarize


def event(kind, trial="1", at=0.0, condition="SL", target=None, **payload):
    return {
        "event_type": kind,
        "session_id": "session-demo", "participant_id": "participant-01", "trial_id": trial,
        "condition": condition, "trial_time": at,
        "target_id": target, "sequence_number": 0,
        "payload": {"mode": "Demo", **payload},
    }


def events_for_trial(code, trial, neutralized=None, attempted=None, acquired=None):
    count = 1 if code.startswith("S") else 3
    result = [event("trial_start", trial, -1, code, threat_count=count, seed=77, block_index=2)]
    for i in range(count):
        target = f"t{i}"
        result.append(event("spawn", trial, 0, code, target=target,
                            zone=("FrontLeft", "FrontRight", "BackLeft")[i], deadline_time=4.0))
    if acquired is not None:
        result.append(event("first_acquired", trial, acquired, code, target="t0"))
    if attempted is not None:
        result.append(event("response_attempt", trial, attempted, code, target="t0"))
    if neutralized is not None:
        result.append(event("neutralized", trial, neutralized, code, target="t0"))
    result.append(event("camera_sample", trial, .5, code, camera_yaw=350))
    result.append(event("camera_sample", trial, 1, code, camera_yaw=10))
    result.append(event("camera_sample", trial, 1.5, code, camera_yaw=5))
    result.append(event("trial_end", trial, 5, code, yaw_path_deg=999))
    return result


def replay(records):
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / "events.jsonl"
        with path.open("w", encoding="utf8") as f:
            for record in records:
                f.write(json.dumps(record, ensure_ascii=False) + "\n")
        return reconstruct(path)


class ReplayTests(unittest.TestCase):
    def test_single_acquisition_missed_deadline_even_when_equal(self):
        trials, issues = replay(events_for_trial("SL", "1", neutralized=4, attempted=1, acquired=.7))
        threats, summary = evaluate(trials)
        self.assertEqual(issues, [])
        self.assertEqual(threats[0]["failure"], 1)
        self.assertEqual(threats[0]["no_response"], 0)
        self.assertEqual(threats[0]["first_acquisition_latency_s"], .7)
        self.assertEqual(summary[0]["first_shot_target"], "t0")
        # Only one camera sample precedes first acquisition: path is unknown.
        self.assertIsNone(summary[0]["camera_path_deg"])
        self.assertEqual(summary[0]["reported_yaw_path_deg"], 999)
        self.assertEqual(summary[0]["first_acquisition_latency_s"], .7)

    def test_multi_three_unique_zones_and_no_response(self):
        trials, _ = replay(events_for_trial("MH", "1", neutralized=2, attempted=1))
        threats, summary = evaluate(trials)
        self.assertEqual([r["failure"] for r in threats], [0, 1, 1])
        self.assertEqual([r["no_response"] for r in threats], [0, 1, 1])
        self.assertIsNone(threats[1]["first_acquisition_latency_s"])
        self.assertEqual(summary[0]["failure_rate"], 2/3)
        self.assertEqual(summary[0]["technical_excluded"], 0)
        self.assertEqual(summary[0]["camera_path_deg"], 25)
        self.assertEqual(summary[0]["camera_reversals"], 1)

    def test_damage_is_explicit_and_shots_deduplicated(self):
        records = events_for_trial("SL", "1", attempted=1)
        records.extend([event("shot", "1", 1.0002, target="t0", condition="SL"),
                        event("attack", "1", 2, target="t0", condition="SL", damage=5),
                        event("damage", "1", 2.1, target="t0", condition="SL", damage=5)])
        trials, _ = replay(records)
        threat, summary = evaluate(trials)
        self.assertEqual(threat[0]["shot_count"], 1)
        self.assertEqual(summary[0]["damage_total"], 5)

    def test_near_miss_inference_is_labeled_not_proven_intent(self):
        records = events_for_trial("SL", "1")
        records.extend([
            event("shot", "1", 1, "SL", target="t0", shot_index=1,
                  attribution_method="angle_proximity_inference", inferred_target_id="t0"),
            event("response_attempt", "1", 1.004, "SL", target="t0", shot_index=1,
                  attribution_method="angle_proximity_inference", attempted=True),
        ])
        threats, trials = evaluate(replay(records)[0])
        self.assertEqual(threats[0]["failure"], 1)
        self.assertEqual(threats[0]["no_response"], 0)
        self.assertEqual(threats[0]["shot_count"], 1)
        self.assertEqual(threats[0]["inferred_near_miss_shots"], 1)
        self.assertEqual(trials[0]["first_shot_attribution"], "angle_proximity_inference")

    def test_no_threat_trial_does_not_divide_by_zero(self):
        records = [event("trial_start", "empty", -1, "S0", threat_count=0),
                   event("trial_end", "empty", 5, "S0")]
        threats, summary = evaluate(replay(records)[0])
        self.assertEqual(threats, [])
        self.assertEqual(summary[0]["threat_count"], 0)
        self.assertIsNone(summary[0]["failure_rate"])
        self.assertIn("condition_threat_count_mismatch", summary[0]["exclusion_reasons"])

    def test_invalid_multi_trial_excluded_from_probability(self):
        records = events_for_trial("ML", "1", neutralized=2)
        for e in records:
            if e["event_type"] == "spawn" and e["target_id"] == "t2":
                e["payload"]["zone"] = "FrontLeft"
        threats, summary = evaluate(replay(records)[0])
        self.assertTrue(all(t["failure"] is None for t in threats))
        self.assertIn("multi_zone_not_unique", summary[0]["exclusion_reasons"])

    def test_missing_deadline_excludes_technical_error(self):
        records = events_for_trial("S0", "1")
        for record in records:
            record["payload"].pop("deadline_time", None)
        threats, summary = evaluate(replay(records)[0])
        self.assertIsNone(threats[0]["failure"])
        self.assertIn("missing_deadline", summary[0]["exclusion_reasons"])

    def test_main_mock_is_excluded(self):
        records = events_for_trial("SL", "1", neutralized=1)
        for e in records:
            e["payload"]["mode"] = "Main"
            e["payload"]["output_mode"] = "mock"
        threats, summary = evaluate(replay(records)[0])
        self.assertIsNone(threats[0]["failure"])
        self.assertIn("main_device_unverified", summary[0]["exclusion_reasons"])

    def test_main_zero_intensity_not_requested_is_valid_control(self):
        records = events_for_trial("S0", "1", neutralized=1)
        for e in records:
            e["payload"].update({"mode": "Main", "device_verified": True, "output_mode": "real"})
        records.append(event("haptic_result", "1", 0, "S0", target="t0",
                             mode="Main", device_verified=True, output_mode="real",
                             output_status="not_requested", delivered=False))
        threats, rows = evaluate(replay(records)[0])
        self.assertEqual(threats[0]["failure"], 0)
        self.assertEqual(rows[0]["technical_excluded"], 0)

    def test_unclosed_and_invalid_json(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "log.jsonl"
            path.write_text("{oops\n" + json.dumps(event("trial_start", "1", -1)) + "\n")
            trials, errors = reconstruct(path)
        self.assertEqual(errors[0]["code"], "invalid_json")
        _, summary = evaluate(trials)
        self.assertIn("missing_trial_end", summary[0]["exclusion_reasons"])

    def test_cli_damaged_line_excludes_all_trials(self):
        with tempfile.TemporaryDirectory() as directory:
            raw = Path(directory) / "events.jsonl"
            out = Path(directory) / "out"
            raw.write_text("\n".join(json.dumps(e) for e in events_for_trial("SL", "1", neutralized=1)) + "\n{broken\n")
            main([str(raw), "--out", str(out), "--mode", "Demo"])
            self.assertIn("untrusted_event_log", (out / "per_trial.csv").read_text())

    def test_snapshot_hash_and_metadata_carried_to_trial(self):
        with tempfile.TemporaryDirectory() as directory:
            raw = Path(directory) / "events.jsonl"
            out = Path(directory) / "out"
            raw.write_text("\n".join(json.dumps(e) for e in events_for_trial("SL", "1", neutralized=1)))
            (Path(directory) / "session.json").write_text(json.dumps({"participant_id": "participant-01", "session_id": "session-demo", "mode": "Demo", "config_hash": "test-hash", "active_duration_seconds": 5}))
            main([str(raw), "--out", str(out), "--mode", "Demo"])
            self.assertIn("test-hash", (out / "per_trial.csv").read_text())
            self.assertIn('"recorded_config_hash": "test-hash"', (out / "provenance.json").read_text())

    def test_primary_and_secondary_contrast_with_paired_participant(self):
        records = []
        # SL: success; SH: failure; ML: all failures; MH: one success, two failures;
        # M0: all failures. Primary = 2/3 - 1 - 1 + 0 = -4/3.
        for ix, code in enumerate(CONDITIONS):
            success = {"SL", "MH"}
            records.extend(events_for_trial(code, str(ix), neutralized=1 if code in success else None))
        threats, trial_rows = evaluate(replay(records)[0])
        cond, contrasts = summarize(threats, trial_rows)
        self.assertEqual(len([r for r in cond if r["participant_id"] == "ALL"]), 8)
        self.assertAlmostEqual(contrasts["Demo"]["primary_MH_minus_ML_minus_SH_plus_SL"], -4/3)
        self.assertAlmostEqual(contrasts["Demo"]["secondary_MH_minus_M0"], -1/3)
        self.assertEqual(contrasts["Demo"]["paired_participant_count"], 1)

    def test_camera_wrap_and_jitter(self):
        self.assertEqual(rotation([(0, 355), (1, 5), (2, 5.5), (3, 358)]), (18.0, 1))
        self.assertEqual(rotation([]), (None, None))

    def test_cli_writes_parse_diagnostics_and_columns(self):
        with tempfile.TemporaryDirectory() as directory:
            raw = Path(directory) / "events.jsonl"
            out = Path(directory) / "out"
            raw.write_text("\n".join(json.dumps(e) for e in events_for_trial("SL", "1", neutralized=1)) + "\n")
            self.assertEqual(main([str(raw), "--out", str(out), "--mode", "Demo"]), 0)
            for name in ("per_threat.csv", "per_trial.csv", "by_condition.csv", "contrasts.json", "parse_warnings.json"):
                self.assertTrue((out / name).is_file())
            self.assertIn("first_acquisition_latency_s", (out / "per_threat.csv").read_text())


if __name__ == "__main__":
    unittest.main()
