#!/usr/bin/env python3
"""Read-only FM Atlas adapter for the MIT-licensed fmsave package.

Usage: py -3 Export-FMAtlas-Fmsave.py <copy-of-career-save> <output.json>
The save is opened read-only. This adapter never modifies the save or connects to a network.
"""
from __future__ import annotations
import dataclasses
import datetime as dt
import enum
import json
import pathlib
import sys
import traceback
from typing import Any


def plain(value: Any, depth: int = 0) -> Any:
    if depth > 12:
        return str(value)
    if value is None or isinstance(value, (str, int, float, bool)):
        return value
    if isinstance(value, (dt.date, dt.datetime)):
        return value.isoformat()
    if isinstance(value, enum.Enum):
        return value.value
    if dataclasses.is_dataclass(value):
        return {f.name: plain(getattr(value, f.name), depth + 1) for f in dataclasses.fields(value)}
    if isinstance(value, dict):
        return {str(k): plain(v, depth + 1) for k, v in value.items()}
    if isinstance(value, (list, tuple, set)):
        return [plain(v, depth + 1) for v in value]
    if hasattr(value, "to_dict"):
        try:
            return plain(value.to_dict(), depth + 1)
        except Exception:
            pass
    if hasattr(value, "__dict__"):
        try:
            return {k: plain(v, depth + 1) for k, v in vars(value).items() if not k.startswith("_")}
        except Exception:
            pass
    slots = getattr(type(value), "__slots__", ())
    if slots:
        result = {}
        for key in slots:
            if isinstance(key, str) and not key.startswith("_"):
                try:
                    result[key] = plain(getattr(value, key), depth + 1)
                except Exception:
                    continue
        if result:
            return result
    return str(value)


def first(record: dict, *keys: str):
    for key in keys:
        if key in record and record[key] is not None:
            return record[key]
    return None


def nested(record: dict, *paths: str):
    for path in paths:
        current: Any = record
        try:
            for part in path.split("."):
                current = current[part] if isinstance(current, dict) else getattr(current, part)
            if current is not None:
                return current
        except Exception:
            continue
    return None


def value_label(value: Any) -> Any:
    """Return a display label for fmsave CodedValue wrappers without dropping raw data."""
    if isinstance(value, dict):
        for key in ("label_text", "label", "name", "display_name", "common_name", "full_name", "short_name"):
            candidate = value.get(key)
            if candidate is None:
                continue
            if isinstance(candidate, dict):
                candidate = candidate.get("value") or candidate.get("name") or candidate.get("raw")
            if isinstance(candidate, str):
                return candidate.replace("_", " ").title()
            if isinstance(candidate, enum.Enum):
                return candidate.name.replace("_", " ").title()
            if candidate is not None:
                return str(candidate)
        return None
    if isinstance(value, (list, tuple)):
        labels = [value_label(v) for v in value]
        return [v for v in labels if v is not None]
    if isinstance(value, enum.Enum):
        return value.name.replace("_", " ").title()
    return value


def flatten_numeric(obj: Any, prefix: str = "") -> dict[str, float]:
    result: dict[str, float] = {}
    if not isinstance(obj, dict):
        return result
    for key, value in obj.items():
        label = f"{prefix}.{key}" if prefix else str(key)
        if isinstance(value, dict):
            result.update(flatten_numeric(value, label))
        elif isinstance(value, bool):
            continue
        elif isinstance(value, (int, float)):
            result[label] = float(value)
    return result


