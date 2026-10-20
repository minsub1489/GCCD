#!/usr/bin/env python3
"""Reconstruct research trials from append-only JSONL, without third-party packages.

This module intentionally never guesses missing deadlines or first-acquisition times.
Its output is descriptive; it does not claim participant-independent trials.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from statistics import mean
from typing import Any


CONDITIONS = ("S0", "SL", "SM", "SH", "M0", "ML", "MM", "MH")
MODES = ("Demo", "Pilot", "Main")
ZONE_NAMES = ("FrontLeft", "FrontRight", "BackLeft", "BackRight")


def normalized(value: Any) -> str:
    return re.sub(r"[^a-z0-9]+", "_", re.sub(r"([a-z])([A-Z])", r"\1_\2", str(value or ""))).strip("_").lower()


KINDS = {
    "trial_start": "trial_start", "trial_started": "trial_start", "start_trial": "trial_start",
    "trial_end": "trial_end", "trial_finished": "trial_end", "end_trial": "trial_end",
    "threat_spawn": "threat_spawn", "threat_appeared": "threat_spawn", "threat_created": "threat_spawn", "spawn": "threat_spawn",
    "threat_acquired": "threat_acquired", "first_acquisition": "threat_acquired", "first_acquired": "threat_acquired", "target_acquired": "threat_acquired",
    "shot": "shot", "shot_fired": "shot", "fire": "shot", "response_attempt": "shot",
    "threat_neutralized": "threat_neutralized", "neutralized": "threat_neutralized", "threat_destroyed": "threat_neutralized", "threat_killed": "threat_neutralized", "kill": "threat_neutralized",
    "deadline_expired": "deadline_expired", "threat_deadline": "deadline_expired",
    "damage": "damage", "player_damage": "damage", "attack": "attack", "camera_sample": "camera_sample", "camera": "camera_sample",
    "invalid_spawn": "technical_exclusion", "log_error": "technical_exclusion",
    "technical_exclusion": "technical_exclusion", "session_start": "session_start", "session_end": "session_end",
    "hit": "hit", "warning": "other", "haptic_request": "other", "haptic_result": "haptic_result",
    "calibration": "ignore", "block_start": "ignore", "block_end": "ignore", "survey": "ignore",
}


def first(obj: dict[str, Any], *keys: str) -> Any:
    # Both snake_case and Unity/C# camelCase are accepted.
    lookups = {normalized(k): v for k, v in obj.items()}
    for key in keys:
        candidate = lookups.get(normalized(key))
        if candidate is not None and candidate != "":
            return candidate
    return None


def number(value: Any) -> float | None:
    if isinstance(value, bool) or value is None:
        return None
    try:
        n = float(value)
        return n if math.isfinite(n) else None
    except (TypeError, ValueError):
        return None


def instant(obj: dict[str, Any]) -> float | None:
    for key in ("timestamp_ms", "time_ms", "t_ms", "elapsed_ms"):
        n = number(first(obj, key))
        if n is not None:
            return n / 1000
    value = first(obj, "trial_time", "trial_time_s", "timestamp_s", "time_s", "t", "timestamp", "time", "timestamp_utc")
    n = number(value)
    if n is not None:
        return n
    if isinstance(value, str):
        try:
            return datetime.fromisoformat(value.replace("Z", "+00:00")).timestamp()
        except ValueError:
            pass
    return None


def condition_name(obj: dict[str, Any]) -> str | None:
    code = first(obj, "condition", "condition_code", "condition_id")
    if code is None:
        return None
    upper = str(code).strip().upper()
    return upper if upper in CONDITIONS else None


def mode_name(value: Any) -> str | None:
    for mode in MODES:
        if str(value).lower() == mode.lower():
            return mode
    return None


def threat_id(obj: dict[str, Any]) -> str | None:
    v = first(obj, "threat_id", "target_id", "enemy_id")
    return str(v) if v is not None else None


def deadline(obj: dict[str, Any], spawn_time: float | None, trial_start: float | None) -> float | None:
    value = number(first(obj, "deadline_at_s", "deadline_time_s", "deadline_timestamp_s"))
    if value is not None:
        return value
    value = number(first(obj, "deadline_at_ms", "deadline_time_ms"))
    if value is not None:
        return value / 1000
    # Runtime trial_time is a shared trial-relative clock. deadline_time is likewise
    # trial-relative only when the writer declares it that way (deadline_time_s).
    value = number(first(obj, "deadline_at_trial_time", "deadline_trial_time", "deadline_time"))
    if value is not None:
        return value
    duration = number(first(obj, "deadline_after_s", "deadline_seconds", "time_limit_s"))
    if duration is not None and spawn_time is not None:
        return spawn_time + duration
    duration = number(first(obj, "deadline_after_ms", "time_limit_ms"))
    if duration is not None and spawn_time is not None:
        return spawn_time + duration / 1000
    # An explicit trial-relative deadline is distinct from an absolute timestamp.
    duration = number(first(obj, "deadline_from_trial_s"))
    if duration is not None and trial_start is not None:
        return trial_start + duration
    return None


@dataclass
class Threat:
    id: str
    zone: str | None = None
    spawn: float | None = None
    cutoff: float | None = None
    acquired: float | None = None
    neutralized: float | None = None
    shots: list[float] = field(default_factory=list)
    inferred_shots: int = 0
    deadlines_seen: int = 0


@dataclass
class Trial:
    participant: str
    session: str
    id: str
    mode: str | None = None
    condition: str | None = None
    started: float | None = None
    ended: float | None = None
    expected: int | None = None
    threats: dict[str, Threat] = field(default_factory=dict)
    shots: list[tuple[float | None, str | None]] = field(default_factory=list)
    shot_indices: set[tuple[str, int]] = field(default_factory=set)
    camera: list[tuple[float, float]] = field(default_factory=list)
    damage: float = 0.0
    damage_events: int = 0
    reported_yaw_path_deg: float | None = None
    reported_turn_reversals: int | None = None
    errors: list[str] = field(default_factory=list)
    metadata: dict[str, Any] = field(default_factory=dict)

    def error(self, code: str) -> None:
        if code not in self.errors:
            self.errors.append(code)


def capture_trial_metadata(trial: Trial, event: dict[str, Any], started: bool = False) -> None:
    mode = mode_name(first(event, "mode", "run_mode", "study_mode"))
    if mode:
        if trial.mode and mode != trial.mode:
            trial.error("conflicting_mode")
        else:
            trial.mode = mode
    code = condition_name(event)
    if code:
        if trial.condition and code != trial.condition:
            trial.error("conflicting_condition")
        else:
            trial.condition = code
    expected = number(first(event, "threat_count", "simultaneous_threat_count"))
    if expected is not None and expected.is_integer() and expected >= 0:
        trial.expected = int(expected)
    if started:
        trial.started = instant(event)
    for key in ("device_verified", "hardware_verified", "output_mode", "calibration_id", "seed", "schedule_seed", "block_id", "block_index", "trial_index", "order_index", "snapshot", "duration_s", "config_hash", "practice", "intensity_level"):
        val = first(event, key)
        if val is not None:
            trial.metadata[key] = val


def reconstruct(path: Path) -> tuple[list[Trial], list[dict[str, Any]]]:
    trials: dict[tuple[str, str, str], Trial] = {}
    global_errors: list[dict[str, Any]] = []
    sessions: dict[tuple[str, str], dict[str, Any]] = {}
    last_seq: dict[tuple[str, str], int] = {}
    if not path.is_file():
        raise FileNotFoundError(path)
    with path.open(encoding="utf-8-sig") as stream:
        for line_no, line in enumerate(stream, 1):
            if not line.strip():
                continue
            try:
                event = json.loads(line)
            except json.JSONDecodeError as exc:
                global_errors.append({"line": line_no, "code": "invalid_json", "detail": str(exc)})
                continue
            if not isinstance(event, dict):
                global_errors.append({"line": line_no, "code": "event_not_object"})
                continue
            nested = first(event, "payload")
            if isinstance(nested, str):
                try:
                    nested = json.loads(nested)
                except json.JSONDecodeError:
                    global_errors.append({"line": line_no, "code": "invalid_payload", "detail": "Expected object JSON"})
            if isinstance(nested, dict):
                event = {**nested, **event}
            raw_kind = first(event, "event", "event_type", "type", "kind")
            kind = KINDS.get(normalized(raw_kind))
            if not kind:
                global_errors.append({"line": line_no, "code": "unknown_event", "detail": str(raw_kind)})
                continue
            participant = str(first(event, "participant_id", "participant") or "UNKNOWN")
            session = str(first(event, "session_id", "session") or "UNKNOWN")
            scope = (participant, session)
            sequence = number(first(event, "sequence_number"))
            seq_error = None
            if sequence is not None and sequence.is_integer() and sequence > 0:
                prior = last_seq.get(scope)
                if prior is not None and int(sequence) > prior + 1:
                    seq_error = "sequence_gap"
                if prior is not None and int(sequence) <= prior:
                    seq_error = "non_monotonic_sequence"
                last_seq[scope] = int(sequence)
            if kind == "session_start":
                sessions[scope] = dict(event)
                if seq_error:
                    global_errors.append({"line": line_no, "code": seq_error})
                continue
            if kind == "session_end":
                if seq_error:
                    global_errors.append({"line": line_no, "code": seq_error})
                continue
            if kind == "ignore":
                if seq_error:
                    global_errors.append({"line": line_no, "code": seq_error})
                continue
            tid = first(event, "trial_id", "trial_index", "trial")
            if tid is None:
                global_errors.append({"line": line_no, "code": "missing_trial_id", "detail": kind})
                continue
            key = (*scope, str(tid))
            trial = trials.setdefault(key, Trial(participant, session, str(tid)))
            if seq_error:
                trial.error(seq_error)
            if scope in sessions:
                session_data = sessions[scope]
                capture_trial_metadata(trial, session_data)
            capture_trial_metadata(trial, event, started=kind == "trial_start")
            t = instant(event)
            if kind == "trial_start":
                if trial.started is not None and trial.metadata.get("_start_count"):
                    trial.error("duplicate_trial_start")
                trial.metadata["_start_count"] = trial.metadata.get("_start_count", 0) + 1
                declared = first(event, "threats")
                if isinstance(declared, list):
                    for record in declared:
                        if isinstance(record, dict):
                            add_spawn(trial, record, t)
            elif kind == "trial_end":
                trial.ended = t
                path_value = number(first(event, "yaw_path_deg"))
                reversals = number(first(event, "turn_reversals"))
                trial.reported_yaw_path_deg = path_value
                if reversals is not None and reversals.is_integer() and reversals >= 0:
                    trial.reported_turn_reversals = int(reversals)
                if first(event, "validity", "valid") is False:
                    trial.error("runtime_marked_invalid")
                if normalized(first(event, "validity")) in {"invalid", "excluded", "failed"}:
                    trial.error("runtime_marked_invalid")
            elif kind == "threat_spawn":
                add_spawn(trial, event, t)
            elif kind in ("threat_acquired", "threat_neutralized", "deadline_expired"):
                ident = threat_id(event)
                if ident is None:
                    trial.error("event_missing_threat_id")
                    continue
                threat = trial.threats.setdefault(ident, Threat(ident))
                if kind == "threat_acquired":
                    if t is not None and (threat.acquired is None or t < threat.acquired):
                        threat.acquired = t
                elif kind == "threat_neutralized":
                    if t is not None and (threat.neutralized is None or t < threat.neutralized):
                        threat.neutralized = t
                else:
                    threat.deadlines_seen += 1
            elif kind == "shot":
                ident = threat_id(event)
                attribution = normalized(first(event, "attribution_method"))
                # Some writers emit both shot and response_attempt for one trigger.
                shot_index = number(first(event, "shot_index"))
                index_key = (str(ident), int(shot_index)) if shot_index is not None and shot_index.is_integer() and shot_index > 0 else None
                if index_key is not None:
                    if index_key in trial.shot_indices:
                        continue
                    trial.shot_indices.add(index_key)
                if any(sid == ident and ts is not None and t is not None and abs(ts-t) < .001 for ts, sid in trial.shots):
                    continue
                trial.shots.append((t, ident))
                if len(trial.shots) == 1:
                    trial.metadata["_first_shot_attribution"] = attribution or None
                if ident is not None:
                    th = trial.threats.setdefault(ident, Threat(ident))
                    th.shots.append(t if t is not None else float("nan"))
                    if attribution == "angle_proximity_inference":
                        th.inferred_shots += 1
            elif kind == "camera_sample":
                yaw = number(first(event, "camera_yaw", "yaw_deg", "camera_yaw_deg", "yaw"))
                if yaw is None or t is None:
                    trial.error("invalid_camera_sample")
                else:
                    trial.camera.append((t, yaw))
            elif kind == "damage":
                amount = number(first(event, "amount", "damage", "damage_amount"))
                trial.damage += amount if amount is not None else 1.0
                trial.damage_events += 1
            elif kind == "attack":
                # An attack is not necessarily damage: explicit damage events are authoritative.
                pass
            elif kind == "haptic_result":
                status = normalized(first(event, "output_status", "status", "result", "validity"))
                # A Mock in Demo is expected; real output problems invalidate Main.
                bad = status in {"failed", "error", "blocked", "disconnected", "timed_out", "timeout"}
                if trial.mode == "Main" and status != "not_requested" and (first(event, "success", "delivered") is False or bad):
                    trial.error("haptic_output_failure")
            elif kind == "technical_exclusion":
                trial.error("reported_technical_exclusion")
                reason = first(event, "reason", "code")
                if reason:
                    trial.error("runtime_" + normalized(reason))
    return list(trials.values()), global_errors


def add_spawn(trial: Trial, record: dict[str, Any], time: float | None) -> None:
    ident = threat_id(record)
    if ident is None:
        trial.error("spawn_missing_threat_id")
        return
    threat = trial.threats.setdefault(ident, Threat(ident))
    spawn = number(first(record, "spawn_time_s", "spawn_at_s"))
    if spawn is None:
        spawn_ms = number(first(record, "spawn_time_ms"))
        spawn = spawn_ms / 1000 if spawn_ms is not None else time
    if threat.spawn is not None and spawn is not None and abs(spawn - threat.spawn) > 1e-6:
        trial.error("conflicting_spawn_time")
    else:
        threat.spawn = spawn
    zone = first(record, "zone", "direction_zone", "haptic_zone")
    if zone is not None:
        threat.zone = str(zone)
    limit = deadline(record, threat.spawn, trial.started)
    if limit is not None:
        if threat.cutoff is not None and abs(threat.cutoff - limit) > 1e-6:
            trial.error("conflicting_deadline")
        else:
            threat.cutoff = limit


def rotation(samples: list[tuple[float, float]]) -> tuple[float | None, int | None]:
    if len(samples) < 2:
        return None, None
    samples = sorted(samples)
    diffs = [((b[1] - a[1] + 180) % 360) - 180 for a, b in zip(samples, samples[1:])]
    path = sum(abs(x) for x in diffs)
    # Treat <=1 degree variations as jitter rather than deliberate reversals.
    signs = [1 if d > 1 else -1 if d < -1 else 0 for d in diffs]
    nonzero = [x for x in signs if x]
    return round(path, 6), sum(a != b for a, b in zip(nonzero, nonzero[1:]))


def evaluate(trials: list[Trial]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    threat_rows: list[dict[str, Any]] = []
    trial_rows: list[dict[str, Any]] = []
    for trial in trials:
        errors = list(trial.errors)
        def issue(code: str) -> None:
            if code not in errors:
                errors.append(code)
        if trial.started is None:
            issue("missing_trial_start")
        if trial.ended is None:
            issue("missing_trial_end")
        if trial.mode is None:
            issue("missing_mode")
        if trial.condition is None:
            issue("missing_condition")
        if trial.metadata.get("practice") is True:
            issue("practice_trial")
        if trial.condition:
            expected_by_condition = 1 if trial.condition.startswith("S") else 3
            if len(trial.threats) != expected_by_condition:
                issue("condition_threat_count_mismatch")
        if trial.expected is not None and trial.expected != len(trial.threats):
            issue("declared_threat_count_mismatch")
        if trial.ended is not None and trial.started is not None and trial.ended < trial.started:
            issue("end_before_start")
        if len(trial.threats) == 3:
            zones = [normalized(t.zone) for t in trial.threats.values()]
            if len(set(zones)) < 3 or any(not z for z in zones):
                issue("multi_zone_not_unique")
            spawns = [t.spawn for t in trial.threats.values()]
            if all(s is not None for s in spawns) and max(spawns) - min(spawns) > .05:
                issue("non_simultaneous_spawns")
        if trial.mode == "Main":
            verified = trial.metadata.get("device_verified", trial.metadata.get("hardware_verified"))
            output = normalized(trial.metadata.get("output_mode"))
            if verified is not True or output in {"mock", "none", "simulation"}:
                issue("main_device_unverified")
        for th in trial.threats.values():
            if th.spawn is None:
                issue("missing_spawn_time")
            if th.cutoff is None:
                issue("missing_deadline")
            if th.cutoff is not None and th.spawn is not None and th.cutoff <= th.spawn:
                issue("invalid_deadline")
            if th.neutralized is not None and th.spawn is not None and th.neutralized < th.spawn:
                issue("neutralized_before_spawn")
            if th.acquired is not None and th.spawn is not None and th.acquired < th.spawn:
                issue("acquired_before_spawn")
            if th.cutoff is not None and trial.ended is not None and trial.ended < th.cutoff and th.neutralized is None:
                issue("trial_ends_before_deadline")
        exclusion = bool(errors)
        # Rotation cost is defined up to FIRST target acquisition. If no target
        # was acquired, follow the complete trial. Runtime's aggregate yaw_path
        # is preserved separately because its measurement window is unknown.
        acquisitions = [th.acquired for th in trial.threats.values() if th.acquired is not None]
        first_acquisition = min(acquisitions) if acquisitions else None
        window = [(ts, yaw) for ts, yaw in trial.camera if first_acquisition is None or ts <= first_acquisition]
        path, reversals = rotation(window)
        warning_origin = 0.0  # Unity trial_time is relative to warning onset.
        first_latency = first_acquisition - warning_origin if first_acquisition is not None else None
        shots_sorted = sorted(trial.shots, key=lambda v: (v[0] is None, v[0] if v[0] is not None else 0))
        first_target = shots_sorted[0][1] if shots_sorted else None
        complete = 0
        failures = 0
        no_responses = 0
        for th in sorted(trial.threats.values(), key=lambda x: x.id):
            attempted = any(math.isfinite(t) and (th.cutoff is None or t < th.cutoff) and
                            (th.spawn is None or t >= th.spawn) for t in th.shots)
            missed = th.cutoff is None or th.neutralized is None or th.neutralized >= th.cutoff
            if not exclusion:
                complete += 1
                failures += int(missed)
                no_responses += int(not attempted)
            latency = None if th.acquired is None or th.spawn is None else round(th.acquired - th.spawn, 6)
            threat_rows.append({
                "participant_id": trial.participant, "session_id": trial.session, "trial_id": trial.id,
                "mode": trial.mode, "condition": trial.condition, "threat_id": th.id, "zone": th.zone,
                "spawn_time_s": th.spawn, "deadline_time_s": th.cutoff, "acquisition_time_s": th.acquired,
                "first_acquisition_latency_s": latency, "neutralization_time_s": th.neutralized,
                "failure": None if exclusion else int(missed), "no_response": None if exclusion else int(not attempted),
                "attempted": int(attempted), "shot_count": len(th.shots),
                "inferred_near_miss_shots": th.inferred_shots,
                "first_shot_target": first_target, "damage_total": trial.damage,
                "technical_excluded": int(exclusion), "exclusion_reasons": ";".join(errors),
            })
        trial_rows.append({
            "participant_id": trial.participant, "session_id": trial.session, "trial_id": trial.id,
            "mode": trial.mode, "condition": trial.condition, "threat_count": len(trial.threats),
            "declared_threat_count": trial.expected, "trial_start_s": trial.started, "trial_end_s": trial.ended,
            "eligible_threats": complete, "failures": failures if not exclusion and complete else None,
            "failure_rate": failures / complete if not exclusion and complete else None,
            "no_responses": no_responses if not exclusion and complete else None,
            "first_shot_target": first_target, "first_shot_time_s": shots_sorted[0][0] if shots_sorted else None,
            "first_shot_attribution": trial.metadata.get("_first_shot_attribution"),
            "first_acquisition_latency_s": first_latency,
            "camera_path_deg": path, "camera_reversals": reversals,
            "reported_yaw_path_deg": trial.reported_yaw_path_deg,
            "reported_turn_reversals": trial.reported_turn_reversals,
            "camera_metric_window": "warning_to_first_acquisition" if first_acquisition is not None else "warning_to_trial_end",
            "damage_total": trial.damage, "damage_events": trial.damage_events,
            "technical_excluded": int(exclusion), "exclusion_reasons": ";".join(errors),
            "block_id": trial.metadata.get("block_id", trial.metadata.get("block_index")), "seed": trial.metadata.get("seed", trial.metadata.get("schedule_seed")),
            "calibration_id": trial.metadata.get("calibration_id"),
            "config_hash": trial.metadata.get("config_hash"),
            "snapshot_json": json.dumps(trial.metadata.get("snapshot"), ensure_ascii=False, sort_keys=True) if trial.metadata.get("snapshot") is not None else None,
        })
    return threat_rows, trial_rows


def summarize(threat_rows: list[dict[str, Any]], trial_rows: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    groups: dict[tuple[str, str, str], list[dict[str, Any]]] = defaultdict(list)
    trial_counts: dict[tuple[str, str, str], set[tuple[str, str, str]]] = defaultdict(set)
    for row in threat_rows:
        if row["failure"] is None or row["condition"] not in CONDITIONS or row["mode"] not in MODES:
            continue
        for participant in ("ALL", row["participant_id"]):
            key = (row["mode"], participant, row["condition"])
            groups[key].append(row)
            trial_counts[key].add((row["participant_id"], row["session_id"], row["trial_id"]))
    summaries = []
    for (mode, participant, code), rows in sorted(groups.items()):
        count = len(rows)
        summaries.append({
            "mode": mode, "participant_id": participant, "condition": code,
            "participants": len({r["participant_id"] for r in rows}), "trials": len(trial_counts[(mode, participant, code)]),
            "threats": count, "failures": sum(r["failure"] for r in rows),
            "failure_probability": sum(r["failure"] for r in rows) / count,
            "no_response_probability": sum(r["no_response"] for r in rows) / count,
            "mean_acquisition_latency_s": mean([r["first_acquisition_latency_s"] for r in rows if r["first_acquisition_latency_s"] is not None]) if any(r["first_acquisition_latency_s"] is not None for r in rows) else None,
        })

    lookup = {(r["mode"], r["participant_id"], r["condition"]): r["failure_probability"] for r in summaries}
    contrasts: dict[str, Any] = {}
    for mode in MODES:
        all_codes = {code: lookup.get((mode, "ALL", code)) for code in CONDITIONS}
        def contrast(codes: list[str], weights: list[float]) -> float | None:
            vals = [all_codes[c] for c in codes]
            return round(sum(w * v for w, v in zip(weights, vals)), 9) if all(v is not None for v in vals) else None
        paired = []
        participants = sorted({r["participant_id"] for r in summaries if r["mode"] == mode and r["participant_id"] != "ALL"})
        for participant in participants:
            values = [lookup.get((mode, participant, c)) for c in ("MH", "ML", "SH", "SL")]
            if all(v is not None for v in values):
                paired.append(sum(w * v for w, v in zip((1, -1, -1, 1), values)))
        contrasts[mode] = {
            "observed_failure_probability": all_codes,
            "primary_MH_minus_ML_minus_SH_plus_SL": contrast(["MH", "ML", "SH", "SL"], [1, -1, -1, 1]),
            "secondary_MH_minus_M0": contrast(["MH", "M0"], [1, -1]),
            "paired_participant_primary_mean": round(mean(paired), 9) if paired else None,
            "paired_participant_count": len(paired),
            "warnings": ["Descriptive only; threats within trials and trials within participants are dependent.",
                         "Participant-paired primary requires MH, ML, SH and SL for each participant; no p-values or CIs computed."]
                        + (["One or more primary cells unavailable."] if contrast(["MH", "ML", "SH", "SL"], [1, -1, -1, 1]) is None else []),
        }
    contrasts["technical_exclusions"] = sum(r["technical_excluded"] for r in trial_rows)
    contrasts["no_threat_trials"] = sum(r["threat_count"] == 0 for r in trial_rows)
    return summaries, contrasts


def write_csv(path: Path, rows: list[dict[str, Any]], fields: list[str]) -> None:
    with path.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Replay Unity research JSONL to per-threat and trial CSV.")
    parser.add_argument("input", type=Path, help="Append-only JSONL event log")
    parser.add_argument("--out", type=Path, required=True, help="Output directory")
    parser.add_argument("--mode", choices=[*MODES, "all"], default="Main", help="Default Main; Demo/Pilot analyzed separately")
    parser.add_argument("--session-json", type=Path, help="Session metadata/snapshot; default: input's sibling session.json if present")
    args = parser.parse_args(argv)
    try:
        trials, log_errors = reconstruct(args.input)
    except (OSError, UnicodeError) as exc:
        parser.error(str(exc))
    metadata_file = args.session_json or args.input.with_name("session.json")
    session_hash = None
    config_hash = None
    session_snapshot = None
    if metadata_file.is_file():
        try:
            metadata_bytes = metadata_file.read_bytes()
            session_hash = hashlib.sha256(metadata_bytes).hexdigest()
            parsed = json.loads(metadata_bytes.decode("utf-8-sig"))
            if isinstance(parsed, dict):
                config_hash = first(parsed, "config_hash")
                session_snapshot = parsed
            else:
                log_errors.append({"line": 0, "code": "invalid_session_json", "detail": "Root is not an object"})
        except (OSError, UnicodeError, json.JSONDecodeError) as exc:
            log_errors.append({"line": 0, "code": "invalid_session_json", "detail": str(exc)})
    elif args.session_json is not None:
        log_errors.append({"line": 0, "code": "missing_session_json", "detail": str(args.session_json)})
    if session_snapshot is not None:
        snapshot_session = first(session_snapshot, "session_id")
        snapshot_participant = first(session_snapshot, "participant_id")
        for trial in trials:
            if snapshot_session is not None and str(snapshot_session) != trial.session:
                trial.error("session_snapshot_id_mismatch")
            if snapshot_participant is not None and str(snapshot_participant) != trial.participant:
                trial.error("participant_snapshot_id_mismatch")
            trial.metadata.setdefault("snapshot", session_snapshot)
            trial.metadata.setdefault("config_hash", config_hash)
            if trial.mode is None:
                trial.mode = mode_name(first(session_snapshot, "mode"))
    if log_errors:
        # A damaged/missing log line cannot always be assigned to one trial.
        # Conservative default excludes every trial in this source file.
        for trial in trials:
            trial.error("untrusted_event_log")
    if args.mode != "all":
        trials = [t for t in trials if t.mode == args.mode]
    threat_rows, trial_rows = evaluate(trials)
    condition_rows, contrasts = summarize(threat_rows, trial_rows)
    args.out.mkdir(parents=True, exist_ok=True)
    write_csv(args.out / "per_threat.csv", threat_rows, [
        "participant_id", "session_id", "trial_id", "mode", "condition", "threat_id", "zone", "spawn_time_s", "deadline_time_s", "acquisition_time_s", "first_acquisition_latency_s", "neutralization_time_s", "failure", "no_response", "attempted", "shot_count", "inferred_near_miss_shots", "first_shot_target", "damage_total", "technical_excluded", "exclusion_reasons",
    ])
    write_csv(args.out / "per_trial.csv", trial_rows, [
        "participant_id", "session_id", "trial_id", "mode", "condition", "threat_count", "declared_threat_count", "trial_start_s", "trial_end_s", "eligible_threats", "failures", "failure_rate", "no_responses", "first_shot_target", "first_shot_time_s", "first_shot_attribution", "first_acquisition_latency_s", "camera_path_deg", "camera_reversals", "reported_yaw_path_deg", "reported_turn_reversals", "camera_metric_window", "damage_total", "damage_events", "technical_excluded", "exclusion_reasons", "block_id", "seed", "calibration_id", "config_hash", "snapshot_json",
    ])
    write_csv(args.out / "by_condition.csv", condition_rows, ["mode", "participant_id", "condition", "participants", "trials", "threats", "failures", "failure_probability", "no_response_probability", "mean_acquisition_latency_s"])
    (args.out / "contrasts.json").write_text(json.dumps(contrasts, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (args.out / "parse_warnings.json").write_text(json.dumps(log_errors, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    provenance = {
        "input_file": str(args.input), "input_sha256": hashlib.sha256(args.input.read_bytes()).hexdigest(),
        "session_json_file": str(metadata_file) if metadata_file.is_file() else None,
        "session_json_sha256": session_hash, "recorded_config_hash": config_hash,
        "selected_mode": args.mode,
        "analysis": "stdlib replay.py; warning-relative seconds; deadline equality=failure; descriptive only",
    }
    (args.out / "provenance.json").write_text(json.dumps(provenance, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"mode={args.mode} trials={len(trial_rows)} threats={len(threat_rows)} excluded={contrasts['technical_exclusions']} parse_warnings={len(log_errors)}")
    if not trial_rows:
        print("WARNING: No trials in selected mode.", file=sys.stderr)
    if log_errors:
        print("WARNING: Some input lines were not replayed. Inspect parse_warnings.json.", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
