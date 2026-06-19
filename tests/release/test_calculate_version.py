import unittest
import sys
from io import StringIO
from unittest.mock import patch

import tempfile
from pathlib import Path

from tools.release.calculate_version import (
    DetectedTag,
    SemVerVersion,
    detect_latest_tag,
    detect_latest_detected_tag,
    calculate_next_version,
    validate_package_version,
    main,
    _strip_v,
    _latest_by_semver,
)


class TestSemVerVersion(unittest.TestCase):

    def test_try_parse_stable(self):
        v = SemVerVersion.try_parse("v0.1.0")
        self.assertIsNotNone(v)
        self.assertEqual((0, 1, 0, None), (v.major, v.minor, v.patch, v.preview))
        self.assertTrue(v.is_stable)

    def test_try_parse_stable_without_v(self):
        v = SemVerVersion.try_parse("0.1.0")
        self.assertIsNotNone(v)

    def test_try_parse_preview(self):
        v = SemVerVersion.try_parse("v0.1.1-preview.2")
        self.assertIsNotNone(v)
        self.assertEqual((0, 1, 1, 2), (v.major, v.minor, v.patch, v.preview))
        self.assertFalse(v.is_stable)

    def test_try_parse_invalid(self):
        self.assertIsNone(SemVerVersion.try_parse("not-a-version"))
        self.assertIsNone(SemVerVersion.try_parse("v1.2"))
        self.assertIsNone(SemVerVersion.try_parse("v1.2.3.4"))
        self.assertIsNone(SemVerVersion.try_parse("v1.2.3-rc.1"))
        self.assertIsNone(SemVerVersion.try_parse("v1.2.3-preview"))
        self.assertIsNone(SemVerVersion.try_parse(""))

    def test_str_stable(self):
        self.assertEqual("0.1.0", str(SemVerVersion(0, 1, 0)))

    def test_str_preview(self):
        self.assertEqual("0.1.1-preview.2", str(SemVerVersion(0, 1, 1, 2)))

    def test_increment_preview_from_preview(self):
        v = SemVerVersion(0, 1, 1, 2)
        next_v = v.increment_preview()
        self.assertEqual((0, 1, 1, 3), (next_v.major, next_v.minor, next_v.patch, next_v.preview))

    def test_increment_preview_from_stable(self):
        v = SemVerVersion(0, 1, 0)
        next_v = v.increment_preview()
        self.assertEqual((0, 1, 1, 1), (next_v.major, next_v.minor, next_v.patch, next_v.preview))

    def test_increment_patch_from_stable(self):
        v = SemVerVersion(0, 1, 0)
        next_v = v.increment_patch()
        self.assertEqual((0, 1, 1, None), (next_v.major, next_v.minor, next_v.patch, next_v.preview))
        self.assertTrue(next_v.is_stable)

    def test_increment_patch_from_preview_finalizes(self):
        v = SemVerVersion(0, 1, 1, 2)
        next_v = v.increment_patch()
        self.assertEqual((0, 1, 1, None), (next_v.major, next_v.minor, next_v.patch, next_v.preview))
        self.assertTrue(next_v.is_stable)

    def test_increment_minor(self):
        v = SemVerVersion(0, 1, 0)
        next_v = v.increment_minor()
        self.assertEqual((0, 2, 0, None), (next_v.major, next_v.minor, next_v.patch, next_v.preview))

    def test_increment_major(self):
        v = SemVerVersion(0, 1, 0)
        next_v = v.increment_major()
        self.assertEqual((1, 0, 0, None), (next_v.major, next_v.minor, next_v.patch, next_v.preview))


