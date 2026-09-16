"""Offline orchestration regressions; these do not replace real scanner evidence."""

import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("qodana_lifecycle_runner", ROOT / "tools/scripts/qodana_ci.py")
qodana = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(qodana)
IMAGE = "jetbrains/qodana-cdnet:2026.2"
IMAGE_ID = "sha256:" + "a" * 64


def completed(label):
    rules = {qodana.PROBE_RULE: 1} if label == "positive" else {}
    return {"label": label, "status": "completed", "exit_code": 0,
            "seconds": 1.0, "cache_bytes": 128, "findings": sum(rules.values()),
            "fingerprint": "stable", "by_rule": rules}


class LifecycleTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        # Resolved because main() resolves --output; macOS's /var -> /private/var symlink
        # would otherwise desync this from the paths main() actually passes to command().
        self.root = Path(temporary.name).resolve()
        self.project = self.root / "repository"
        self.project.mkdir()
        (self.project / "ArchLinterNet.slnx").write_text("<Solution />\n", encoding="utf-8")
        (self.project / "qodana.yaml").write_text(f"linter: {IMAGE}\n", encoding="utf-8")
        self.output = self.root / "evidence"
        self.command = self.enterContext(patch.object(qodana, "command", return_value=0))
        self.inspect = self.enterContext(patch.object(
            qodana.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, IMAGE_ID + "\n")))
        self.scan = self.enterContext(patch.object(
            qodana, "scan", side_effect=lambda *args, **kwargs: completed(args[4])))
        self.enterContext(patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": "", "QODANA_ANALYZED_SHA": "b" * 40}))
        self.enterContext(contextlib.redirect_stdout(io.StringIO()))
        self.enterContext(contextlib.redirect_stderr(io.StringIO()))

    def run_main(self, burn_in=False):
        args = ["--project", str(self.project), "--output", str(self.output)]
        return qodana.main(args + (["--burn-in"] if burn_in else []))

    def evidence(self):
        return json.loads((self.output / "artifacts/evidence.json").read_text(encoding="utf-8"))

    def test_ordinary_run_uses_one_scan_and_records_identity(self):
        self.assertEqual(0, self.run_main())
        report = self.evidence()
        self.assertEqual("completed", report["status"])
        self.assertEqual(IMAGE_ID, report["image_id"])
        self.assertEqual("b" * 40, report["analyzed_sha"])
        self.assertFalse(report["cross_run_cache"])
        self.assertFalse(report["burn_in_requested"])
        self.assertEqual(["cold"], [s["label"] for s in report["scans"]])
        self.command.assert_called_once_with(["docker", "pull", IMAGE], self.output / "artifacts/image.log", 300)
        self.assertEqual(IMAGE_ID, self.scan.call_args.args[3])

    def test_advisory_findings_do_not_turn_successful_scan_into_failure(self):
        result = completed("cold")
        result.update(findings=10, by_rule={"ExistingDebt": 10})
        self.scan.side_effect = None
        self.scan.return_value = result
        self.assertEqual(0, self.run_main())
        self.assertEqual(10, self.evidence()["scans"][0]["findings"])

    def test_burn_in_reuses_image_and_repository_cache_but_isolates_probes(self):
        self.assertEqual(0, self.run_main(burn_in=True))
        self.assertTrue(self.evidence()["burn_in_checks_passed"])
        calls = self.scan.call_args_list
        self.assertEqual(["cold", "warm", "positive", "negative"], [c.args[4] for c in calls])
        self.assertEqual({IMAGE_ID}, {c.args[3] for c in calls})
        self.assertEqual(calls[0].args[5], calls[1].args[5])
        self.assertEqual(3, len({c.args[5] for c in calls}))
        for call in calls[2:]:
            self.assertNotEqual(self.project, call.args[0])
            self.assertEqual(180, call.kwargs["timeout"])
            self.assertTrue((call.args[0] / "Probe.csproj").is_file())

    def test_every_completed_phase_is_persisted_before_the_next_scan(self):
        observed = []

        def scan(*args, **kwargs):
            if observed:
                self.assertEqual(observed, [s["label"] for s in self.evidence()["scans"]])
                self.assertEqual("failed", self.evidence()["status"])
            observed.append(args[4])
            return completed(args[4])

        self.scan.side_effect = scan
        self.assertEqual(0, self.run_main(burn_in=True))
        self.assertEqual(4, len(observed))

    def test_cold_failure_does_not_run_additional_burn_in_scans(self):
        result = completed("cold")
        result.update(status="failed", exit_code=124)
        self.scan.side_effect = None
        self.scan.return_value = result
        self.assertEqual(1, self.run_main(burn_in=True))
        self.scan.assert_called_once()
        self.assertEqual(124, self.evidence()["scans"][0]["exit_code"])

    def test_missing_positive_inspection_fails_burn_in(self):
        self.scan.side_effect = lambda *args, **kwargs: {**completed(args[4]), "by_rule": {}}
        self.assertEqual(1, self.run_main(burn_in=True))
        self.assertFalse(self.evidence()["burn_in_checks_passed"])

    def test_later_phase_exception_preserves_completed_phases(self):
        self.scan.side_effect = [completed("cold"), completed("warm"), OSError("scanner unavailable")]
        self.assertEqual(1, self.run_main(burn_in=True))
        self.assertEqual(["cold", "warm"], [s["label"] for s in self.evidence()["scans"]])
        self.assertIn("scanner unavailable", self.evidence()["error"])

    def test_invalid_image_identity_prevents_scans(self):
        self.inspect.return_value.stdout = "latest\n"
        self.assertEqual(1, self.run_main())
        self.scan.assert_not_called()
        self.assertIn("immutable image identity", self.evidence()["error"])

    def test_image_inspection_timeout_is_not_success(self):
        self.inspect.side_effect = subprocess.TimeoutExpired("docker image inspect", 30)
        self.assertEqual(1, self.run_main())
        self.assertEqual("failed", self.evidence()["status"])
        self.scan.assert_not_called()

    def test_missing_solution_fails_before_docker(self):
        (self.project / "ArchLinterNet.slnx").unlink()
        self.assertEqual(1, self.run_main())
        self.command.assert_not_called()
        self.assertIn("canonical", self.evidence()["error"])

    def test_output_inside_repository_is_rejected_before_docker(self):
        self.output = self.project / "generated-evidence"
        with self.assertRaises(SystemExit) as raised:
            self.run_main()
        self.assertEqual(2, raised.exception.code)
        self.command.assert_not_called()
        self.assertFalse(self.output.exists())

    def test_existing_evidence_cannot_be_overwritten(self):
        self.output.mkdir()
        original = self.output / "evidence.json"
        original.write_text("prior evidence", encoding="utf-8")
        with self.assertRaises(SystemExit) as raised:
            self.run_main()
        self.assertEqual(2, raised.exception.code)
        self.assertEqual("prior evidence", original.read_text(encoding="utf-8"))
        self.command.assert_not_called()


if __name__ == "__main__":
    unittest.main()
