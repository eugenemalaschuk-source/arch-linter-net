from __future__ import annotations

from dataclasses import dataclass

FAILURE_COLLECTIONS = (
    ("violations", "Violation"),
    ("coverage_findings", "Coverage"),
    ("cycle_diagnostics", "Cycle"),
    ("unmatched_ignored_violations", "Stale baseline ignore"),
    ("policy_consistency_findings", "Policy consistency"),
    ("preflight_diagnostics", "Build-state preflight"),
    ("classification_conflicts", "Classification conflict"),
    ("classification_metadata_failures", "Classification metadata"),
)

COVERAGE_FALLBACK_BUCKETS = (
    ("uncovered_items", "uncovered"),
    ("stale_items", "stale"),
    ("unknown_items", "unknown"),
)


@dataclass(frozen=True)
class FailedRuleDiagnostic:
    category: str
    summary: str


@dataclass(frozen=True)
class FailedRule:
    identifier: str
    name: str
    diagnostics: tuple[FailedRuleDiagnostic, ...]


def _as_text(value: object) -> str | None:
    if isinstance(value, str):
        return value
    if isinstance(value, int | float):
        return str(value)
    return None


def _compact(value: object) -> str:
    text = " ".join(str(value).split())
    return text if len(text) <= 240 else f"{text[:237]}..."


def _code(value: object) -> str:
    cleaned = _compact(value).replace("`", "'")
    return f"`{cleaned}`"


def _format_location(value: object) -> str | None:
    if not isinstance(value, dict):
        return _as_text(value)

    path = _as_text(value.get("path"))
    if path is None:
        return None

    location = path
    line = _as_text(value.get("line"))
    column = _as_text(value.get("column"))
    if line is not None:
        location = f"{location}:{line}"
    if column is not None:
        location = f"{location}:{column}"
    return location


def _format_references(value: object) -> str | None:
    if not isinstance(value, list):
        return None

    references = [_as_text(item) for item in value]
    present = [_code(reference) for reference in references if reference]
    return ", ".join(present) if present else None


def _diagnostic_summary(finding: dict) -> str:
    parts: list[str] = []
    message_code = _as_text(finding.get("message_code"))
    if message_code:
        parts.append(f"code {_code(message_code)}")

    source = _as_text(finding.get("source"))
    if source:
        parts.append(f"source {_code(source)}")

    subject = _as_text(finding.get("subject"))
    if subject and subject != source:
        parts.append(f"subject {_code(subject)}")

    state = _as_text(finding.get("state"))
    if state:
        parts.append(f"state {_code(state)}")

    scope = _as_text(finding.get("scope"))
    item = _as_text(finding.get("item"))
    if item:
        label = f"{scope} item" if scope else "item"
        parts.append(f"{label} {_code(item)}")

    forbidden_namespace = _as_text(finding.get("forbidden_namespace"))
    if forbidden_namespace:
        parts.append(f"forbidden namespace {_code(forbidden_namespace)}")

    references = _format_references(finding.get("forbidden_references"))
    if references:
        parts.append(f"forbidden references {references}")

    for field, label in (("evidence", "evidence"), ("reason", "reason"), ("detail", "detail")):
        value = _as_text(finding.get(field))
        if value:
            parts.append(f"{label} {_code(value)}")

    seen_locations: set[str] = set()
    for field, label in (
        ("source_location", "source"),
        ("policy_origin", "policy"),
        ("policy_location", "policy"),
    ):
        location = _format_location(finding.get(field))
        if location and location not in seen_locations:
            parts.append(f"{label} {_code(location)}")
            seen_locations.add(location)

    return "; ".join(parts) if parts else "structured diagnostic emitted without detail fields"


def _rule_identity(finding: dict, category: str) -> tuple[str, str]:
    contract_id = _as_text(finding.get("contract_id"))
    contract_name = _as_text(finding.get("contract"))
    if contract_id:
        return contract_id, contract_name or contract_id
    if contract_name:
        return contract_name, contract_name
    return category.lower().replace(" ", "-"), category


def _coverage_summary_fallback(report: dict) -> list[dict]:
    if report.get("coverage_findings"):
        return []

    findings: list[dict] = []
    for entry in report.get("coverage_summary", []) or []:
        if not isinstance(entry, dict):
            continue
        for bucket, state in COVERAGE_FALLBACK_BUCKETS:
            for item in entry.get(bucket, []) or []:
                if not isinstance(item, dict):
                    continue
                findings.append(
                    {
                        "contract_id": entry.get("contract_id"),
                        "contract": entry.get("contract"),
                        "scope": entry.get("scope"),
                        "state": state,
                        "item": item.get("item"),
                        "evidence": item.get("evidence") or item.get("reason"),
                    }
                )
    return findings


def collect_failed_rules(report: dict) -> list[FailedRule]:
    grouped: dict[str, tuple[str, set[tuple[str, str]]]] = {}
    collections = (*FAILURE_COLLECTIONS, ("coverage_summary_fallback", "Coverage summary"))
    for collection, category in collections:
        findings = _coverage_summary_fallback(report) if collection == "coverage_summary_fallback" else report.get(collection, []) or []
        for finding in findings:
            if not isinstance(finding, dict):
                continue
            identifier, name = _rule_identity(finding, category)
            existing_name, diagnostics = grouped.setdefault(identifier, (name, set()))
            if existing_name == identifier and name != identifier:
                grouped[identifier] = (name, diagnostics)
            diagnostics.add((category, _diagnostic_summary(finding)))

    rules = [
        FailedRule(
            identifier=identifier,
            name=name,
            diagnostics=tuple(
                FailedRuleDiagnostic(category=category, summary=summary)
                for category, summary in sorted(diagnostics)
            ),
        )
        for identifier, (name, diagnostics) in grouped.items()
    ]
    return sorted(rules, key=lambda rule: (rule.identifier, rule.name))


def render_failed_rules_section(
    rules: list[FailedRule], max_representative_diagnostics: int | None = None
) -> list[str]:
    lines = [f"### Failed rules ({len(rules)})", ""]
    if not rules:
        lines.append(
            "> **Unavailable:** strict validation failed without structured diagnostics; download the "
            "`architecture-strict` artifact for the raw result."
        )
        return lines

    for rule in rules:
        name_suffix = "" if rule.name == rule.identifier else f" — {_compact(rule.name)}"
        diagnostic_count = len(rule.diagnostics)
        noun = "diagnostic" if diagnostic_count == 1 else "diagnostics"
        lines.append(f"- **{_code(rule.identifier)}{name_suffix}** — {diagnostic_count} failed {noun}")
        representatives = rule.diagnostics[:max_representative_diagnostics]
        for diagnostic in representatives:
            lines.append(f"  - **{diagnostic.category}:** {diagnostic.summary}")
        omitted_count = diagnostic_count - len(representatives)
        if omitted_count > 0:
            omitted_noun = "diagnostic" if omitted_count == 1 else "diagnostics"
            lines.append(f"  - _{omitted_count} additional {omitted_noun} omitted._")

    return lines
