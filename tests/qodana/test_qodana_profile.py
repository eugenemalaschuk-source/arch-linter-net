"""Contract tests for the repository's reviewed Qodana severity calibration."""

import re
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class QodanaProfileTests(unittest.TestCase):
    def test_reviewed_inspections_are_editorconfig_suggestions(self):
        editorconfig = (ROOT / ".editorconfig").read_text()
        overrides = re.findall(
            r"(?m)^(resharper_[a-z0-9_]+_highlighting)=suggestion$", editorconfig
        )
        self.assertEqual((45, 45), (len(overrides), len(set(overrides))))
        self.assertIn("resharper_access_to_disposed_closure_highlighting=suggestion", editorconfig)
        self.assertIn(
            "resharper_condition_is_always_true_or_false_according_to_"
            "nullable_api_contract_highlighting=suggestion",
            editorconfig,
        )
        self.assertIn("resharper_redundant_using_directive_highlighting=suggestion", editorconfig)


if __name__ == "__main__":
    unittest.main()
