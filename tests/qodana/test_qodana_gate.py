"""Contract tests for the trusted differential Qodana gate and PR publisher."""

from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "qodana_gate", ROOT / "tools/scripts/qodana_gate.py"
)
gate = importlib.util.module_from_spec(SPEC)
import sys

sys.modules[SPEC.name] = gate
SPEC.loader.exec_module(gate)

HEAD = "a" * 40
BASE = "b" * 40
REPOSITORY = "owner/repository"


def finding(rule="UseCollectionExpression", severity="Moderate", path="src/Example.cs", line=10,
            message="Use collection expression"):
    raw_fingerprint = hashlib.sha256(f"{rule}:{path}:{line}:{message}".encode()).hexdigest()
    return {
        "ruleId": rule,
        "message": {"text": message},
        "properties": {"qodanaSeverity": severity},
        "partialFingerprints": {"equalIndicator/v1": raw_fingerprint.upper()},
        "locations": [{"physicalLocation": {
            "artifactLocation": {"uri": path},
            "region": {"startLine": line, "startColumn": 4},
        }}],
    }


def sarif(results=None):
    return json.dumps({
        "version": "2.1.0",
        "runs": [{
            "tool": {"driver": {"name": "QDNETC", "rules": []}},
            "invocations": [{"executionSuccessful": True}],
            "results": [] if results is None else results,
        }],
    }).encode()


def baseline(results=None):
    value = gate.build_baseline(
        sarif(results), analyzed_sha=HEAD, image=gate.EXPECTED_IMAGE
    )
    return json.dumps(value).encode()


class DifferentialGateTests(unittest.TestCase):
    def test_every_new_moderate_finding_fails(self):
        result = gate.evaluate(sarif([finding()]), baseline())
        self.assertFalse(result.passed)
        self.assertEqual("UseCollectionExpression", result.blocking[0].rule)

    def test_new_low_finding_is_reported_without_failing(self):
        result = gate.evaluate(sarif([finding(severity="Low")]), baseline())
        self.assertTrue(result.passed)
        self.assertEqual(1, len(result.new))
        self.assertEqual([], result.blocking)

    def test_new_high_finding_fails_even_when_identity_is_in_baseline(self):
        original = finding(rule="RedundantUsingDirective")
        report_baseline = baseline([original])
        high = copy.deepcopy(original)
        high["properties"]["qodanaSeverity"] = "High"
        result = gate.evaluate(sarif([high]), report_baseline)
        self.assertFalse(result.passed)
        self.assertEqual(1, len(result.blocking))

    def test_removing_baseline_findings_is_reported_as_remediated(self):
        existing = finding(rule="UnusedMember.Global")
        result = gate.evaluate(sarif(), baseline([existing]))
        self.assertTrue(result.passed)
        self.assertEqual(1, len(result.remediated))
        self.assertEqual([], result.new)

    def test_initial_baseline_refuses_high_or_critical_findings(self):
        for severity in ("High", "Critical"):
            item = finding(severity=severity)
            with self.subTest(severity=severity), self.assertRaises(gate.GateError):
                baseline([item])

    def test_baseline_rejects_unreviewed_high_severity_entry_and_bad_policy(self):
        value = json.loads(baseline())
        value["findings"] = [gate.Finding(
            "1" * 64, "SomeRule", "Critical", "src/Foo.cs", 5, "bad"
        ).as_json()]
        encoded = json.dumps(value).encode()
        with self.assertRaises(gate.GateError):
            gate.parse_baseline(encoded)
        value = json.loads(baseline())
        value["block_new_moderate"] = False
        encoded = json.dumps(value).encode()
        with self.assertRaises(gate.GateError):
            gate.parse_baseline(encoded)

    def test_malformed_sarif_and_unrecognized_severity_fail_closed(self):
        with self.assertRaises(gate.GateError):
            gate.parse_sarif(b"not JSON")
        unrecognized = sarif([finding(severity="Unknown")])
        with self.assertRaises(gate.GateError):
            gate.parse_sarif(unrecognized)
        malformed = json.loads(sarif([finding()]))
        malformed["runs"][0]["invocations"][0]["executionSuccessful"] = False
        encoded = json.dumps(malformed).encode()
        with self.assertRaises(gate.GateError):
            gate.parse_sarif(encoded)

    def test_report_has_stable_marker_identity_and_bounded_actionable_locations(self):
        result = gate.evaluate(sarif([finding(rule="UnusedParameter.Global")]), baseline())
        report = gate.render_report(
            result, run_url="https://github.com/owner/repository/actions/runs/22",
            run_id="22", attempt="1", head_sha=HEAD, base_sha=BASE, baseline_updated=True,
        )
        self.assertIn(gate.REPORT_MARKER, report)
        self.assertIn("UnusedParameter.Global", report)
        self.assertIn("src/Example.cs:10", report)
        self.assertIn("new; 0 remediated", report)
        self.assertIn("applies only after merge", report)

    def test_untrusted_diagnostic_text_cannot_inject_markdown_blocks_or_mentions(self):
        result = gate.evaluate(
            sarif([finding(message="Diagnostic\n@maintainer | [click](https://example.invalid) <tag>")]),
            baseline(),
        )
        report = gate.render_report(
            result, run_url="https://github.com/owner/repository/actions/runs/22",
            run_id="22", attempt="1", head_sha=HEAD, base_sha=BASE,
        )
        self.assertNotIn("\n@maintainer", report)
        self.assertIn("&#64;maintainer", report)
        self.assertIn("\\[click\\]", report)
        self.assertIn("&lt;tag&gt;", report)

    def test_finding_identity_includes_rule_location_and_message(self):
        findings = gate.parse_sarif(sarif([
            finding(line=10), finding(line=11), finding(message="Different message")
        ]))
        self.assertEqual(3, len({item.key for item in findings}))