class TestSemVerComparison(unittest.TestCase):

    def test_stable_higher_than_preview_same_version(self):
        tags = [
            SemVerVersion.try_parse("v0.1.0-preview.3"),
            SemVerVersion.try_parse("v0.1.0"),
        ]
        latest = _latest_by_semver(tags)
        self.assertEqual("0.1.0", str(latest))

    def test_latest_stable_by_semver_not_lexicographic(self):
        tags = [
            SemVerVersion.try_parse("v0.9.0"),
            SemVerVersion.try_parse("v0.10.0"),
        ]
        latest = _latest_by_semver(tags)
        self.assertEqual("0.10.0", str(latest))

    def test_latest_preview_by_numeric_order(self):
        tags = [
            SemVerVersion.try_parse("v0.1.0-preview.9"),
            SemVerVersion.try_parse("v0.1.0-preview.10"),
        ]
        latest = _latest_by_semver(tags)
        self.assertEqual("0.1.0-preview.10", str(latest))

    def test_ignores_non_semver_tags(self):
        tags = [
            SemVerVersion.try_parse("v0.1.0"),
        ]
        latest = _latest_by_semver(tags)
        self.assertEqual("0.1.0", str(latest))


class TestCalculateNextVersion(unittest.TestCase):

    scenarios = [
        ("v0.1.1-preview.2", "preview", "0.1.1-preview.3"),
        ("v0.1.0", "preview", "0.1.1-preview.1"),
        ("v0.1.1-preview.2", "patch", "0.1.1"),
        ("v0.1.0", "patch", "0.1.1"),
        ("v0.1.0", "minor", "0.2.0"),
        ("v0.1.0", "major", "1.0.0"),
        ("v0.1.1-preview.2", "minor", "0.2.0"),
        ("v0.1.1-preview.2", "major", "1.0.0"),
    ]

    def test_scenarios(self):
        for tag_str, release_type, expected in self.scenarios:
            with self.subTest(tag=tag_str, type=release_type):
                tag = SemVerVersion.try_parse(tag_str)
                self.assertIsNotNone(tag, f"Could not parse tag: {tag_str}")
                result = calculate_next_version(tag, release_type)
                self.assertEqual(expected, str(result))

    def test_no_tag_raises_error(self):
        with self.assertRaises(ValueError) as ctx:
            calculate_next_version(None, "patch")
        self.assertIn("No valid SemVer tag found", str(ctx.exception))

    def test_unknown_release_type_raises_error(self):
        tag = SemVerVersion(0, 1, 0)
        with self.assertRaises(ValueError) as ctx:
            calculate_next_version(tag, "unknown")
        self.assertIn("Unknown release type", str(ctx.exception))


class TestValidatePackageVersion(unittest.TestCase):

    def test_valid_stable(self):
        validate_package_version("0.1.0")
        validate_package_version("10.99.999")

    def test_valid_preview(self):
        validate_package_version("0.1.0-preview.1")
        validate_package_version("0.1.0-alpha.1")

    def test_valid_with_build_metadata(self):
        validate_package_version("0.1.0+build.123")

    def test_empty_raises(self):
        with self.assertRaises(ValueError):
            validate_package_version("")

    def test_invalid_raises(self):
        with self.assertRaises(ValueError):
            validate_package_version("not-a-version")
        with self.assertRaises(ValueError):
            validate_package_version("v0.1.0")


class TestStripV(unittest.TestCase):

    def test_strips_v(self):
        self.assertEqual("0.1.0", _strip_v("v0.1.0"))

    def test_strips_capital_v(self):
        self.assertEqual("0.1.0", _strip_v("V0.1.0"))

    def test_no_v_unchanged(self):
        self.assertEqual("0.1.0", _strip_v("0.1.0"))


