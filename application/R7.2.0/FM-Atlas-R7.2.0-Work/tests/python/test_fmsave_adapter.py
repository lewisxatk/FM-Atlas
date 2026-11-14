from __future__ import annotations

import importlib.util
import json
import pathlib
import sys
import tempfile
import types
import unittest
from unittest.mock import patch


WORK_ROOT = pathlib.Path(__file__).resolve().parents[2]
ADAPTER_PATH = WORK_ROOT / "tools" / "Export-FMAtlas-Fmsave.py"
SPEC = importlib.util.spec_from_file_location("fmatlas_fmsave_adapter", ADAPTER_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Could not load adapter from {ADAPTER_PATH}")
ADAPTER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ADAPTER)


TABLE_NAMES = [
    "players", "contracts", "suspensions", "staff", "staff_lists",
    "clubs", "managed_clubs", "finances", "sponsorships", "facilities",
    "stadiums", "affiliates", "job_vacancies", "stages", "competitions",
    "fixtures", "league_tables", "competition_rules", "transfer_windows", "officials",
    "player_match_stats", "team_match_stats", "player_season_stats", "player_season_history", "squad_planner",
    "nations", "injury_types", "injuries", "training", "mentoring",
    "tactics", "set_pieces", "selections"
]


class FakeTable:
    def __init__(self, rows):
        self._rows = rows

    def iter_rows(self, named=True):
        if not named:
            raise AssertionError("The adapter should request named rows.")
        yield from self._rows


class FakeSave:
    info = types.SimpleNamespace(
        game="FM27",
        build="27.0.0-test",
        build_number=270000,
        known_build=True,
        db_version="test-db",
        game_date="2026-11-14",
        time_slot="afternoon",
        summary_strings=["must-not-be-exported"],
    )

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_value, traceback):
        return False

    def __getattr__(self, name):
        if name not in TABLE_NAMES:
            raise AttributeError(name)

        def read_table():
            if name == "players":
                return FakeTable([{
                    "uid": "P-100",
                    "unique_id": 100,
                    "name": "Test Striker",
                    "age": 24,
                    "birth_date": "2002-01-01",
                    "club_uid": "C-10",
                    "club_name": "Example FC",
                    "natural_positions": ["ST"],
                    "accomplished_positions": ["AM (C)"],
                    "left_foot": 15,
                    "right_foot": 18,
                    "height_cm": 181,
                    "transfer_value": 1250000,
                    "ability": {"current": 120, "potential": 150, "potential_range_code": 0},
                    "attributes": {"pace": 15, "finishing": 17},
                    "raw_attributes": {"raw_pace": 1500},
                    "personality": {"professionalism": 18},
                    "contract": {"wage": 10000, "contract_end": "2028-06-30"},
                    "traits": ["Likes to beat offside trap"],
                    "home_grown_nation_ids": [1],
                    "home_grown_club_uids": ["C-10"],
                }])
            if name == "staff":
                return FakeTable([{
                    "uid": "S-1",
                    "unique_id": 501,
                    "name": "Test Coach",
                    "club_name": "Example FC",
                    "job": "Manager",
                    "age": 45,
                    "nation_id": 1,
                    "attributes": {"judging_player_ability": 16},
                }])
            if name == "clubs":
                return FakeTable([{
                    "uid": "C-10",
                    "unique_id": 10,
                    "name": "Example FC",
                    "nation_id": 1,
                    "teams": [],
                }])
            if name == "player_season_stats":
                return FakeTable([{
                    "player_uid": "P-100",
                    "season": "2026/27",
                    "minutes_played": 900,
                    "goals": 10,
                }])
            if name == "team_match_stats":
                return FakeTable([{
                    "team_uid": "C-10",
                    "competition_id": 42,
                    "home_team_id": "C-10",
                    "away_team_id": "C-20",
                    "date": "2026-11-01",
                    "shots": 14,
                    "possession": 61.5,
                    "expected_goals": 1.8,
                }])
            return FakeTable([])

        return read_table