class EvidenceValidationTests(unittest.TestCase):
    def evidence(self, **updates):
        result = {
            "schema_version": 1,
            "status": "completed",
            "repository": REPOSITORY,
            "pull_request_number": 7,
            "head_sha": HEAD,
            "base_sha": BASE,
            "base_analyzed_sha": BASE,
            "analyzed_sha": HEAD,
            "run_id": "123",
            "run_attempt": "2",
            "event": "pull_request",
            "configured_image": gate.EXPECTED_IMAGE,
            "configured_profile": gate.EXPECTED_PROFILE,
            "configured_solution": gate.EXPECTED_SOLUTION,
            "image_id": "sha256:" + "c" * 64,
            "scans": [{"label": "base", "status": "completed", "exit_code": 0},
                      {"label": "cold", "status": "completed", "exit_code": 0}],
        }
        result.update(updates)
        return json.dumps(result).encode()

    def test_exact_run_head_base_repository_and_attempt_are_required(self):
        expected = {
            "repository": REPOSITORY,
            "pr_number": 7,
            "head_sha": HEAD,
            "base_sha": BASE,
            "run_id": 123,
            "attempt": 2,
        }
        gate._validate_evidence(self.evidence(), **expected)
        for field, wrong in (("repository", "other/repository"),
                             ("pull_request_number", 8),
                             ("head_sha", "d" * 40),
                             ("base_sha", "e" * 40),
                             ("run_id", "999"),
                             ("run_attempt", "1")):
            raw = self.evidence(**{field: wrong})
            with self.subTest(field=field), self.assertRaises(gate.GateError):
                gate._validate_evidence(raw, **expected)

    def test_failed_scan_or_wrong_profile_is_unassessable(self):
        expected = {
            "repository": REPOSITORY, "pr_number": 7, "head_sha": HEAD,
            "base_sha": BASE, "run_id": 123, "attempt": 2,
        }
        for update in ({"status": "failed"}, {"configured_profile": "different"},
                       {"scans": [{"label": "cold", "status": "failed", "exit_code": 1}]}):
            raw = self.evidence(**update)
            with self.subTest(update=update), self.assertRaises(gate.GateError):
                gate._validate_evidence(raw, **expected)

    def test_source_run_association_requires_matching_head_and_base(self):
        run = {"pull_requests": [{
            "number": 7, "head": {"sha": HEAD}, "base": {"sha": BASE}
        }]}
        self.assertEqual((HEAD, BASE), gate._source_pull_request_identity(run, 7, "f" * 40))
        self.assertEqual((HEAD, None), gate._source_pull_request_identity(
            {"pull_requests": []}, 7, HEAD
        ))

    def test_artifact_rejects_missing_unexpected_or_symlinked_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "evidence.json").write_text("{}")
            (root / "cold.sarif.json").write_text("{}")
            (root / "base.sarif.json").write_text("{}")
            gate._downloaded_reports(root)
            (root / "evil.txt").write_text("untrusted")
            with self.assertRaises(gate.GateError):
                gate._downloaded_reports(root)

    def test_configuration_and_suppression_changes_are_flagged(self):
        files = [
            {"filename": "qodana.yaml", "patch": "@@\n+profile: changed"},
            {"filename": "tools/qodana/baseline.json", "patch": "@@\n+{}"},
            {"filename": "src/Nested/.editorconfig", "patch": "@@\n+resharper_unused_member_global_highlighting = none"},
            {"filename": "src/Example.cs", "patch": "@@\n+// ReSharper disable All"},
        ]
        self.assertEqual(3, len(gate._added_suppressions(files)))
        self.assertFalse(gate._is_protected_scan_path(gate.BASELINE_PATH))
        self.assertTrue(gate._is_protected_scan_path("src/Nested/.editorconfig"))
        self.assertEqual([], gate._added_suppressions([
            {"filename": "src/Example.cs", "patch": "@@\n+public class Example {}"}
        ]))
        self.assertEqual([], gate._added_suppressions([
            {"filename": "src/Example.cs", "patch": "+[SuppressMessage(\"Major Code Smell\", \"S107\", Justification = \"Public API compatibility\")]"}
        ]))

    def test_updated_baseline_must_be_the_exact_scan_inventory(self):
        raw_sarif = sarif([finding()])
        candidate = gate.build_baseline(raw_sarif, analyzed_sha=HEAD, image=gate.EXPECTED_IMAGE)
        encoded = (json.dumps(candidate, indent=2, sort_keys=True) + "\n").encode()
        gate.validate_candidate_baseline(encoded, raw_sarif, analyzed_sha=HEAD)

        candidate["findings"].append(gate.Finding(
            "a" * 64, "OtherInspection", "Moderate", "src/Unreported.cs", 12, "hidden debt"
        ).as_json())
        encoded = (json.dumps(candidate, indent=2, sort_keys=True) + "\n").encode()
        with self.assertRaises(gate.GateError):
            gate.validate_candidate_baseline(encoded, raw_sarif, analyzed_sha=HEAD)


