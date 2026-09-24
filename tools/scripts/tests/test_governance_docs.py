"""Render documentation and exercise its shell orchestration, not fake CLI semantics."""

from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from pathlib import Path

import pytest
import yaml

ROOT = Path(__file__).resolve().parents[3]
DOCS = ROOT / "docs"
PAGES = (
    "cli/index.md", "contracts/index.md", "guides/single-tool-workflow.md",
    "guides/upgrading.md", "guides/extended-governance-adoption.md",
    "guides/migration-baselines.md", "guides/sarif-integration.md",
    "guides/history-forensics.md", "usage/timings.md",
    "installation/index.md", "reference/repository-metrics.md",
)


def text(page: str) -> str:
    return (DOCS / page).read_text(encoding="utf-8")


def blocks(page: str, language: str) -> list[str]:
    return re.findall(rf"(?m)^```{language}\n(.*?)^```", text(page), re.S)


@pytest.mark.parametrize("page,kind", [("cli/index.md", "cli-command"), ("contracts/index.md", "contract-family")])
def test_catalog_renders_every_inventory_row(page: str, kind: str) -> None:
    import markdown
    from xml.etree import ElementTree

    rendered = markdown.markdown(text(page), extensions=["tables", "fenced_code"])
    # Only parse the first table; the rest of the page need not be XML.
    table = re.search(r"<table>.*?</table>", rendered, re.S)
    assert table is not None
    rows = ElementTree.fromstring(table.group()).findall("./tbody/tr")
    markers = re.findall(rf"<!-- {kind}: (.*?) -->", text(page))
    assert len(markers) == len(set(markers))
    assert len(rows) == len(markers)
    expected_cells = 2 if kind == "cli-command" else 3
    assert all(len(row.findall("td")) == expected_cells for row in rows)


def test_site_excludes_internal_agent_workflows(tmp_path: Path) -> None:
    result = subprocess.run(
        [sys.executable, "tools/scripts/filter_mkdocs_warnings.py", "--",
         "mkdocs", "build", "--strict", "--site-dir", str(tmp_path / "site")],
        cwd=ROOT, capture_output=True, text=True, timeout=90,
    )
    assert result.returncode == 0, result.stdout + result.stderr
    site = tmp_path / "site"
    assert (site / "cli/index.html").is_file()
    assert (site / "guides/history-forensics/index.html").is_file()
    for name in ("feature-implementation-workflow", "release-preparation-workflow"):
        assert not list(site.rglob(f"{name}*"))
        search_index = site / "search/search_index.json"
        assert name not in search_index.read_text(encoding="utf-8")


@pytest.mark.parametrize("page", PAGES)
def test_structured_examples_parse_and_shell_has_valid_syntax(page: str) -> None:
    for block in blocks(page, "json"):
        json.loads(block)
    for block in blocks(page, "yaml"):
        yaml.safe_load(block)
    for block in blocks(page, "bash"):
        result = subprocess.run(["bash", "-n"], input=block, capture_output=True, text=True)
        assert result.returncode == 0, result.stderr


def test_documented_reuse_options_are_registered_in_source() -> None:
    pairs = {
        "src/ArchLinterNet.Cli/Commands/Health/Application/HealthCommandDefinition.cs": ["--change-snapshot"],
        "src/ArchLinterNet.Cli/ArchitectureAnalysisCommandSupport.cs": ["--use-prepared-receipts"],
        "src/ArchLinterNet.Cli/Commands/Validate/Application/ValidateCommandDefinition.cs": ["--publish-prepared-receipts"],
    }
    public = text("cli/index.md") + text("usage/timings.md")
    for path, flags in pairs.items():
        source = (ROOT / path).read_text(encoding="utf-8")
        for flag in flags:
            assert f'new("{flag}")' in source
            assert flag in public


def test_internal_exclusions_and_new_navigation_are_configured() -> None:
    import pathspec

    config = yaml.safe_load((ROOT / "mkdocs.yml").read_text(encoding="utf-8"))
    excluded = pathspec.GitIgnoreSpec.from_lines(config["exclude_docs"].splitlines())
    assert excluded.match_file("internal/example.md")
    assert excluded.match_file("ai/feature-implementation-workflow.md")
    assert excluded.match_file("ai/release-preparation-workflow.md")
    assert not excluded.match_file("ai/policy-authoring-guide.md")
    nav = json.dumps(config["nav"])
    assert "guides/history-forensics.md" in nav
    assert "guides/sarif-integration.md" in nav


