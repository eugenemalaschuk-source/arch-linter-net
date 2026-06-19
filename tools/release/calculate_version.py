import argparse
import re
import subprocess
import sys
from dataclasses import dataclass
from typing import Optional


@dataclass
class DetectedTag:
    name: str
    version: "SemVerVersion"


_SEMVER_TAG_RE = re.compile(
    r"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
    r"(?:-preview\.(0|[1-9]\d*))?$"
)

_NUGET_PACKAGE_VERSION_RE = re.compile(
    r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
    r"(-[0-9A-Za-z][0-9A-Za-z.-]*)?"
    r"(\+[0-9A-Za-z][0-9A-Za-z.-]*)?$"
)


@dataclass
class SemVerVersion:
    major: int
    minor: int
    patch: int
    preview: Optional[int] = None

    def __str__(self) -> str:
        base = f"{self.major}.{self.minor}.{self.patch}"
        if self.preview is not None:
            return f"{base}-preview.{self.preview}"
        return base

    @property
    def is_stable(self) -> bool:
        return self.preview is None

    @staticmethod
    def try_parse(text: str) -> Optional["SemVerVersion"]:
        m = _SEMVER_TAG_RE.match(text.strip())
        if not m:
            return None
        major = int(m.group(1))
        minor = int(m.group(2))
        patch = int(m.group(3))
        preview = int(m.group(4)) if m.group(4) else None
        return SemVerVersion(major, minor, patch, preview)

    def increment_preview(self) -> "SemVerVersion":
        if self.preview is not None:
            return SemVerVersion(self.major, self.minor, self.patch, self.preview + 1)
        return SemVerVersion(self.major, self.minor, self.patch + 1, 1)

    def increment_patch(self) -> "SemVerVersion":
        if self.preview is not None:
            return SemVerVersion(self.major, self.minor, self.patch)
        return SemVerVersion(self.major, self.minor, self.patch + 1)

    def increment_minor(self) -> "SemVerVersion":
        return SemVerVersion(self.major, self.minor + 1, 0)

    def increment_major(self) -> "SemVerVersion":
        return SemVerVersion(self.major + 1, 0, 0)


def _semver_sort_key(v: SemVerVersion):
    base = (v.major, v.minor, v.patch)
    if v.is_stable:
        return (base, (1,))
    return (base, (0, v.preview))


def _latest_by_semver(versions: list[SemVerVersion]) -> Optional[SemVerVersion]:
    if not versions:
        return None
    sorted_versions = sorted(versions, key=_semver_sort_key, reverse=True)
    return sorted_versions[0]


def detect_latest_detected_tag() -> Optional[DetectedTag]:
    try:
        result = subprocess.run(
            ["git", "tag"],
            capture_output=True,
            text=True,
            check=True,
        )
    except (subprocess.CalledProcessError, FileNotFoundError):
        return None

    detected_tags: list[DetectedTag] = []
    for line in result.stdout.splitlines():
        tag = line.strip()
        v = SemVerVersion.try_parse(tag)
        if v is not None:
            detected_tags.append(DetectedTag(tag, v))

    if not detected_tags:
        return None

    sorted_tags = sorted(
        detected_tags,
        key=lambda dt: _semver_sort_key(dt.version),
        reverse=True,
    )
    return sorted_tags[0]


def detect_latest_tag() -> Optional[SemVerVersion]:
    detected = detect_latest_detected_tag()
    return detected.version if detected is not None else None


def calculate_next_version(
    latest_tag: Optional[SemVerVersion],
    release_type: str,
) -> SemVerVersion:
    if latest_tag is None:
        raise ValueError(
            "No valid SemVer tag found. "
            "Use --version-override for the first release."
        )

    if release_type == "preview":
        return latest_tag.increment_preview()
    elif release_type == "patch":
        return latest_tag.increment_patch()
    elif release_type == "minor":
        return latest_tag.increment_minor()
    elif release_type == "major":
        return latest_tag.increment_major()
    else:
        raise ValueError(f"Unknown release type: {release_type}")


def validate_package_version(version: str) -> None:
    if not version:
        raise ValueError("Package version is required.")
    if not _NUGET_PACKAGE_VERSION_RE.match(version):
        raise ValueError(
            f"Package version '{version}' is not a valid SemVer-style NuGet version."
        )


def _strip_v(text: str) -> str:
    if text.startswith("v") or text.startswith("V"):
        return text[1:]
    return text


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Calculate the next NuGet package version from git tags."
    )
    parser.add_argument(
        "--release-type",
        choices=["preview", "patch", "minor", "major"],
        default=None,
        help="Release scenario for automatic version calculation",
    )
    parser.add_argument(
        "--version-override",
        default=None,
        help="Explicit version override (bypasses tag detection)",
    )
    parser.add_argument(
        "--github-env",
        default=None,
        help="Path to GitHub env file for key=value export "
             "(exports env file and prints summary; PACKAGE_VERSION, TARGET_TAG, PREVIOUS_TAG)",
    )

    args = parser.parse_args()

    if args.version_override:
        version = _strip_v(args.version_override)
        try:
            validate_package_version(version)
        except ValueError as e:
            print(f"Error: {e}", file=sys.stderr)
            sys.exit(1)
        result = version
    else:
        if not args.release_type:
            print(
                "Error: --release-type is required when --version-override is not provided.",
                file=sys.stderr,
            )
            sys.exit(1)

        latest = detect_latest_tag()
        try:
            next_version = calculate_next_version(latest, args.release_type)
        except ValueError as e:
            print(f"Error: {e}", file=sys.stderr)
            sys.exit(1)

        result = str(next_version)
        validate_package_version(result)

    if args.github_env:
        detected = detect_latest_detected_tag()
        target_tag = f"v{result}"
        previous_tag = detected.name if detected is not None else ""
        with open(args.github_env, "a") as f:
            f.write(f"PACKAGE_VERSION={result}\n")
            f.write(f"TARGET_TAG={target_tag}\n")
            f.write(f"PREVIOUS_TAG={previous_tag}\n")
        previous_display = previous_tag if previous_tag else "<none>"
        print(f"Calculated PACKAGE_VERSION={result}")
        print(f"Target tag: {target_tag}")
        print(f"Previous tag: {previous_display}")
    else:
        print(result)


if __name__ == "__main__":
    main()
