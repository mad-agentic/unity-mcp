"""Phase 1 foundation contract tests (response, errors, jobs)."""

from core.error_codes import ERROR_CATALOG, ERROR_INTERNAL, ERROR_TIMEOUT
from core.job_contract import JobState, is_terminal_job_state, serialize_job_state
from core.response_contract import error_response, pending_response, success_response


def test_success_response_contract():
    response = success_response(
        tool="manage_scene",
        data={"scene": "SampleScene"},
        message="Scene loaded",
        next_actions=["Run save"],
    )
    assert response["status"] == "success"
    assert response["tool"] == "manage_scene"
    assert response["data"]["scene"] == "SampleScene"
    assert response["error"] is None
    assert response["next_actions"] == ["Run save"]


def test_error_response_contract():
    response = error_response(
        tool="manage_scene",
        code=ERROR_TIMEOUT,
        message="Timed out",
        retryable=True,
    )
    assert response["status"] == "error"
    assert response["tool"] == "manage_scene"
    assert response["error"]["code"] == ERROR_TIMEOUT
    assert response["error"]["retryable"] is True


def test_pending_response_contract():
    response = pending_response(
        tool="manage_packages",
        job_id="job-123",
        poll_after_seconds=2.5,
    )
    assert response["status"] == "pending"
    assert response["meta"]["job_id"] == "job-123"
    assert response["meta"]["poll_after_seconds"] == 2.5


def test_job_state_serialization_and_terminals():
    state = JobState(
        job_id="job-456",
        tool="manage_build",
        status="running",
        progress=0.4,
        message="Building",
    )
    payload = serialize_job_state(state)
    assert payload["job_id"] == "job-456"
    assert payload["status"] == "running"
    assert is_terminal_job_state("running") is False
    assert is_terminal_job_state("succeeded") is True


def test_error_catalog_contains_core_codes():
    assert ERROR_INTERNAL in ERROR_CATALOG
    assert ERROR_TIMEOUT in ERROR_CATALOG
