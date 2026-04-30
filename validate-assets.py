#!/usr/bin/env python3
"""Thin CLI wrapper around vs_validators.run()."""
import sys
from vs_validators import run

if __name__ == "__main__":
    sys.exit(run())
