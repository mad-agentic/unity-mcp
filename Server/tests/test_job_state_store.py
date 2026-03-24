"""Tests for persistent job state store (Phase 1)."""

from __future__ import annotations

from core.job_contract import JobState
from core.job_state_store import JobStateStore


def test_job_state_store_upsert_get_delete(tmp_path):
    store_file = tmp_path / "jobs.json"
    store = JobStateStore(file_path=str(store_file))

    state = JobState(job_id="job-1", tool="manage_build", status="running", progress=0.2)
    saved = store.upsert(state)

    assert saved["job_id"] == "job-1"
    loaded = store.get("job-1")
    assert loaded is not None
    assert loaded["status"] == "running"

    assert store.delete("job-1") is True
    assert store.get("job-1") is None


def test_job_state_store_list_by_tool(tmp_path):
    store_file = tmp_path / "jobs.json"
    store = JobStateStore(file_path=str(store_file))

    store.upsert(JobState(job_id="job-a", tool="manage_build", status="queued"))
    store.upsert(JobState(job_id="job-b", tool="manage_packages", status="running"))
    store.upsert(JobState(job_id="job-c", tool="manage_build", status="succeeded"))

    build_jobs = store.list_by_tool("manage_build")
    assert len(build_jobs) == 2
    assert {job["job_id"] for job in build_jobs} == {"job-a", "job-c"}