class TestMainCLI(unittest.TestCase):

    def test_override_produces_version(self):
        test_args = ["prog", "--version-override", "v0.2.0-rc.1"]
        with patch.object(sys, "argv", test_args):
            with patch("sys.stdout", new_callable=StringIO) as out:
                main()
                self.assertEqual("0.2.0-rc.1\n", out.getvalue())

    def test_override_rejects_invalid(self):
        test_args = ["prog", "--version-override", "not-a-version"]
        with patch.object(sys, "argv", test_args):
            with patch("sys.stderr", new_callable=StringIO) as err:
                with self.assertRaises(SystemExit) as ctx:
                    main()
                self.assertEqual(1, ctx.exception.code)
                self.assertIn("not a valid SemVer", err.getvalue())

    def test_no_tag_no_override_release_type_required(self):
        test_args = ["prog", "--release-type", "preview"]
        with patch.object(sys, "argv", test_args):
            with (
                patch(
                    "tools.release.calculate_version.detect_latest_tag",
                    return_value=None,
                ),
                patch("sys.stderr", new_callable=StringIO) as err,
            ):
                with self.assertRaises(SystemExit) as ctx:
                    main()
                self.assertEqual(1, ctx.exception.code)
                self.assertIn("No valid SemVer tag found", err.getvalue())

    def test_no_release_type_no_override_prints_help(self):
        test_args = ["prog"]
        with patch.object(sys, "argv", test_args):
            with patch("sys.stderr", new_callable=StringIO) as err:
                with self.assertRaises(SystemExit) as ctx:
                    main()
                self.assertEqual(1, ctx.exception.code)
                self.assertIn("required when", err.getvalue())

    def test_v_prefix_stripped_from_output(self):
        test_args = ["prog", "--version-override", "v0.1.0-preview.1"]
        with patch.object(sys, "argv", test_args):
            with patch("sys.stdout", new_callable=StringIO) as out:
                main()
                self.assertEqual("0.1.0-preview.1\n", out.getvalue())

    def test_stable_override_accepted(self):
        test_args = ["prog", "--version-override", "1.0.0"]
        with patch.object(sys, "argv", test_args):
            with patch("sys.stdout", new_callable=StringIO) as out:
                main()
                self.assertEqual("1.0.0\n", out.getvalue())


class TestDetectLatestTag(unittest.TestCase):

    @patch(
        "tools.release.calculate_version.subprocess.run",
    )
    def test_returns_latest_stable(self, mock_run):
        mock_run.return_value.stdout = "v0.0.9\nv0.1.0\n"
        mock_run.return_value.returncode = 0

        result = detect_latest_tag()
        self.assertIsNotNone(result)
        self.assertEqual("0.1.0", str(result))

    @patch(
        "tools.release.calculate_version.subprocess.run",
    )
    def test_returns_latest_preview(self, mock_run):
        mock_run.return_value.stdout = (
            "v0.1.0-preview.1\nv0.1.0-preview.2\nv0.0.9\n"
        )
        mock_run.return_value.returncode = 0

        result = detect_latest_tag()
        self.assertIsNotNone(result)
        self.assertEqual("0.1.0-preview.2", str(result))

    @patch(
        "tools.release.calculate_version.subprocess.run",
    )
    def test_stable_has_higher_precedence_than_preview(self, mock_run):
        mock_run.return_value.stdout = "v0.1.0\nv0.1.0-preview.3\n"
        mock_run.return_value.returncode = 0

        result = detect_latest_tag()
        self.assertIsNotNone(result)
        self.assertEqual("0.1.0", str(result))

    @patch(
        "tools.release.calculate_version.subprocess.run",
    )
    def test_non_semver_tags_ignored(self, mock_run):
        mock_run.return_value.stdout = (
            "v0.1.0\nrelease-candidate-1\nbuild-123\n"
        )
        mock_run.return_value.returncode = 0

        result = detect_latest_tag()
        self.assertIsNotNone(result)
        self.assertEqual("0.1.0", str(result))

    @patch(
        "tools.release.calculate_version.subprocess.run",
    )
    def test_no_tags_returns_none(self, mock_run):
        mock_run.return_value.stdout = ""
        mock_run.return_value.returncode = 0

        result = detect_latest_tag()
        self.assertIsNone(result)

    @patch(
        "tools.release.calculate_version.subprocess.run",
    )
    def test_numeric_preview_compared_as_numbers(self, mock_run):
        mock_run.return_value.stdout = (
            "v0.1.0-preview.9\nv0.1.0-preview.10\n"
        )
        mock_run.return_value.returncode = 0

        result = detect_latest_tag()
        self.assertIsNotNone(result)
        self.assertEqual("0.1.0-preview.10", str(result))