class FmsaveAdapterStreamingTests(unittest.TestCase):
    def test_main_writes_normalised_ndjson_bundle_incrementally(self):
        fake_fmsave = types.ModuleType("fmsave")
        fake_fmsave.__version__ = "test-version"
        fake_fmsave.open = lambda _path: FakeSave()
        fake_fmsave.validate_save = lambda _save: types.SimpleNamespace(
            fmsave_version="test-version",
            game="FM27",
            build="27.0.0-test",
            known_build=True,
            readers=[],
            field_statuses={},
            experimental_counts={},
        )

        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            save_copy = root / "career-copy.fm"
            save_copy.write_bytes(b"read-only fixture")
            output = root / "bundle"
            argv = ["Export-FMAtlas-Fmsave.py", str(save_copy), str(output)]
            with patch.dict(sys.modules, {"fmsave": fake_fmsave}), patch.object(sys, "argv", argv):
                result = ADAPTER.main()

            self.assertEqual(result, 0)
            manifest_path = output / "manifest.json"
            self.assertTrue(manifest_path.is_file())
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            self.assertEqual(manifest["schema"], "fmatlas.fmsave.snapshot.ndjson.v1")
            self.assertEqual(manifest["reader_scope"]["intended_views"], ["FM Data Hub", "FM League Hub", "FM player and club records"])
            self.assertFalse(manifest["reader_scope"]["external_metrics_included"])
            self.assertEqual(manifest["counts"]["players"], 1)
            self.assertEqual(manifest["counts"]["staff"], 1)
            self.assertEqual(manifest["counts"]["clubs"], 1)
            self.assertEqual(manifest["counts"]["player_season_stats"], 1)
            self.assertEqual(manifest["counts"]["team_match_stats"], 1)
            self.assertIn("officials", manifest["counts"])
            self.assertIn("squad_planner", manifest["counts"])

            player_rows = [json.loads(line) for line in (output / "players.ndjson").read_text(encoding="utf-8").splitlines()]
            self.assertEqual(len(player_rows), 1)
            player = player_rows[0]
            self.assertEqual(player["atlas_source_id"], "FMSAVE:FM27:test-db:100")
            self.assertEqual(player["position"], "ST")
            self.assertEqual(player["attributes"]["pace"], 15)
            self.assertEqual(player["personality"]["professionalism"], 18)
            self.assertEqual(player["contract"]["wage"], 10000)

            staff_rows = [json.loads(line) for line in (output / "staff.ndjson").read_text(encoding="utf-8").splitlines()]
            club_rows = [json.loads(line) for line in (output / "clubs.ndjson").read_text(encoding="utf-8").splitlines()]
            stats_rows = [json.loads(line) for line in (output / "player_season_stats.ndjson").read_text(encoding="utf-8").splitlines()]
            team_stats_rows = [json.loads(line) for line in (output / "team_match_stats.ndjson").read_text(encoding="utf-8").splitlines()]
            self.assertEqual(staff_rows[0]["job_title"], "Manager")
            self.assertEqual(club_rows[0]["name"], "Example FC")
            self.assertEqual(stats_rows[0]["goals"], 10)
            self.assertEqual(team_stats_rows[0]["possession"], 61.5)
            self.assertEqual(team_stats_rows[0]["expected_goals"], 1.8)

            # The bundle keeps only a small JSON manifest plus one-row-per-line table streams.
            self.assertFalse(list(output.glob("*.raw.ndjson")))
            self.assertEqual([p.name for p in output.glob("*.json")], ["manifest.json"])
            self.assertNotIn("summary_strings", manifest)


    def test_existing_output_folder_is_never_deleted(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            save_copy = root / "career-copy.fm"
            save_copy.write_bytes(b"read-only fixture")
            output = root / "existing-user-folder"
            output.mkdir()
            sentinel = output / "keep-me.txt"
            sentinel.write_text("user data must survive", encoding="utf-8")

            argv = ["Export-FMAtlas-Fmsave.py", str(save_copy), str(output)]
            with patch.object(sys, "argv", argv):
                result = ADAPTER.main()

            self.assertEqual(result, 2)
            self.assertEqual(sentinel.read_text(encoding="utf-8"), "user data must survive")
            self.assertEqual(sorted(p.name for p in output.iterdir()), ["keep-me.txt"])


if __name__ == "__main__":
    unittest.main()
