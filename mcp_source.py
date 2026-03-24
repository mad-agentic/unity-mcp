#!/usr/bin/env python3
"""
Helper to switch the Unity MCP package source in a Unity project's Packages/manifest.json.
Useful for switching between upstream and local repos while developing.

Usage:
  python mcp_source.py [--manifest /abs/path/to/manifest.json] [--repo /abs/path/to/unity-mcp] [--choice 1|2|3|4]
"""

from __future__ import annotations

import argparse
import json
import pathlib
import subprocess
import sys

PKG_NAME = "com.madagent.unity-mcp"
BRIDGE_SUBPATH = "MCPForUnity"


def run_git(repo: pathlib.Path, *args: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(repo), *args], capture_output=True, text=True
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or f"git {' '.join(args)} failed")
    return result.stdout.strip()


def normalize_origin_to_https(url: str) -> str:
    """Map common SSH origin forms to https for Unity's git URL scheme."""
    if url.startswith("git@github.com:"):
        owner_repo = url.split(":", 1)[1]
        if owner_repo.endswith(".git"):
            owner_repo = owner_repo[:-4]
        return f"https://github.com/{owner_repo}.git"
    return url


def detect_repo_root(explicit: str | None) -> pathlib.Path:
    if explicit:
        return pathlib.Path(explicit).resolve()
    here = pathlib.Path(__file__).resolve().parent
    try:
        top = run_git(here, "rev-parse", "--show-toplevel")
        return pathlib.Path(top)
    except Exception:
        return here


def detect_branch(repo: pathlib.Path) -> str:
    return run_git(repo, "rev-parse", "--abbrev-ref", "HEAD")


def detect_origin(repo: pathlib.Path) -> str:
    url = run_git(repo, "remote", "get-url", "origin")
    return normalize_origin_to_https(url)


def find_manifest(explicit: str | None) -> pathlib.Path:
    if explicit:
        return pathlib.Path(explicit).resolve()
    cur = pathlib.Path.cwd().resolve()
    for parent in [cur, *cur.parents]:
        candidate = parent / "Packages" / "manifest.json"
        if candidate.exists():
            return candidate
    raise FileNotFoundError(
        "Could not find Packages/manifest.json from current directory. Use --manifest to specify a path."
    )


def read_json(path: pathlib.Path) -> dict:
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def write_json(path: pathlib.Path, data: dict) -> None:
    with path.open("w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


def build_options(repo_root: pathlib.Path, branch: str, origin_https: str):
    upstream_main = "https://github.com/mad-agent/unity-mcp.git?path=/MCPForUnity#main"
    upstream_beta = "https://github.com/mad-agent/unity-mcp.git?path=/MCPForUnity#beta"
    origin = origin_https
    if origin.startswith("file:"):
        origin_remote = upstream_main
    else:
        origin_remote = origin
    return [
        ("[1] Upstream main", upstream_main),
        ("[2] Upstream beta", upstream_beta),
        (f"[3] Remote {branch}", f"{origin_remote}?path=/{BRIDGE_SUBPATH}#{branch}"),
        (f"[4] Local {branch}", f"file:{(repo_root / BRIDGE_SUBPATH).as_posix()}"),
    ]


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Switch Unity MCP package source")
    p.add_argument("--manifest", help="Path to Packages/manifest.json")
    p.add_argument("--repo", help="Path to unity-mcp repo root (for local file option)")
    p.add_argument("--choice", choices=["1", "2", "3", "4"], help="Pick option non-interactively")
    return p.parse_args()


def main() -> None:
    args = parse_args()
    try:
        repo_root = detect_repo_root(args.repo)
        branch = detect_branch(repo_root)
        origin = detect_origin(repo_root)
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

    options = build_options(repo_root, branch, origin)

    try:
        manifest_path = find_manifest(args.manifest)
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

    print("Select Unity MCP package source by number:")
    for label, _ in options:
        print(label)

    if args.choice:
        choice = args.choice
    else:
        choice = input("Enter 1-4: ").strip()

    if choice not in {"1", "2", "3", "4"}:
        print("Invalid selection.", file=sys.stderr)
        sys.exit(1)

    idx = int(choice) - 1
    _, chosen = options[idx]

    data = read_json(manifest_path)
    deps = data.get("dependencies", {})

    if PKG_NAME not in deps:
        print(f"Error: '{PKG_NAME}' not found in manifest dependencies.", file=sys.stderr)
        sys.exit(1)

    print(f"\nUpdating {PKG_NAME} -> {chosen}")
    deps[PKG_NAME] = chosen
    data["dependencies"] = deps
    write_json(manifest_path, data)
    print(f"Done. Wrote to: {manifest_path}")
    print("Tip: In Unity, open Package Manager and Refresh to re-resolve packages.")


if __name__ == "__main__":
    main()
