#!/usr/bin/env python3
"""Filter out the mkdocs-material MkDocs 2.0 advisory banner from stderr."""
import sys
import re

# The warning block is ANSI-colored (ESC[31m) and ends with a line
# containing only color reset (ESC[0m). Filter all lines between
# the first occurrence and the reset line after it.
in_warning = False
for line in sys.stdin:
    if not in_warning and "\x1b[31m" in line and "Warning" in line:
        in_warning = True
    if in_warning:
        if line.strip() == "\x1b[0m":
            in_warning = False
        continue
    sys.stdout.write(line)
