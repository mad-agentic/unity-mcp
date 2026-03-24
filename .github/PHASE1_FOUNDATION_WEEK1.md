# Phase 1 — Foundation Week 1 (Implementation Tracker)

Status: Closed (2026-03-24)

## Mục tiêu tuần

- Chuẩn hóa taxonomy metadata cho tools.
- Chuẩn hóa response/error/job contract ở mức server foundation.
- Thiết lập baseline tests cho các contract mới.

## Đã triển khai trong đợt này

### 1) Taxonomy foundation
- Thêm maturity levels vào constants:
  - `core`, `advanced`, `experimental`
- Mở rộng registry decorator `mcp_for_unity_tool(..., maturity=...)`.
- Thêm `get_tool_taxonomy()` để snapshot cấu trúc tools theo `group` và `maturity`.
- Áp dụng taxonomy metadata cho đăng ký tool mới mà vẫn backward-compatible với tool hiện tại.

### 2) Contract foundation
- Thêm `Server/src/core/error_codes.py`:
  - Catalog mã lỗi chuẩn (`E_*`, `W_*`).
- Thêm `Server/src/core/response_contract.py`:
  - `success_response`, `error_response`, `pending_response`.
- Thêm `Server/src/core/job_contract.py`:
  - `JobState`, `serialize_job_state`, `is_terminal_job_state`.
- Thêm `execute_tool_with_contract(...)` trong `services/tools/utils.py` để chuẩn hóa kết quả tool call.

### 3) Config consistency
- Đồng bộ default HTTP port về `8080` trong `constants.py` để khớp docs/test hiện tại.

### 4) Baseline tests
- Mở rộng `test_core.py` cho maturity levels.
- Mở rộng `test_registry.py` cho maturity + taxonomy.
- Thêm `test_phase1_foundation.py` cho response/error/job contracts.
- Thêm `tests/tools/test_phase1_core_workflow.py` cho golden workflow và pending/error mapping.

## Còn lại trong Week 1

- [x] Chuẩn hóa error mapping vào các tool wrappers chính (`manage_scene`, `manage_gameobject`, `manage_asset`, `manage_script`, `manage_components`).
- [x] Chọn 1 workflow core để áp dụng `pending/status/result` end-to-end ở mức server contract.
- [x] Viết thêm test theo workflow (không chỉ test unit contract).
- [x] Chốt tài liệu error matrix (P0/P1) trong docs/reference.
- [x] Chuyển phần rollout toàn bộ wrappers sang backlog Week 2 (không phải blocker Week 1).

## Definition of Done (Week 1)

- [x] Taxonomy metadata có thể truy vấn đầy đủ cho tất cả tools.
- [x] Contract modules được dùng ít nhất ở 1 luồng tool thực tế.
- [x] Baseline tests pass ổn định.
