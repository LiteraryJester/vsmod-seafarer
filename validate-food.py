#!/usr/bin/env python3
"""Convenience wrapper: runs `validate-assets.py --type food --verbose`."""
import sys
from vs_validators import run

if __name__ == "__main__":
    sys.exit(run(["--type", "food", "--verbose", *sys.argv[1:]]))