class GitHubRawFileTests(unittest.TestCase):
    def test_raw_content_request_supports_large_baseline_and_enforces_bound(self):
        payload = b"x" * (1024 * 1024 + 10)

        class Response:
            def __enter__(self):
                return self

            def __exit__(self, *_args):
                return None

            def read(self, size):
                return payload[:size]

        api = gate.GitHubApi(REPOSITORY, "test-token")
        with patch.object(gate.urllib.request, "urlopen", return_value=Response()) as urlopen:
            actual = api.request_raw(
                "/contents/baseline.json?ref=" + BASE,
                limit=len(payload),
            )
            self.assertEqual(payload, actual)
        request = urlopen.call_args.args[0]
        self.assertEqual("application/vnd.github.raw+json", request.get_header("Accept"))

        class OversizedResponse(Response):
            def read(self, size):
                return payload[:size] + b"x"

        with patch.object(gate.urllib.request, "urlopen", return_value=OversizedResponse()), self.assertRaises(gate.GateError):
            api.request_raw("/contents/baseline.json?ref=" + BASE, limit=len(payload))


class WorkflowContractTests(unittest.TestCase):
    def test_scanner_is_read_only_and_publisher_uses_trusted_default_branch(self):
        scanner = (ROOT / ".github/workflows/qodana.yml").read_text()
        publisher = (ROOT / ".github/workflows/qodana-publisher.yml").read_text()
        self.assertIn("name: Qodana Community .NET (scan)", scanner)
        self.assertIn("ref: ${{ github.event.pull_request.head.sha || github.sha }}", scanner)
        self.assertIn("pull_request:", scanner)
        self.assertIn("workflow_run:", publisher)
        self.assertIn("ref: main", publisher)
        self.assertIn("QODANA_ARTIFACT_DOWNLOAD_OUTCOME", publisher)
        self.assertIn("if: always()", publisher)
        for workflow in (scanner, publisher):
            for action in re.findall(r"(?m)^\s*uses:\s+(\S+)", workflow):
                self.assertRegex(action, r"^[\w/-]+@[0-9a-f]{40}$")
        self.assertNotRegex(scanner, r"(?m)^\s+(?:contents|issues|pull-requests|statuses): write")
        self.assertNotRegex(scanner, r"secrets[.\[]|pull_request_target")
        self.assertNotIn("pull_request.head.repo.full_name", publisher)

    def test_required_ruleset_context_matches_published_commit_status(self):
        ruleset = json.loads((ROOT / "docs/internal/main-ruleset-required-status-checks.json").read_text())
        checks = next(rule["parameters"]["required_status_checks"]
                      for rule in ruleset["rules"] if rule["type"] == "required_status_checks")
        self.assertIn({"context": "qodana"}, checks)


class PublisherIntegrationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.artifacts = self.root / "artifact"
        self.artifacts.mkdir()
        self.sarif = sarif()
        self.baseline = baseline()
        evidence = {
            "schema_version": 1,
            "status": "completed",
            "repository": REPOSITORY,
            "pull_request_number": 7,
            "head_sha": HEAD,
            "base_sha": BASE,
            "base_analyzed_sha": BASE,
            "analyzed_sha": HEAD,
            "run_id": "123",
            "run_attempt": "2",
            "event": "pull_request",
            "configured_image": gate.EXPECTED_IMAGE,
            "configured_profile": gate.EXPECTED_PROFILE,
            "configured_solution": gate.EXPECTED_SOLUTION,
            "image_id": "sha256:" + "c" * 64,
            "scans": [{
                "label": label, "status": "completed", "exit_code": 0,
                "sarif_sha256": hashlib.sha256(self.sarif).hexdigest(),
                "sarif_bytes": len(self.sarif),
            } for label in ("base", "cold")],
        }
        (self.artifacts / "evidence.json").write_text(json.dumps(evidence))
        (self.artifacts / "cold.sarif.json").write_bytes(self.sarif)
        (self.artifacts / "base.sarif.json").write_bytes(self.sarif)
        self.event_path = self.root / "event.json"
        self.event_path.write_text(json.dumps({"workflow_run": {
            "id": 123,
            "run_attempt": 2,
            "event": "pull_request",
            "head_sha": "f" * 40,
            "conclusion": "success",
            "html_url": "https://github.com/owner/repository/actions/runs/123",
            "pull_requests": [{"number": 7, "head": {"sha": HEAD}, "base": {"sha": BASE}}],
        }}))
        self.api = _FakeGitHubApi(self.baseline)
        environ = {
            "GITHUB_REPOSITORY": REPOSITORY,
            "GITHUB_TOKEN": "test-token",
            "GITHUB_EVENT_PATH": str(self.event_path),
            "GITHUB_STEP_SUMMARY": str(self.root / "summary.md"),
            "QODANA_ARTIFACT_DIR": str(self.artifacts),
            "QODANA_ARTIFACT_DOWNLOAD_OUTCOME": "success",
        }
        self.environment_patch = patch.dict(os.environ, environ, clear=True)
        self.environment_patch.start()
        self.addCleanup(self.environment_patch.stop)
        self.api_patch = patch.object(gate, "GitHubApi", return_value=self.api)
        self.api_patch.start()
        self.addCleanup(self.api_patch.stop)

    def test_successful_run_publishes_one_comment_and_exact_qodana_status(self):
        self.assertEqual(0, gate.publish())
        statuses = [body for method, path, body in self.api.calls
                    if method == "POST" and path.startswith("/statuses/")]
        self.assertEqual(["error", "success"], [status["state"] for status in statuses])
        self.assertTrue(all(status["context"] == "qodana" for status in statuses))
        self.assertEqual(HEAD, next(path.rsplit("/", 1)[1]
                                    for method, path, _ in self.api.calls
                                    if method == "POST" and path.startswith("/statuses/")))
        comment = next(body["body"] for method, path, body in self.api.calls
                       if method == "POST" and path == "/issues/7/comments")
        self.assertIn(gate.REPORT_MARKER, comment)
        self.assertIn("Qodana Community .NET — PASS", comment)

    def test_baseline_refresh_uses_exact_base_source_scan_but_does_not_approve_new_findings(self):
        self.api.baseline = baseline([finding(path="src/Old.cs")])
        candidate = gate.build_baseline(
            self.sarif, analyzed_sha=BASE, image=gate.EXPECTED_IMAGE
        )
        self.api.candidate_baseline = (
            json.dumps(candidate, indent=2, sort_keys=True) + "\n"
        ).encode()
        self.api.files = [{"filename": gate.BASELINE_PATH, "patch": "@@ baseline refresh"}]

        self.assertEqual(0, gate.publish())
        comment = next(body["body"] for method, path, body in self.api.calls
                       if method == "POST" and path == "/issues/7/comments")
        self.assertIn("applies only after merge", comment)
        raw_revisions = [path.rsplit("ref=", 1)[1]
                         for method, path, _ in self.api.calls if method == "GET_RAW"]
        self.assertEqual([HEAD, BASE], raw_revisions)

    def test_baseline_refresh_mixed_with_source_changes_fails_closed(self):
        self.api.files = [
            {"filename": gate.BASELINE_PATH, "patch": "@@ baseline refresh"},
            {"filename": "src/Example.cs", "patch": "@@ source change"},
        ]
        self.assertEqual(1, gate.publish())
        statuses = [body for method, path, body in self.api.calls
                    if method == "POST" and path.startswith("/statuses/")]
        self.assertEqual(["error", "failure"], [status["state"] for status in statuses])
        comment = next(body["body"] for method, path, body in self.api.calls
                       if method == "POST" and path == "/issues/7/comments")
        self.assertIn("baseline-only PR", comment)
        self.assertNotIn("GET_RAW", [method for method, _, _ in self.api.calls])

    def test_missing_artifact_publishes_failure_instead_of_success(self):
        self.api.artifacts = []
        self.assertEqual(1, gate.publish())
        status = [body for method, path, body in self.api.calls
                  if method == "POST" and path.startswith("/statuses/")][-1]
        self.assertEqual("failure", status["state"])

    def test_report_publication_error_keeps_provisional_failure_status(self):
        with patch.object(gate, "_upsert_comment", side_effect=gate.GateError("comment unavailable")), self.assertRaises(gate.GateError):
            gate.publish()
        statuses = [body for method, path, body in self.api.calls
                    if method == "POST" and path.startswith("/statuses/")]
        self.assertEqual(["error"], [status["state"] for status in statuses])

    def test_stale_head_or_base_cannot_publish_success(self):
        changed = json.loads(self.event_path.read_text())
        changed["workflow_run"]["pull_requests"][0]["base"]["sha"] = "d" * 40
        self.event_path.write_text(json.dumps(changed))
        self.assertEqual(1, gate.publish())
        status = [body for method, path, body in self.api.calls
                  if method == "POST" and path.startswith("/statuses/")][-1]
        self.assertEqual("error", status["state"])
        self.assertNotIn("/contents/tools/qodana/baseline.json", [
            path for _, path, _ in self.api.calls
        ])

    def test_stale_scan_status_is_never_written_to_new_unscanned_head(self):
        self.api.current_head = "c" * 40

        self.assertEqual(1, gate.publish())
        status_paths = [path for method, path, _ in self.api.calls
                        if method == "POST" and path.startswith("/statuses/")]
        self.assertEqual([f"/statuses/{HEAD}", f"/statuses/{HEAD}"], status_paths)
        self.assertNotIn(f"/statuses/{self.api.current_head}", status_paths)

    def test_report_comment_is_updated_without_creating_duplicates(self):
        self.api.comments = [
            {"id": 41, "body": gate.REPORT_MARKER + " old", "user": {"type": "Bot", "login": "github-actions[bot]"}},
            {"id": 42, "body": gate.REPORT_MARKER + " newer", "user": {"type": "Bot", "login": "github-actions[bot]"}},
        ]
        gate._upsert_comment(self.api, 7, gate.REPORT_MARKER + " updated")
        self.assertIn(("DELETE", "/issues/comments/41", None), self.api.calls)
        self.assertIn(("PATCH", "/issues/comments/42", {"body": gate.REPORT_MARKER + " updated"}),
                      self.api.calls)
        self.assertFalse(any(method == "POST" and path == "/issues/7/comments"
                             for method, path, _ in self.api.calls))