class TestGithubEnv(unittest.TestCase):

    def _run_with_env(self, args: list[str], detected: DetectedTag | None):
        env_file = tempfile.NamedTemporaryFile(mode="w", delete=False, suffix=".env")
        env_path = env_file.name
        env_file.close()

        try:
            test_args = ["prog"] + args + ["--github-env", env_path]
            with patch.object(sys, "argv", test_args):
                with (
                    patch(
                        "tools.release.calculate_version.detect_latest_detected_tag",
                        return_value=detected,
                    ),
                    patch("sys.stdout", new_callable=StringIO) as out,
                ):
                    main()
                    stdout = out.getvalue()

            with open(env_path) as f:
                content = f.read()
        finally:
            Path(env_path).unlink(missing_ok=True)

        return stdout, content

    def _parse_env(self, content: str) -> dict[str, str]:
        result: dict[str, str] = {}
        for line in content.strip().splitlines():
            if "=" in line:
                k, v = line.split("=", 1)
                result[k] = v
        return result

    def test_github_env_preview_exports_context(self):
        detected = DetectedTag("v0.2.0-preview.2", SemVerVersion(0, 2, 0, 2))
        stdout, content = self._run_with_env(
            ["--release-type", "preview"], detected
        )
        env = self._parse_env(content)
        self.assertEqual(env["PACKAGE_VERSION"], "0.2.0-preview.3")
        self.assertEqual(env["TARGET_TAG"], "v0.2.0-preview.3")
        self.assertEqual(env["PREVIOUS_TAG"], "v0.2.0-preview.2")
        self.assertIn("Calculated PACKAGE_VERSION=0.2.0-preview.3", stdout)
        self.assertIn("Target tag: v0.2.0-preview.3", stdout)
        self.assertIn("Previous tag: v0.2.0-preview.2", stdout)

    def test_github_env_stable_exports_previous_tag(self):
        detected = DetectedTag("v0.1.0", SemVerVersion(0, 1, 0))
        stdout, content = self._run_with_env(
            ["--release-type", "patch"], detected
        )
        env = self._parse_env(content)
        self.assertEqual(env["PACKAGE_VERSION"], "0.1.1")
        self.assertEqual(env["TARGET_TAG"], "v0.1.1")
        self.assertEqual(env["PREVIOUS_TAG"], "v0.1.0")
        self.assertIn("Target tag: v0.1.1", stdout)

    def test_github_env_override_detects_previous_tag(self):
        detected = DetectedTag("v0.2.0", SemVerVersion(0, 2, 0))
        stdout, content = self._run_with_env(
            ["--version-override", "0.3.0"], detected
        )
        env = self._parse_env(content)
        self.assertEqual(env["PACKAGE_VERSION"], "0.3.0")
        self.assertEqual(env["TARGET_TAG"], "v0.3.0")
        self.assertEqual(env["PREVIOUS_TAG"], "v0.2.0")
        self.assertIn("Target tag: v0.3.0", stdout)

    def test_github_env_override_no_tags_previous_empty(self):
        stdout, content = self._run_with_env(
            ["--version-override", "0.1.0"], None
        )
        env = self._parse_env(content)
        self.assertEqual(env["PACKAGE_VERSION"], "0.1.0")
        self.assertEqual(env["TARGET_TAG"], "v0.1.0")
        self.assertEqual(env["PREVIOUS_TAG"], "")
        self.assertIn("Previous tag: <none>", stdout)

    def test_github_env_writes_to_file(self):
        detected = DetectedTag("v0.1.0", SemVerVersion(0, 1, 0))
        stdout, content = self._run_with_env(
            ["--release-type", "minor"], detected
        )
        env = self._parse_env(content)
        self.assertIn("PACKAGE_VERSION", env)
        self.assertIn("TARGET_TAG", env)
        self.assertIn("PREVIOUS_TAG", env)
        self.assertNotEqual(stdout, "")


if __name__ == "__main__":
    unittest.main()
