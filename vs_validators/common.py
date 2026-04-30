"""Shared helpers: JSON5 loading, colors, ValidationResult, path helpers."""
from __future__ import annotations

import json5
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


class C:
    RED    = "\033[91m"
    YELLOW = "\033[93m"
    GREEN  = "\033[92m"
    CYAN   = "\033[96m"
    DIM    = "\033[2m"
    BOLD   = "\033[1m"
    RESET  = "\033[0m"


def err(msg):    return f"{C.RED}ERROR{C.RESET} {msg}"
def warn(msg):   return f"{C.YELLOW}WARN {C.RESET} {msg}"
def ok(msg):     return f"{C.GREEN}OK   {C.RESET} {msg}"
def info(msg):   return f"{C.CYAN}INFO {C.RESET} {msg}"
def header(msg): return f"\n{C.BOLD}{'─' * 60}\n {msg}\n{'─' * 60}{C.RESET}"


def load_json5(path: Path) -> tuple[Any | None, str | None]:
    """Load a VS-style JSON5 file. Returns (data, error_string)."""
    try:
        with open(path, "r", encoding="utf-8-sig") as f:
            text = f.read()
        data = json5.loads(text)
        return data, None
    except Exception as e:
        return None, str(e)


def rel(path: Path, root: Path) -> str:
    """Short relative path for display, forward-slash normalised."""
    try:
        return str(Path(path).relative_to(root)).replace("\\", "/")
    except ValueError:
        return str(path)


@dataclass
class Finding:
    rule_id: str          # e.g. "food.pie", "asset.blocktypes", ""
    severity: str         # "error" | "warning"
    file: Path
    message: str


@dataclass
class ValidationResult:
    findings: list[Finding] = field(default_factory=list)
    passed: int = 0

    def error(self, file: Path, msg: str, rule_id: str = "") -> None:
        self.findings.append(Finding(rule_id, "error", file, msg))

    def warning(self, file: Path, msg: str, rule_id: str = "") -> None:
        self.findings.append(Finding(rule_id, "warning", file, msg))

    def ok(self) -> None:
        self.passed += 1

    @property
    def errors(self) -> list[tuple[Path, str]]:
        return [(f.file, f.message) for f in self.findings if f.severity == "error"]

    @property
    def warnings(self) -> list[tuple[Path, str]]:
        return [(f.file, f.message) for f in self.findings if f.severity == "warning"]

    @property
    def total(self) -> int:
        return self.passed + len(self.errors)