def records_for(save: Any, name: str, warnings: list[str]) -> list[dict]:
    # Progress is written to stderr so the desktop app can display it while stdout
    # remains reserved for the final machine-readable summary.
    print(f"Reading save table: {name}", file=sys.stderr, flush=True)
    try:
        reader = getattr(save, name, None)
        if not callable(reader):
            message = f"Reader '{name}' is not exposed by this fmsave version."
            warnings.append(message)
            print(message, file=sys.stderr, flush=True)
            return []
        table = reader()
        rows = table.to_dicts() if hasattr(table, "to_dicts") else list(table)
        materialised = [plain(row) for row in rows]
        print(f"Read {len(materialised):,} rows from {name}", file=sys.stderr, flush=True)
        return materialised
    except Exception as exc:
        message = f"Reader '{name}' did not complete: {type(exc).__name__}: {exc}"
        warnings.append(message)
        print(message, file=sys.stderr, flush=True)
        return []


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: Export-FMAtlas-Fmsave.py <career-save-copy> <output.json>", file=sys.stderr)
        return 2
    save_path = pathlib.Path(sys.argv[1]).expanduser().resolve()
    output_path = pathlib.Path(sys.argv[2]).expanduser().resolve()
    if not save_path.is_file():
        print(f"Save file not found: {save_path}", file=sys.stderr)
        return 2
    try:
        import fmsave
    except Exception as exc:
        print("fmsave is not installed for this Python interpreter. Run Setup-FMAtlas-Save-Bridge.ps1.", file=sys.stderr)
        print(f"Details: {type(exc).__name__}: {exc}", file=sys.stderr)
        return 3

    warnings: list[str] = []
    try:
        with fmsave.open(str(save_path)) as save:
            # Reader names below match the currently published fmsave API (30 tables).
            table_names = [
                "players", "contracts", "suspensions", "staff", "staff_lists",
                "clubs", "managed_clubs", "finances", "sponsorships", "facilities",
                "stadiums", "affiliates", "job_vacancies", "stages", "competitions",
                "fixtures", "league_tables", "competition_rules", "transfer_windows",
                "player_match_stats", "player_season_stats", "player_season_history",
                "nations", "injury_types", "injuries", "training", "mentoring",
                "tactics", "set_pieces", "selections"
            ]
            # Capture only non-personal save metadata. Do not export summary_strings,
            # which can contain the human manager's name and other identifying text.
            info = getattr(save, "info", None)
            save_info = {
                "game": getattr(info, "game", None),
                "build": getattr(info, "build", None),
                "build_number": getattr(info, "build_number", None),
                "known_build": getattr(info, "known_build", None),
                "db_version": getattr(info, "db_version", None),
                "game_date": plain(getattr(info, "game_date", None)),
                "time_slot": getattr(info, "time_slot", None),
                "field_meanings_confirmed": False if str(getattr(info, "game", "")).upper() == "FM27" else None,
            }
            validation = None
            try:
                report = fmsave.validate_save(save)
                validation = {
                    "fmsave_version": getattr(report, "fmsave_version", None),
                    "game": getattr(report, "game", None),
                    "build": getattr(report, "build", None),
                    "known_build": getattr(report, "known_build", None),
                    "readers": [
                        {
                            "reader": getattr(reader, "reader", "unknown"),
                            "status": plain(getattr(reader, "status", "unknown")),
                            "record_count": getattr(reader, "record_count", None),
                            "gates": plain(getattr(reader, "gates", {})),
                            "coverage": plain(getattr(reader, "coverage", {})),
                            "anomalies": plain(getattr(reader, "anomalies", ())),
                        }
                        for reader in getattr(report, "readers", ())
                    ],
                    "field_statuses": plain(getattr(report, "field_statuses", {})),
                    "experimental_counts": plain(getattr(report, "experimental_counts", {})),
                }
                failures = [f"{r['reader']}: {r['status']}" for r in validation["readers"] if str(r["status"]).lower() != "ok"]
                if failures:
                    warnings.append("Reader validation reported incomplete results: " + ", ".join(failures))
                if save_info["known_build"] is False:
                    warnings.append("This FM build is not registered by fmsave; imported fields require additional verification.")
                if save_info["field_meanings_confirmed"] is False:
                    warnings.append("FM27 field meanings are unconfirmed by fmsave; treat imported values as a research snapshot, not verified in-game values.")
            except Exception as validation_exc:
                warnings.append(f"Reader validation report unavailable: {type(validation_exc).__name__}: {validation_exc}")
            tables = {name: records_for(save, name, warnings) for name in table_names}
    except Exception as exc:
        print(f"fmsave could not read this save: {type(exc).__name__}: {exc}", file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        return 4

    players = []
    for item in tables.get("players", []):
        if not isinstance(item, dict):
            continue
        ability = first(item, "ability") or {}
        attributes = first(item, "attributes") or {}
        personality = first(item, "personality") or {}
        contract = first(item, "contract") or {}
        # fmsave 0.9.x Player model: birth_date, club_name, nation_id,
        # natural_positions, accomplished_positions, and a Positions value object.
        natural_positions = first(item, "natural_positions") or []
        accomplished_positions = first(item, "accomplished_positions") or []
        if isinstance(natural_positions, (list, tuple)):
            primary_position = natural_positions[0] if natural_positions else None
        else:
            primary_position = None
        if primary_position is None and isinstance(accomplished_positions, (list, tuple)) and accomplished_positions:
            primary_position = accomplished_positions[0]
        left_foot = first(item, "left_foot")
        right_foot = first(item, "right_foot")
        foot_strength = None
        if left_foot is not None or right_foot is not None:
            foot_strength = f"Left {left_foot if left_foot is not None else '—'}/20 · Right {right_foot if right_foot is not None else '—'}/20"
        save_info = save_info or {}
        db_namespace = save_info.get("db_version") or "unknown-db"
        game_namespace = save_info.get("game") or "FM"
        unique_id = first(item, "unique_id")
        save_uid = first(item, "uid")
        game_date = save_info.get("game_date") or "unknown-date"
        atlas_source_id = f"FMSAVE:{game_namespace}:{db_namespace}:{unique_id}" if unique_id is not None else f"FMSAVE:UNRESOLVED:{game_namespace}:{db_namespace}:{game_date}:{save_uid}"
        players.append({
            "uid": save_uid,
            "unique_id": unique_id,
            "atlas_source_id": atlas_source_id,
            "name": first(item, "name", "full_name", "common_name"),
            "age": first(item, "age"),
            "date_of_birth": first(item, "birth_date", "date_of_birth", "dob"),
            # Nation ID is save-local; fmsave's nations table has no display names.
            # Keep the nationality name blank rather than displaying a numeric ID as a country.
            "nationality": None,
            "nation_id": first(item, "nation_id"),
            "club_uid": first(item, "club_uid"),
            "club_name": first(item, "club_name"),
            # No current-league name is exposed on this record; do not infer it from club metadata.
            "league": None,
            "position": primary_position,
            "natural_positions": plain(natural_positions),
            "accomplished_positions": plain(accomplished_positions),
            "preferred_foot": foot_strength,
            "left_foot": left_foot,
            "right_foot": right_foot,
            "height": first(item, "height_cm"),
            "reputation": plain(first(item, "reputation")),
            "condition": first(item, "raw_condition"),
            "match_sharpness": first(item, "raw_match_sharpness"),
            "value": first(item, "transfer_value"),
            "transfer_value_state": plain(first(item, "transfer_value_state")),
            "current_ability": nested(item, "ability.current", "current_ability", "ability_current"),
            "potential_ability": nested(item, "ability.potential", "potential_ability", "ability_potential"),
            "potential_range_code": nested(item, "ability.potential_range_code"),
            "attributes": flatten_numeric(plain(attributes)),
            "raw_attributes": flatten_numeric(plain(first(item, "raw_attributes") or {})),
            "personality": flatten_numeric(plain(personality)),
            "positions": plain(first(item, "positions")),
            "contract": plain(contract),
            "traits": plain(first(item, "traits", "preferred_moves") or []),
            "on_loan": first(item, "on_loan"),
            "loan_parent_club_name": first(item, "loan_parent_club_name"),
            "general": {
                "birth_date": plain(first(item, "birth_date")),
                "nation_id": first(item, "nation_id"),
                "second_nation_ids": plain(first(item, "second_nation_ids") or []),
                "home_grown_nation_ids": plain(first(item, "home_grown_nation_ids") or []),
                "home_grown_club_uids": plain(first(item, "home_grown_club_uids") or []),
                "home_grown_club_names": plain(first(item, "home_grown_club_names") or []),
                "height_cm": first(item, "height_cm"),
                "left_foot": left_foot,
                "right_foot": right_foot,
                "raw_left_foot": first(item, "raw_left_foot"),
                "raw_right_foot": first(item, "raw_right_foot"),
                "club_uid": first(item, "club_uid"),
                "team_id": first(item, "team_id"),
                "team_slot": first(item, "team_slot"),
                "club_join_date": plain(first(item, "club_join_date")),
                "on_loan": first(item, "on_loan"),
                "loan_parent_club_name": first(item, "loan_parent_club_name"),
                "loan_start": plain(first(item, "loan_start")),
                "loan_end": plain(first(item, "loan_end")),
            },
            "state": {
                "raw_condition": first(item, "raw_condition"),
                "raw_match_sharpness": first(item, "raw_match_sharpness"),
                "transfer_value_state": plain(first(item, "transfer_value_state")),
                "potential_range_code": nested(item, "ability.potential_range_code"),
            },
            "raw": item
        })

    staff = []
    for item in tables.get("staff", []):
        if not isinstance(item, dict):
            continue
        staff.append({
            "uid": first(item, "uid"),
            "unique_id": first(item, "unique_id"),
            "name": first(item, "name", "full_name", "common_name"),
            "club_name": first(item, "club_name"),
            "job_title": value_label(plain(first(item, "job"))),
            "age": first(item, "age"),
            # A save-local nation ID, not a human-readable nationality.
            "nationality": None,
            "nation_id": first(item, "nation_id"),
            "is_human_manager": first(item, "is_human_manager"),
            "ability": plain(first(item, "ability")),
            "personality": plain(first(item, "personality")),
            "attributes": plain(first(item, "attributes")),
            "preferences": plain(first(item, "preferences")),
            "has_contract": first(item, "has_contract"),
            "wage": first(item, "wage"),
            "contract_start": first(item, "contract_start"),
            "contract_end": first(item, "contract_end"),
            "raw": item
        })

    clubs = []
    for item in tables.get("clubs", []):
        if not isinstance(item, dict):
            continue
        clubs.append({
            "uid": first(item, "uid"),
            "unique_id": first(item, "unique_id"),
            "name": first(item, "name", "long_name", "club_name"),
            # A save-local nation ID, not a country name.
            "nation": None,
            "nation_id": first(item, "nation_id"),
            "teams": plain(first(item, "teams") or []),
            "raw": item
        })

    envelope = {
        "schema": "fmatlas.fmsave.snapshot.v1",
        "created_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "source_file_name": save_path.name,
        "source_file_size": save_path.stat().st_size,
        "adapter": "FM Atlas local fmsave bridge",
        "fmsave_version": getattr(fmsave, "__version__", "not exposed"),
        "save_info": save_info,
        "validation": validation,
        "players": players,
        "staff": staff,
        "clubs": clubs,
        "managed_clubs": tables.get("managed_clubs", []),
        "contracts": tables.get("contracts", []),
        "finances": tables.get("finances", []),
        "facilities": tables.get("facilities", []),
        "stadiums": tables.get("stadiums", []),
        "affiliates": tables.get("affiliates", []),
        "competitions": tables.get("competitions", []),
        "nations": tables.get("nations", []),
        "injuries": tables.get("injuries", []),
        "player_season_stats": tables.get("player_season_stats", []),
        "player_season_history": tables.get("player_season_history", []),
        "player_match_stats": tables.get("player_match_stats", []),
        "fixtures": tables.get("fixtures", []),
        "league_tables": tables.get("league_tables", []),
        "tactics": tables.get("tactics", []),
        "training": tables.get("training", []),
        "mentoring": tables.get("mentoring", []),
        "set_pieces": tables.get("set_pieces", []),
        "selections": tables.get("selections", []),
        "counts": {name: len(rows) for name, rows in tables.items()},
        "warnings": warnings
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(envelope, ensure_ascii=False, separators=(",", ":"), default=str), encoding="utf-8")
    print(json.dumps({"output": str(output_path), "players": len(players), "staff": len(envelope["staff"]), "clubs": len(envelope["clubs"]), "warnings": warnings}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
