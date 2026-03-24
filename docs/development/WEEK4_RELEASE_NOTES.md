# Week 4 Release Notes

Date: 2026-03-24

## Highlights
- Added CLI bootstrap flow via `unity-mcp --bootstrap`
- Added config artifact export via `--write-config`
- Added skill sync with mismatch diagnostics via `--sync-skills`
- Added benchmark harness script `tools/benchmark_week4.py`

## Why this matters
- Faster onboarding and lower setup friction
- Better visibility into config/metadata drift
- Repeatable KPI measurement path for release decisions

## Compatibility
- Backward compatible with existing `--doctor` behavior
- No breaking changes to existing tool wrappers

## Validation
- Full server test suite passed: `53 passed`
- Benchmark report artifact generated: `docs/development/WEEK4_BENCHMARK_REPORT.json`
