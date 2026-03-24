"""Week 4 benchmark harness.

Measures:
- bootstrap latency
- setup doctor latency
- regression test duration and success
"""

from __future__ import annotations

import json
import subprocess
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "Server"
OUT = ROOT / "docs" / "development" / "WEEK4_BENCHMARK_REPORT.json"


def run_cmd(args: list[str], cwd: Path) -> tuple[int, str, float]:
    started = time.perf_counter()
    proc = subprocess.run(
        args,
        cwd=str(cwd),
        text=True,
        capture_output=True,
        check=False,
    )
    duration = time.perf_counter() - started
    output = (proc.stdout or "") + ("\n" + proc.stderr if proc.stderr else "")
    return proc.returncode, output, duration


def main() -> int:
    results: dict[str, object] = {
        "generated_at": int(time.time()),
        "kpi": {},
        "commands": {},
    }

    bootstrap_cmd = [
        "uv",
        "run",
        "unity-mcp",
        "--bootstrap",
        "--client",
        "vscode-copilot",
        "--check-connection",
    ]
    bootstrap_code, bootstrap_output, bootstrap_seconds = run_cmd(bootstrap_cmd, SERVER)
    results["commands"]["bootstrap"] = {
        "exit_code": bootstrap_code,
        "duration_seconds": round(bootstrap_seconds, 3),
    }

    doctor_cmd = ["uv", "run", "unity-mcp", "--doctor", "--client", "vscode-copilot"]
    doctor_code, doctor_output, doctor_seconds = run_cmd(doctor_cmd, SERVER)
    results["commands"]["doctor"] = {
        "exit_code": doctor_code,
        "duration_seconds": round(doctor_seconds, 3),
    }

    regression_cmd = ["uv", "run", "--with", "pytest", "pytest", "tests/", "-v"]
    test_code, test_output, test_seconds = run_cmd(regression_cmd, SERVER)
    results["commands"]["regression"] = {
        "exit_code": test_code,
        "duration_seconds": round(test_seconds, 3),
    }

    success_count = sum(1 for key in ("bootstrap", "doctor", "regression") if results["commands"][key]["exit_code"] == 0)
    total_count = 3

    results["kpi"] = {
        "setup_time_seconds": round(bootstrap_seconds, 3),
        "doctor_time_seconds": round(doctor_seconds, 3),
        "regression_time_seconds": round(test_seconds, 3),
        "success_rate": round(success_count / total_count, 4),
    }

    results["notes"] = {
        "bootstrap_excerpt": bootstrap_output[:1000],
        "doctor_excerpt": doctor_output[:1000],
        "regression_excerpt": test_output[:1000],
    }

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"Benchmark report written to: {OUT}")

    return 0 if success_count == total_count else 1


if __name__ == "__main__":
    raise SystemExit(main())