def run_git(repo: Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", str(repo), *args], text=True).strip()


MOCK_CLI = r'''#!/usr/bin/env -S python3 -S
import json, os, subprocess, sys
from pathlib import Path
args = sys.argv[1:]
def option(name):
    return args[args.index(name) + 1]
def write(name, value):
    Path(option(name)).write_text(json.dumps(value))
if '--help' in args:
    print('--change-snapshot' if os.environ['NEW_CLI'] == '1' else 'older health')
    sys.exit(0)
if '--version' in args:
    print('test-double, not a packed CLI')
    sys.exit(0)
revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
with open(os.environ['CALLS'], 'a') as log:
    log.write(json.dumps({'args': args, 'revision': revision}) + '\n')
if args[:2] == ['policy', 'context']:
    print(json.dumps({'revision': revision}))
elif args[:2] == ['change', 'snapshot']:
    write('--output', {'revision': revision})
elif args[0] == 'health':
    if '--change-snapshot' in args and os.environ.get('NO_SNAPSHOT') != '1':
        write('--change-snapshot', {'revision': revision})
    print(json.dumps({'revision': revision, 'gate_exit': int(os.environ['HEALTH_EXIT'])}))
    sys.exit(int(os.environ['HEALTH_EXIT']))
elif args[:2] == ['change', 'report']:
    base = json.loads(Path(option('--base')).read_text())
    current = json.loads(Path(option('--current')).read_text())
    assert base['revision'] != current['revision']
    write('--output', {'base': base, 'current': current})
elif args[:2] == ['report', 'pr']:
    Path(option('--output')).write_text('orchestration fixture only')
elif args[:2] == ['badge', 'architecture-health']:
    write('--output', {'fixture': True})
    sys.exit(int(os.environ['HEALTH_EXIT']))
else:
    sys.exit('unexpected mock command: ' + str(args))
'''

MOCK_DOTNET = r'''#!/usr/bin/env -S python3 -S
import os, shutil, sys
from pathlib import Path
args = sys.argv[1:]
assert args[:3] == ['tool', 'install', 'ArchLinterNet.Cli']
assert args[args.index('--version') + 1] == '0.9.0-preview.1'
destination = Path(args[args.index('--tool-path') + 1])
destination.mkdir(parents=True)
shutil.copy2(os.environ['MOCK_CLI'], destination / 'arch-linter-net')
'''


@pytest.fixture
def review_environment(tmp_path: Path) -> tuple[Path, dict[str, str]]:
    repo = tmp_path / "consumer with spaces"
    repo.mkdir()
    run_git(repo, "init")
    run_git(repo, "config", "user.email", "fixture@example.invalid")
    run_git(repo, "config", "user.name", "Documentation fixture")
    (repo / ".config").mkdir()
    (repo / ".config/dotnet-tools.json").write_text(json.dumps({
        "tools": {"archlinternet.cli": {"version": "0.9.0-preview.1"}}
    }))
    run_git(repo, "add", ".")
    run_git(repo, "commit", "-m", "base")
    base = run_git(repo, "rev-parse", "HEAD")
    (repo / "candidate.txt").write_text("different candidate tree")
    run_git(repo, "add", ".")
    run_git(repo, "commit", "-m", "candidate")
    binary = tmp_path / "bin"
    binary.mkdir()
    for name, contents in (("mock-cli", MOCK_CLI), ("dotnet", MOCK_DOTNET)):
        path = binary / name
        path.write_text(contents, encoding="utf-8")
        path.chmod(0o755)
    env = dict(os.environ, PATH=f"{binary}{os.pathsep}{os.environ['PATH']}",
               BASE_SHA=base, HEAD_SHA=run_git(repo, "rev-parse", "HEAD"),
               MOCK_CLI=str(binary / "mock-cli"), CALLS=str(tmp_path / "calls.jsonl"),
               NEW_CLI="1", HEALTH_EXIT="0")
    return repo, env


def execute_review(repo: Path, env: dict[str, str]) -> subprocess.CompletedProcess[str]:
    recipe = text("guides/single-tool-workflow.md").split("<!-- example: governance-review -->", 1)[1]
    script = re.search(r"```bash\n(.*?)\n```", recipe, re.S)
    assert script is not None
    return subprocess.run(["bash"], input=script.group(1), cwd=repo, env=env,
                          capture_output=True, text=True, timeout=30)


@pytest.mark.parametrize("new_cli", ["0", "1"])
@pytest.mark.parametrize("health_exit", [0, 1, 2])
def test_review_uses_exact_revisions_and_preserves_gate(review_environment, new_cli, health_exit) -> None:
    repo, env = review_environment
    env.update(NEW_CLI=new_cli, HEALTH_EXIT=str(health_exit))
    result = execute_review(repo, env)
    assert result.returncode == health_exit, result.stdout + result.stderr
    calls = [json.loads(line) for line in Path(env["CALLS"]).read_text().splitlines()]
    snapshots = [c for c in calls if c["args"][:2] == ["change", "snapshot"]]
    assert snapshots[0]["revision"] == env["BASE_SHA"]
    assert len(snapshots) == (1 if new_cli == "1" else 2)
    health = [c for c in calls if c["args"][0] == "health"]
    assert len(health) == 1 and health[0]["revision"] == env["HEAD_SHA"]
    assert ("--change-snapshot" in health[0]["args"]) == (new_cli == "1")
    assert run_git(repo, "worktree", "list", "--porcelain").count("worktree ") == 1


def test_missing_current_snapshot_does_not_render_a_false_comparison(review_environment) -> None:
    repo, env = review_environment
    env.update(HEALTH_EXIT="2", NO_SNAPSHOT="1")
    result = execute_review(repo, env)
    assert result.returncode == 2
    calls = [json.loads(line)["args"] for line in Path(env["CALLS"]).read_text().splitlines()]
    assert not any(c[:2] == ["change", "report"] for c in calls)
    assert (repo / "artifacts/architecture-review/health.json").is_file()


def test_identical_revisions_are_rejected_before_tool_install(review_environment) -> None:
    repo, env = review_environment
    env["BASE_SHA"] = env["HEAD_SHA"]
    assert execute_review(repo, env).returncode == 2
    assert not Path(env["CALLS"]).exists()
