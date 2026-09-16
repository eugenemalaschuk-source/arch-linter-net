"""Offline runner/CI contract tests; mocks are not evidence that Qodana detected a defect."""

import copy
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("qodana_ci", ROOT / "tools/scripts/qodana_ci.py")
ci = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ci)
IMAGE_ID = "sha256:" + "a" * 64


def sarif(results=None):
    return {"version": "2.1.0", "runs": [{
        "tool": {"driver": {"name": "Qodana Community for .NET", "rules": [{"id": ci.PROBE_RULE}]}},
        "invocations": [{"executionSuccessful": True}],
        "results": [] if results is None else results,
    }]}


def finding(rule=ci.PROBE_RULE):
    return {"ruleId": rule, "message": {"text": "Expression is always true"},
            "locations": [{"physicalLocation": {"artifactLocation": {"uri": "Probe.cs"},
                                                "region": {"startLine": 10}}}]}


class InventoryTests(unittest.TestCase):
    def test_valid_empty_inventory_is_distinct_from_missing_report(self):
        self.assertEqual(0, ci.inventory(sarif())["findings"])
        for invalid in ({}, {"version": "2.1.0", "runs": []}):
            with self.assertRaises(ValueError):
                ci.inventory(invalid)

    def test_order_and_invocation_timestamps_do_not_change_fingerprint(self):
        a = sarif([finding("A"), finding("B")])
        b = copy.deepcopy(a)
        b["runs"][0]["results"].reverse()
        b["runs"][0]["invocations"][0]["startTimeUtc"] = "2030-01-01T00:00:00Z"
        self.assertEqual(ci.inventory(a), ci.inventory(b))

    def test_changed_rule_message_location_and_duplicate_change_fingerprint(self):
        original = ci.inventory(sarif([finding()]))["fingerprint"]
        variants = [finding("Other"), finding(), finding()]
        variants[1]["message"]["text"] = "Changed message"
        variants[2]["locations"][0]["physicalLocation"]["region"]["startLine"] = 11
        for changed in variants:
            self.assertNotEqual(original, ci.inventory(sarif([changed]))["fingerprint"])
        self.assertNotEqual(original, ci.inventory(sarif([finding(), finding()]))["fingerprint"])

    def test_rule_and_artifact_indexes_are_resolved(self):
        direct = sarif([finding()])
        indexed = copy.deepcopy(direct)
        run = indexed["runs"][0]
        result = run["results"][0]
        del result["ruleId"]
        result["ruleIndex"] = 0
        result["locations"][0]["physicalLocation"]["artifactLocation"] = {"index": 0}
        run["artifacts"] = [{"location": {"uri": "Probe.cs"}}]
        self.assertEqual(ci.inventory(direct), ci.inventory(indexed))

    def test_failed_invocation_and_error_notification_are_not_clean(self):
        for invocation in ({"executionSuccessful": False},
                           {"toolExecutionNotifications": [{"level": "error"}]},
                           {"toolConfigurationNotifications": [{"level": "error"}]}):
            data = sarif()
            data["runs"][0]["invocations"] = [invocation]
            with self.assertRaises(ValueError):
                ci.inventory(data)

    def test_missing_results_or_driver_is_not_clean(self):
        data = sarif()
        del data["runs"][0]["results"]
        with self.assertRaises(ValueError):
            ci.inventory(data)
        data = sarif()
        data["runs"][0]["tool"]["driver"]["name"] = ""
        with self.assertRaises(ValueError):
            ci.inventory(data)

    def test_absent_baseline_findings_are_not_active(self):
        item = finding()
        item["baselineState"] = "absent"
        self.assertEqual(0, ci.inventory(sarif([item]))["findings"])

    def test_missing_rule_and_message_are_rejected(self):
        for key in ("ruleId", "message"):
            item = finding()
            del item[key]
            with self.assertRaises(ValueError):
                ci.inventory(sarif([item]))

    def test_read_rejects_missing_malformed_symlink_and_oversize(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "report.json"
            with self.assertRaises(ValueError):
                ci.read_inventory(path)
            for contents in ("not JSON", "null", "[]", '{"version":"2.1.0","runs":[null]}'):
                path.write_text(contents)
                with self.assertRaises(ValueError):
                    ci.read_inventory(path)
            target = Path(tmp) / "target.json"
            target.write_text(json.dumps(sarif()))
            path.unlink()
            path.symlink_to(target)
            with self.assertRaises(ValueError):
                ci.read_inventory(path)
            path.unlink()
            with path.open("wb") as output:
                output.truncate(ci.MAX_SARIF_BYTES + 1)
            with self.assertRaises(ValueError):
                ci.read_inventory(path)


class RunnerTests(unittest.TestCase):
    def test_image_must_be_community_and_version_pinned(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for image in ("jetbrains/qodana-cdnet:2026.2",
                          "jetbrains/qodana-cdnet:2026.2@sha256:" + "a" * 64):
                (root / "qodana.yaml").write_text(f"version: '1.0'\nlinter: {image}\n")
                self.assertEqual(image, ci.configured_image(root))
            for image in ("jetbrains/qodana-dotnet:2026.2", "jetbrains/qodana-cdnet:latest",
                          "jetbrains/qodana-cdnet:2026.2-privileged", "x; echo pwned"):
                (root / "qodana.yaml").write_text(f"linter: {image}\n")
                with self.assertRaises(ValueError):
                    ci.configured_image(root)
            (root / "qodana.yaml").write_text("linter: jetbrains/qodana-cdnet:2026.2\n" * 2)
            with self.assertRaises(ValueError):
                ci.configured_image(root)

    def test_timeout_and_missing_executable_are_recorded(self):
        with tempfile.TemporaryDirectory() as tmp:
            log = Path(tmp) / "scan.log"
            for error, code in ((subprocess.TimeoutExpired("docker", 1), 124),
                                (FileNotFoundError("docker"), 127)):
                with patch.object(ci.subprocess, "run", side_effect=error):
                    self.assertEqual(code, ci.command(["docker"], log, 1))
            self.assertIn("exceeded", log.read_text())
            self.assertIn("Unable to execute", log.read_text())

    def exercise_scan(self, exit_code=0, document=None, produce=True, internal_log=False):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            project, work, artifacts = root / "project", root / "work", root / "artifacts"
            project.mkdir()
            artifacts.mkdir()
            invocations = []

            def fake_command(args, log, timeout):
                invocations.append(args)
                if args[:2] == ["docker", "run"] and produce:
                    report = work / "cold/results/qodana.sarif.json"
                    report.write_text(json.dumps(sarif() if document is None else document))
                if args[:2] == ["docker", "run"] and internal_log:
                    internal = work / "cold/results/log/qodana.log"
                    internal.parent.mkdir(parents=True, exist_ok=True)
                    internal.write_text("internal diagnostic marker")
                return exit_code

            # os.getuid/getgid don't exist on Windows; this suite is offline and must not
            # depend on Unix-only APIs the mocked command() never actually needs.
            with patch.object(ci, "command", side_effect=fake_command), \
                 patch.object(ci.os, "getuid", create=True, return_value=1000), \
                 patch.object(ci.os, "getgid", create=True, return_value=1000):
                result = ci.scan(project, work, artifacts, IMAGE_ID, "cold", root / "cache", 2)
            saved = (artifacts / "cold.sarif.json").is_file()
            log_preserved = (artifacts / "cold-log/qodana.log").is_file()
            return result, invocations, saved, log_preserved

    def test_success_requires_usable_sarif_and_zero_exit(self):
        result, commands, saved, _ = self.exercise_scan()
        self.assertEqual("completed", result["status"])
        self.assertTrue(saved)
        self.assertEqual(["docker", "rm", "--force"], commands[-1][:3])
        self.assertIn(IMAGE_ID, commands[0])
        for forbidden in ("--env", "-e", "--privileged", "/var/run/docker.sock"):
            self.assertNotIn(forbidden, " ".join(commands[0]) if forbidden.startswith("/") else commands[0])

    def test_nonzero_exit_with_partial_report_remains_failed(self):
        result, commands, saved, _ = self.exercise_scan(exit_code=1, document=sarif([finding()]))
        self.assertEqual("failed", result["status"])
        self.assertEqual(1, result["findings"])
        self.assertTrue(saved)
        self.assertEqual("rm", commands[-1][1])

    def test_zero_exit_without_report_is_not_clean(self):
        result, _, saved, _ = self.exercise_scan(produce=False)
        self.assertEqual("failed", result["status"])
        self.assertNotIn("findings", result)
        self.assertFalse(saved)

    def test_timeout_still_removes_container(self):
        result, commands, _, _ = self.exercise_scan(exit_code=124, produce=False)
        self.assertEqual(124, result["exit_code"])
        self.assertEqual("failed", result["status"])
        self.assertEqual("rm", commands[-1][1])

    def test_scanner_error_in_sarif_is_failed_but_report_is_still_published(self):
        data = sarif()
        data["runs"][0]["invocations"][0]["executionSuccessful"] = False
        result, _, saved, _ = self.exercise_scan(document=data)
        self.assertEqual("failed", result["status"])
        self.assertNotIn("findings", result)
        # The diagnostic report is preserved evidence even though the analysis is unsuccessful.
        self.assertTrue(saved)

    def test_configuration_error_with_clean_results_is_not_a_successful_scan(self):
        data = sarif()
        data["runs"][0]["invocations"][0]["toolConfigurationNotifications"] = [
            {"level": "error", "message": {"text": "A requested project could not be configured"}}]
        result, _, saved, _ = self.exercise_scan(document=data)
        self.assertEqual("failed", result["status"])
        self.assertNotIn("findings", result)
        self.assertTrue(saved)

    def test_internal_qodana_log_directory_is_copied_on_success_and_failure(self):
        for exit_code in (0, 1):
            with self.subTest(exit_code=exit_code):
                _, _, _, log_preserved = self.exercise_scan(exit_code=exit_code,
                                                            internal_log=True)
                self.assertTrue(log_preserved)

    def test_unreadable_sarif_is_recorded_but_does_not_lose_exit_code_or_duration(self):
        original_read_bytes = Path.read_bytes

        def flaky_read_bytes(path):
            if path.name == "qodana.sarif.json":
                raise PermissionError("Permission denied")
            return original_read_bytes(path)

        with patch.object(Path, "read_bytes", flaky_read_bytes):
            result, _, saved, _ = self.exercise_scan(document=sarif([finding()]))
        # read_inventory() reads the SARIF via read_text(), independent of the failed
        # read_bytes() copy, so a valid scan still reports its real outcome and findings.
        self.assertEqual("completed", result["status"])
        self.assertEqual(1, result["findings"])
        self.assertFalse(saved)
        self.assertIn("Permission denied", result["sarif_copy_error"])

    def test_unreadable_internal_log_file_is_recorded_but_does_not_lose_the_scan(self):
        original_read_bytes = Path.read_bytes

        def flaky_read_bytes(path):
            if path.name == "qodana.log":
                raise PermissionError("Permission denied")
            return original_read_bytes(path)

        with patch.object(Path, "read_bytes", flaky_read_bytes):
            result, _, saved, log_preserved = self.exercise_scan(
                document=sarif([finding()]), internal_log=True)
        self.assertEqual("completed", result["status"])
        self.assertEqual(1, result["findings"])
        self.assertEqual(0, result["exit_code"])
        self.assertTrue(saved)
        self.assertFalse(log_preserved)
        self.assertIn("qodana.log", result["log_errors"][0])

    def test_cache_measurement_failure_does_not_lose_a_completed_scan(self):
        with patch.object(ci, "cache_bytes", side_effect=PermissionError("no access")):
            result, _, saved, _ = self.exercise_scan(document=sarif([finding()]))
        self.assertEqual("completed", result["status"])
        self.assertEqual(1, result["findings"])
        self.assertIsNone(result["cache_bytes"])
        self.assertIn("no access", result["cache_error"])
        self.assertTrue(saved)

    def test_copy_bounded_tree_skips_symlinks_and_enforces_budgets(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source, destination = root / "source", root / "destination"
            (source / "nested").mkdir(parents=True)
            (source / "small.log").write_bytes(b"ok")
            (source / "nested/big.log").write_bytes(b"x" * 20)
            (source / "linked.log").symlink_to(source / "small.log")
            ci.copy_bounded_tree(source, destination, max_file_bytes=10, max_total_bytes=100)
            self.assertEqual("ok", (destination / "small.log").read_text())
            self.assertFalse((destination / "nested/big.log").exists())
            self.assertFalse((destination / "linked.log").exists())

    def test_copy_bounded_tree_enforces_total_budget_across_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source, destination = root / "source", root / "destination"
            source.mkdir()
            (source / "a.log").write_bytes(b"a" * 6)
            (source / "b.log").write_bytes(b"b" * 6)
            ci.copy_bounded_tree(source, destination, max_file_bytes=10, max_total_bytes=10)
            copied = sorted(p.name for p in destination.iterdir())
            self.assertEqual(["a.log"], copied)

    def test_copy_bounded_tree_tolerates_missing_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            errors = ci.copy_bounded_tree(root / "absent", root / "destination", 10, 10)
            self.assertEqual([], errors)
            self.assertFalse((root / "destination").exists())

    def test_copy_bounded_tree_continues_past_an_unreadable_file(self):
        original_read_bytes = Path.read_bytes

        def flaky_read_bytes(path):
            if path.name == "bad.log":
                raise PermissionError("Permission denied")
            return original_read_bytes(path)

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source, destination = root / "source", root / "destination"
            source.mkdir()
            (source / "bad.log").write_bytes(b"unreadable")
            (source / "good.log").write_bytes(b"ok")
            with patch.object(Path, "read_bytes", flaky_read_bytes):
                errors = ci.copy_bounded_tree(source, destination, max_file_bytes=100,
                                              max_total_bytes=100)
            self.assertIn("bad.log", errors[0])
            self.assertEqual("ok", (destination / "good.log").read_text())

    def test_cache_measurement_does_not_follow_symlinks(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "file").write_bytes(b"1234")
            (root / "link").symlink_to(root / "file")
            self.assertEqual(4, ci.cache_bytes(root))

    def test_probe_is_standalone_and_has_positive_and_corrected_forms(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for name, defective in (("positive", True), ("negative", False)):
                ci.create_probe(root / name, "jetbrains/qodana-cdnet:2026.2", defective)
            self.assertIn("return value != null;", (root / "positive/Probe.cs").read_text())
            self.assertIn("return true;", (root / "negative/Probe.cs").read_text())
            self.assertIn(ci.PROBE_RULE, (root / "positive/qodana.yaml").read_text())
            self.assertFalse((root / "ArchLinterNet.slnx").exists())

    def test_burn_in_requires_determinism_positive_and_negative_controls(self):
        clean = {"status": "completed", **ci.inventory(sarif())}
        positive = {"status": "completed", **ci.inventory(sarif([finding()]))}
        scans = [clean, clean, positive, clean]
        self.assertTrue(ci.burn_in_passed(scans))
        self.assertFalse(ci.burn_in_passed(scans[:3]))
        for index, replacement in ((1, positive), (2, clean), (3, positive),
                                   (0, {"status": "failed"})):
            changed = scans.copy()
            changed[index] = replacement
            self.assertFalse(ci.burn_in_passed(changed))

    def test_pull_failure_writes_evidence_and_does_not_claim_success(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            project = root / "project"
            project.mkdir()
            (project / "ArchLinterNet.slnx").write_text("<Solution />")
            (project / "qodana.yaml").write_text("linter: jetbrains/qodana-cdnet:2026.2\n")
            with patch.object(ci, "command", return_value=127), patch.dict(os.environ, {}, clear=True):
                code = ci.main(["--project", str(project), "--output", str(root / "out")])
            evidence = json.loads((root / "out/artifacts/evidence.json").read_text())
            self.assertEqual(1, code)
            self.assertEqual("failed", evidence["status"])
            self.assertEqual(127, evidence["pull_exit_code"])
            self.assertEqual([], evidence["scans"])

    def test_summary_does_not_render_untrusted_diagnostics(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            summary = root / "actions-summary"
            evidence = {"status": "failed", "error": "::error::untrusted <script>", "scans": []}
            with patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(summary)}):
                ci.write_evidence(root, evidence, publish_summary=False)
                self.assertFalse(summary.exists())
                ci.write_evidence(root, evidence)
            self.assertNotIn("<script>", summary.read_text())
            self.assertNotIn("::error::", summary.read_text())


class WorkflowContractTests(unittest.TestCase):
    def test_workflow_is_independent_read_only_and_fork_safe(self):
        workflow = (ROOT / ".github/workflows/qodana.yml").read_text()
        self.assertIn("  pull_request:", workflow)
        self.assertIn("runs-on: ubuntu-24.04", workflow)
        self.assertIn("persist-credentials: false", workflow)
        self.assertIn("name: Qodana Community .NET (advisory)", workflow)
        for forbidden in (r"secrets[.\[]", r"pull_request_target", r"\bneeds:",
                          r"\b(?:contents|checks|pull-requests|security-events|id-token): write",
                          r"(?m)^\s+continue-on-error:", r"self-hosted"):
            self.assertNotRegex(workflow, forbidden)
        self.assertRegex(workflow, r"timeout-minutes: [1-9][0-9]*")
        self.assertIn("if: always()", workflow)
        self.assertIn("if-no-files-found: error", workflow)
        self.assertIn("/qodana/artifacts", workflow)
        self.assertNotIn("actions/cache@", workflow)

    def test_all_external_actions_are_sha_pinned(self):
        workflow = (ROOT / ".github/workflows/qodana.yml").read_text()
        actions = re.findall(r"uses:\s+(\S+)", workflow)
        self.assertTrue(actions)
        for action in actions:
            self.assertRegex(action, r"^[\w/-]+@[0-9a-f]{40}$")

    def test_canonical_configuration_has_no_baseline_or_suppression(self):
        config = (ROOT / "qodana.yaml").read_text()
        self.assertEqual("jetbrains/qodana-cdnet:2026.2", ci.configured_image(ROOT))
        self.assertIn("solution: ArchLinterNet.slnx", config)
        self.assertIn("name: qodana.recommended", config)
        for forbidden in (r"(?m)^exclude:", r"(?m)^baseline:", r"(?m)^failThreshold:",
                          r"(?m)^bootstrap:", r"(?m)^fixesStrategy:"):
            self.assertNotRegex(config, forbidden)


if __name__ == "__main__":
    unittest.main()