class _FakeGitHubApi:
    def __init__(self, baseline_bytes):
        self.baseline = baseline_bytes
        self.current_head = HEAD
        self.candidate_baseline = None
        self.files = []
        self.artifacts = [{"name": "qodana-evidence-123-2", "expired": False}]
        self.comments = []
        self.calls = []

    def request(self, path, *, method="GET", body=None):
        self.calls.append((method, path, body))
        if path == "/actions/runs/123":
            return {
                "id": 123, "run_attempt": 2, "event": "pull_request",
                "path": gate.EXPECTED_WORKFLOW, "head_sha": "f" * 40,
                "repository": {"full_name": REPOSITORY},
            }
        if path == "/pulls/7":
            return {"number": 7, "state": "open", "head": {"sha": self.current_head},
                    "base": {"sha": BASE}}
        if path.startswith("/pulls/7/files?"):
            return self.files
        if path == "/actions/runs/123/artifacts?per_page=100":
            return {"artifacts": self.artifacts}
        if path == "/issues/7/comments?per_page=100&page=1":
            return self.comments
        if method == "POST" and path == "/issues/7/comments":
            self.comments.append({"id": 50, "body": body["body"], "user": {"type": "Bot", "login": "github-actions[bot]"}})
            return {"id": 50}
        return {}

    def request_raw(self, path, *, limit):
        self.calls.append(("GET_RAW", path, None))
        if path.endswith(f"ref={HEAD}") and self.candidate_baseline is not None:
            return self.candidate_baseline
        if path.endswith(f"ref={BASE}"):
            return self.baseline
        raise gate.GateError("unexpected baseline revision")


if __name__ == "__main__":
    unittest.main()
