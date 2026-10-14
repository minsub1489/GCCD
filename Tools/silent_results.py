"""Recover or summarize actual Silent Study records. Standard library only.

python3 Tools/silent_results.py analyze /path/to/trials.csv [more.csv ...] --output analysis.json
python3 Tools/silent_results.py recover /path/to/journal.jsonl --output recovered.csv
Recovery never edits an original journal/CSV. Incomplete episodes remain invalid.
"""
import argparse
import csv
import json
import pathlib
import statistics
from collections import defaultdict


def rate(rows, field):
    return 100 * sum(r.get(field) == "true" for r in rows) / len(rows) if rows else None


def mean(rows, field):
    values = [float(r[field]) for r in rows if r.get(field, "") != ""]
    return statistics.mean(values) if values else None


def summarize(rows):
    threats = [r for r in rows if r["IsThreat"] == "true"]
    catches = [r for r in rows if r["IsThreat"] == "false"]
    return {
        "ValidTrials": len(rows), "ThreatTrials": len(threats), "CatchTrials": len(catches),
        "ObjectiveRatePct": rate(rows, "ObjectiveSuccess"), "DetectionAccuracyPct": rate(threats, "DetectionSuccess"),
        "TypeAccuracyPct": rate(threats, "IsCorrect"), "AvoidanceRatePct": rate(threats, "AvoidanceSuccess"),
        "FalseAlarmRatePct": 100 * sum(r["DetectionResponse"] == "Threat" for r in catches) / len(catches) if catches else None,
        "CatchResponseCount": sum(r["DetectionResponse"] != "" for r in catches),
        "ThreatDetectionResponseCount": sum(r["DetectionResponse"] != "" for r in threats),
        "TypeResponseCount": sum(r["TypeResponse"] != "" for r in threats),
        "DirectionResponseCount": sum(r["DirectionResponse"] != "" for r in threats),
        "CorrectDefenseResponseCount": sum(r["ReactionTimeMs"] != "" for r in threats),
        "ObjectiveSuccessCount": sum(r["ObjectiveSuccess"] == "true" for r in rows),
        "MeanDirectionErrorDeg": mean(threats, "DirectionErrorDeg"),
        "MeanCorrectDefenseTimeMs": mean(threats, "ReactionTimeMs"),
        "MeanCompletionTimeSec": mean(rows, "CompletionTimeSec"),
    }


def analyze(paths):
    unique = {}
    for path in paths:
        with pathlib.Path(path).open(encoding="utf-8-sig", newline="") as source:
            for row in csv.DictReader(source):
                if row.get("SchemaVersion") != "1" or row.get("RowKind") != "final":
                    raise ValueError(f"Unexpected schema or non-final row in {path}")
                key = row["SessionID"], row["TrialID"]
                if key in unique and unique[key] != row:
                    raise ValueError(f"Conflicting duplicate trial {key}")
                unique[key] = row
    excluded = defaultdict(int)
    grouped = defaultdict(list)
    for row in unique.values():
        if row.get("Purpose", "Measurement") not in ("Measurement", "Practice"):
            excluded[row["Purpose"]] += 1
        elif row["Practice"] == "true":
            excluded["practice"] += 1
        elif row["Valid"] != "true" or row["Status"] == "Aborted":
            excluded["invalid_or_aborted"] += 1
        elif row["Condition"] != "AudioOnly" and row["OutputMode"] != "BHaptics":
            raise ValueError("A simulated haptic trial is incorrectly marked valid measurement")
        else:
            grouped[row["ParticipantID"], row["Condition"], row["ScenarioID"]].append(row)
    summaries = []
    for (participant, condition, scenario), rows in sorted(grouped.items()):
        summaries.append({"ParticipantID": participant, "Condition": condition, "ScenarioID": scenario,
                          "ConfigHashes": sorted(set(r["ConfigHash"] for r in rows)),
                          "ControlHashes": sorted(set(r.get("ControlHash", "") for r in rows)), **summarize(rows)})
    return {"SchemaVersion": 1, "Excluded": dict(excluded), "Participants": summaries,
            "Interpretation": "RT, direction error, and completion means require their response/success denominators. "
                              "Use participants as the unit for paired comparisons; do not pool trials as independent people. "
                              "Different ControlHashes require checking controlled settings/seed before combining. ConfigHash naturally varies by condition."}


def recover(path):
    rows = {}
    truncated = 0
    raw_lines = pathlib.Path(path).read_text(encoding="utf-8").splitlines()
    for index, line in enumerate(raw_lines):
        try:
            row = json.loads(line)
        except json.JSONDecodeError:
            if index != len(raw_lines) - 1:
                raise ValueError("Corrupt journal before final line")
            truncated += 1
            continue
        if row.get("SchemaVersion") != "1":
            raise ValueError("Unsupported journal schema")
        rows[row["SessionID"], row["TrialID"]] = row
    for row in rows.values():
        if row["RowKind"] != "final":
            row.update(RowKind="final", Valid="false", Status="Aborted", AbortReason="Recovered incomplete journal after process loss")
            responses = [row.get(f, "") for f in ("DetectionResponse", "TypeResponse", "DirectionResponse", "CorrectDefense")]
            row["PlayerResponse"] = ";".join(s for s in responses if s) or "NoResponse"
            # Outcomes and missing timing remain blank: a crash does not establish failure or RT.
    return list(rows.values()), truncated


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("analyze", "recover"))
    parser.add_argument("paths", nargs="+")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    output = pathlib.Path(args.output)
    if output.exists():
        parser.error("Output exists; choose a new file to preserve existing data")
    if args.action == "analyze":
        result = analyze(args.paths)
        output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    else:
        if len(args.paths) != 1:
            parser.error("Recover takes exactly one journal")
        rows, truncated = recover(args.paths[0])
        if not rows:
            parser.error("Journal has no recoverable rows")
        with output.open("x", encoding="utf-8", newline="") as target:
            writer = csv.DictWriter(target, fieldnames=list(rows[0]))
            writer.writeheader()
            writer.writerows(rows)
        print(f"Recovered {len(rows)} episodes; ignored {truncated} incomplete final line(s)")
    print(output.resolve())


if __name__ == "__main__":
    main()
