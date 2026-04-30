"""Shape/texture/patch/lang indexing for validators."""
from __future__ import annotations

import json5
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from vs_validators.common import load_json5


# ── Shape/texture index ────────────────────────────────────────────────────────

def build_file_index(root: Path, extension: str) -> set[str]:
    """Build a set of domain-relative paths (extension stripped) under root/extension."""
    index: set[str] = set()
    base = root / extension
    if not base.exists():
        return index
    for f in base.rglob("*"):
        if f.is_file():
            relpath = f.relative_to(base)
            no_ext = str(relpath).replace("\\", "/")
            no_ext = re.sub(r"\.[^.]+$", "", no_ext)
            index.add(no_ext)
    return index


# ── Patch model ────────────────────────────────────────────────────────────────

@dataclass
class PatchRef:
    source_file: Path
    op: str
    path: str
    file: str
    value: Any
    depends_on: list[dict] = field(default_factory=list)

    def depends_on_mod(self, modid: str) -> bool:
        for dep in self.depends_on:
            if isinstance(dep, dict) and dep.get("modid") == modid:
                return True
        return False

    def path_pattern(self) -> str:
        """Normalised path for matching (collapses numeric indexes to *)."""
        return re.sub(r"/\d+(?=/|$)", "/*", self.path)


def path_matches(actual: str, pattern: str) -> bool:
    """Glob-style match for JSON Patch paths. '*' matches a single segment."""
    a_parts = actual.strip("/").split("/")
    p_parts = pattern.strip("/").split("/")
    if len(a_parts) != len(p_parts):
        return False
    for a, p in zip(a_parts, p_parts):
        if p == "*":
            continue
        if a == p:
            continue
        return False
    return True


def load_patches(patches_dir: Path) -> list[PatchRef]:
    """Recursively load every *.json file under patches_dir and flatten into PatchRef list."""
    refs: list[PatchRef] = []
    if not patches_dir.exists():
        return refs
    for f in sorted(patches_dir.rglob("*.json")):
        data, error = load_json5(f)
        if error or data is None:
            continue
        entries = data if isinstance(data, list) else [data]
        for entry in entries:
            if not isinstance(entry, dict):
                continue
            refs.append(PatchRef(
                source_file=f,
                op=entry.get("op", ""),
                path=entry.get("path", ""),
                file=entry.get("file", ""),
                value=entry.get("value"),
                depends_on=entry.get("dependsOn", []) or [],
            ))
    return refs


def patches_by_target(refs: list[PatchRef]) -> dict[str, list[PatchRef]]:
    """Group patches by target 'file' field (e.g. 'seafarer:itemtypes/food/chili.json')."""
    out: dict[str, list[PatchRef]] = {}
    for r in refs:
        if not r.file:
            continue
        out.setdefault(r.file, []).append(r)
    return out


# ── Lang loader (handles VS embedded newlines) ────────────────────────────────

def load_lang_raw(path: Path) -> tuple[dict | None, str | None]:
    """Load a VS lang file that may have literal newlines inside string values."""
    with open(path, "r", encoding="utf-8-sig") as f:
        text = f.read()

    out: list[str] = []
    in_string = False
    i = 0
    while i < len(text):
        ch = text[i]
        if not in_string:
            if ch == "/" and i + 1 < len(text) and text[i + 1] == "/":
                while i < len(text) and text[i] != "\n":
                    i += 1
                continue
            if ch == '"':
                in_string = True
            out.append(ch)
        else:
            if ch == "\\" and i + 1 < len(text):
                out.append(ch)
                out.append(text[i + 1])
                i += 2
                continue
            elif ch == '"':
                in_string = False
                out.append(ch)
            elif ch == "\n":
                out.append("\\n")
            elif ch == "\r":
                pass
            elif ch == "\t":
                out.append("\\t")
            else:
                out.append(ch)
        i += 1

    cleaned = "".join(out)
    try:
        return json5.loads(cleaned), None
    except Exception as e:
        return None, str(e)
