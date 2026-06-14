#!/usr/bin/env python3
"""Configure this repository to use the tracked hooks in githooks/."""

from __future__ import annotations

import os
import stat
import subprocess
import sys
from pathlib import Path


def run_git(*args: str, cwd: Path) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=cwd,
        check=False,
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        message = result.stderr.strip() or result.stdout.strip() or "git command failed"
        raise RuntimeError(message)
    return result.stdout.strip()


def main() -> int:
    try:
        repo_root = Path(run_git("rev-parse", "--show-toplevel", cwd=Path.cwd())).resolve()
    except RuntimeError as error:
        print(f"Error: {error}", file=sys.stderr)
        print("This script must be run inside a git repository.", file=sys.stderr)
        return 1

    hooks_path = repo_root / "githooks"
    required_hooks = ["pre-commit", "pre-push"]
    for hook_name in required_hooks:
        hook_path = hooks_path / hook_name
        if not hook_path.is_file():
            print(f"Error: missing tracked hook at '{hook_path}'.", file=sys.stderr)
            return 1

    run_git("config", "core.hooksPath", "githooks", cwd=repo_root)

    if os.name != "nt":
        for hook_name in required_hooks:
            hook_path = hooks_path / hook_name
            hook_path.chmod(
                hook_path.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH
            )

    print(f"Installed git hooks from '{hooks_path}'.")
    print("  pre-commit: dotnet dotnet-csharpier (staged .cs files)")
    print("  pre-push:   dotnet run --project tools/LicenseCollector -- --check")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
