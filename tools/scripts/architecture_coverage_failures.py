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


def _append_text_part(parts: list[str], label: str, value: str | None) -> None:
    if value:
        parts.append(f"{label} {_code(value)}")


def _append_subject_part(parts: list[str], subject: str | None, source: str | None) -> None:
    if subject and subject != source:
        parts.append(f"subject {_code(subject)}")


def _append_item_part(parts: list[str], finding: dict) -> None:
    item = _as_text(finding.get("item"))
    if item:
        scope = _as_text(finding.get("scope"))
        parts.append(f"{f'{scope} item' if scope else 'item'} {_code(item)}")


def _append_references_part(parts: list[str], finding: dict) -> None:
    references = _format_references(finding.get("forbidden_references"))
    if references:
        parts.append(f"forbidden references {references}")


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


def _append_location_parts(parts: list[str], finding: dict) -> None:
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


def _diagnostic_summary(finding: dict) -> str:
    parts: list[str] = []
    source = _as_text(finding.get("source"))
    _append_text_part(parts, "code", _as_text(finding.get("message_code")))
    _append_text_part(parts, "source", source)
    _append_subject_part(parts, _as_text(finding.get("subject")), source)
    _append_text_part(parts, "state", _as_text(finding.get("state")))
    _append_item_part(parts, finding)
    _append_text_part(parts, "forbidden namespace", _as_text(finding.get("forbidden_namespace")))
    _append_references_part(parts, finding)

    for field, label in (("evidence", "evidence"), ("reason", "reason"), ("detail", "detail")):
        _append_text_part(parts, label, _as_text(finding.get(field)))

    _append_location_parts(parts, finding)

    return "; ".join(parts) if parts else "structured diagnostic emitted without detail fields"


def _rule_identity(finding: dict, category: str) -> tuple[str, str]:
    contract_id = _as_text(finding.get("contract_id"))
    contract_name = _as_text(finding.get("contract"))
    if contract_id:
        return contract_id, contract_name or contract_id
    if contract_name:
        return contract_name, contract_name
    return category.lower().replace(" ", "-"), category


def _coverage_bucket_fallback(entry: dict, bucket: str, state: str) -> list[dict]:
    items = entry.get(bucket, []) or []
    if not isinstance(items, list):
        return []

    return [
        {
            "contract_id": entry.get("contract_id"),
            "contract": entry.get("contract"),
            "scope": entry.get("scope"),
            "state": state,
            "item": item.get("item"),
            "evidence": item.get("evidence") or item.get("reason"),
        }
        for item in items
        if isinstance(item, dict)
    ]


def _coverage_entry_fallback(entry: dict) -> list[dict]:
    findings: list[dict] = []
    for bucket, state in COVERAGE_FALLBACK_BUCKETS:
        findings.extend(_coverage_bucket_fallback(entry, bucket, state))
    return findings


def _coverage_summary_fallback(report: dict) -> list[dict]:
    if report.get("coverage_findings"):
        return []

    return [
        finding
        for entry in report.get("coverage_summary", []) or []
        if isinstance(entry, dict)
        for finding in _coverage_entry_fallback(entry)
    ]


def _collection_findings(report: dict, collection: str) -> list[object]:
    if collection == "coverage_summary_fallback":
        return _coverage_summary_fallback(report)
    return report.get(collection, []) or []


def _add_collection_findings(
    grouped: dict[str, tuple[str, set[tuple[str, str]]]],
    findings: list[object],
    collection: str,
    category: str,
) -> None:
    for finding in findings:
        if not isinstance(finding, dict):
            continue
        if collection == "preflight_diagnostics" and finding.get("state") == "current":
            continue
        identifier, name = _rule_identity(finding, category)
        existing_name, diagnostics = grouped.setdefault(identifier, (name, set()))
        if existing_name == identifier and name != identifier:
            grouped[identifier] = (name, diagnostics)
        diagnostics.add((category, _diagnostic_summary(finding)))


def collect_failed_rules(report: dict) -> list[FailedRule]:
    grouped: dict[str, tuple[str, set[tuple[str, str]]]] = {}
    collections = (*FAILURE_COLLECTIONS, ("coverage_summary_fallback", "Coverage summary"))
    for collection, category in collections:
        _add_collection_findings(
            grouped, _collection_findings(report, collection), collection, category
        )

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
