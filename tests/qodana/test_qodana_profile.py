"""Contract tests for the repository's reviewed Qodana severity calibration."""

import re
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class QodanaProfileTests(unittest.TestCase):
    def test_reviewed_inspections_are_editorconfig_suggestions_in_their_scopes(self):
        editorconfig = (ROOT / ".editorconfig").read_text()
        sections = {}
        current_section = None
        for raw_line in editorconfig.splitlines():
            line = raw_line.strip()
            if line.startswith("[") and line.endswith("]"):
                current_section = line[1:-1]
                sections[current_section] = {}
            elif current_section and "=" in line and not line.startswith("#"):
                key, value = (part.strip() for part in line.split("=", 1))
                sections[current_section][key] = value

        global_overrides = {
            key: value
            for key, value in sections["*.cs"].items()
            if key.startswith("resharper_") and key.endswith("_highlighting")
        }
        test_overrides = {
            key: value
            for key, value in sections["tests/**/*.cs"].items()
            if key.startswith("resharper_") and key.endswith("_highlighting")
        }
        disposed = "resharper_access_to_disposed_closure_highlighting"
        modified = "resharper_access_to_modified_closure_highlighting"

        self.assertEqual(43, len(global_overrides))
        nullable_api_contract = (
            "resharper_condition_is_always_true_or_false_according_to_"
            "nullable_api_contract_highlighting"
        )
        self.assertEqual(
            "suggestion",
            global_overrides[nullable_api_contract],
        )
        self.assertEqual(
            "suggestion",
            global_overrides["resharper_redundant_using_directive_highlighting"],
        )
        self.assertNotIn(disposed, global_overrides)
        self.assertNotIn(modified, global_overrides)
        self.assertEqual({disposed: "suggestion", modified: "suggestion"}, test_overrides)

        all_overrides = re.findall(
            r"(?m)^(resharper_[a-z0-9_]+_highlighting)=suggestion$", editorconfig
        )
        self.assertEqual((45, 45), (len(all_overrides), len(set(all_overrides))))


if __name__ == "__main__":
    unittest.main()
