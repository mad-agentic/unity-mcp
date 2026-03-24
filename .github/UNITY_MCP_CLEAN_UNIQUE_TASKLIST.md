# Unity MCP — Tasklist Chi Tiết 30 Ngày

> Bám theo kế hoạch tại `.github/UNITY_MCP_CLEAN_UNIQUE_PLAN.md`.
> Trạng thái khuyến nghị: `Todo` | `Doing` | `Done` | `Blocked`.

## Tuần 1 — Foundation Clean Core

> Cập nhật trạng thái ngày 2026-03-24:
> - Day 1–5: hoàn tất.
> - Day 6–7: hoàn tất, Week 1 đã đóng sổ.
> - Day 8–14: hoàn tất, Week 2 đã đóng sổ.
> - Day 15–21: hoàn tất, Week 3 đã đóng sổ.
> - Day 22–30: hoàn tất, Week 4 đã đóng sổ.

## Day 1: Chốt taxonomy và phạm vi tool groups
- [x] Liệt kê toàn bộ tools hiện có theo domain.
- [x] Gắn maturity level: `core` / `advanced` / `experimental`.
- [x] Tạo quy tắc naming và action naming thống nhất.
- [x] Review chéo với README + docs reference.

**Acceptance criteria**
- Có bảng mapping tool → group → maturity.
- Không còn tool “mồ côi” ngoài taxonomy.

## Day 2: Chuẩn response envelope
- [x] Định nghĩa schema thống nhất cho response thành công/thất bại.
- [x] Định nghĩa `next_actions` cho lỗi thường gặp.
- [x] Chuẩn hóa format giữa Python server và Unity plugin.

**Acceptance criteria**
- Tối thiểu 5 tool core trả response đúng schema chuẩn.

## Day 3: Chuẩn error contract
- [x] Thiết kế danh mục mã lỗi (`E_*`, `W_*`).
- [x] Mapping lỗi kỹ thuật → thông điệp user-facing.
- [x] Bổ sung retry hint/mitigation cho lỗi phổ biến.

**Acceptance criteria**
- Có error matrix docs + ví dụ thực tế.
- Lỗi P0/P1 có message actionable.

## Day 4: Thiết kế long-running jobs chuẩn
- [x] Chốt flow `start` → `pending` → `status` → `result`.
- [x] Chuẩn timeout/cancel/retry semantics.
- [x] Chuẩn lưu trạng thái job an toàn sau reload.

**Acceptance criteria**
- Spec đầy đủ cho package/build/test jobs.

## Day 5: Baseline tests cho core workflows
- [x] Xác định 3–5 golden workflows.
- [x] Viết test baseline cho workflow quan trọng nhất.
- [x] Tạo báo cáo baseline pass/fail.

**Acceptance criteria**
- Có baseline test chạy được trên môi trường local.

## Day 6–7: Hardening + rà soát tuần 1
- [x] Sửa các mismatch taxonomy/schema/error.
- [x] Dọn docs trùng lặp, giữ 1 nguồn sự thật.
- [x] Chốt biên bản tuần 1 + backlog tuần 2.

**Acceptance criteria**
- [x] Tuần 1 không còn blocker P0.

---

## Tuần 2 — Developer Experience Clean

## Day 8: One-command setup design
- [x] Thiết kế command kiểm tra dependency + generate config + verify kết nối.
- [x] Định nghĩa output log thân thiện.

**Acceptance criteria**
- Có runbook rõ cho Windows/macOS/Linux.

## Day 9: One-command setup implementation
- [x] Implement command setup đầu-cuối.
- [x] Handle edge cases path/env/version.

**Acceptance criteria**
- Setup thành công trong môi trường clean machine mô phỏng.

## Day 10: Runtime tool group toggles
- [x] Thiết kế giao diện bật/tắt tool groups.
- [x] Đồng bộ trạng thái toggle với server.

**Acceptance criteria**
- Bật/tắt có hiệu lực tức thời hoặc được hướng dẫn sync rõ ràng.

## Day 11: Quick Start 3 phút
- [x] Viết lại quick start ultra-short.
- [x] Thêm checklist xác nhận thành công đầu tiên.

**Acceptance criteria**
- Người mới hoàn thành flow đầu tiên trong ≤ 3 phút (mục tiêu nội bộ).

## Day 12: Recipes theo use-case
- [x] Viết 5 recipes phổ biến (scene, object, script, package, debug).
- [x] Mỗi recipe có input mẫu + output mong đợi.

**Acceptance criteria**
- Tất cả recipe chạy được với tool hiện có.

## Day 13–14: Reference cleanup
- [x] Chuẩn hóa tài liệu API/tool reference.
- [x] Đảm bảo docs khớp 100% với hành vi hiện tại.

**Acceptance criteria**
- Không còn endpoint/tool được docs nhưng không tồn tại (và ngược lại).

---

## Tuần 3 — Signature Features (Điểm đặc biệt)

## Day 15: Verification Loop spec
- [x] Thiết kế loop kiểm chứng trước hành động nhạy cảm.
- [x] Xác định trigger conditions và skip conditions.

**Acceptance criteria**
- Có sơ đồ decision flow + policy rõ ràng.

## Day 16: Verification Loop implementation
- [x] Implement preflight checks cho script/scene/API-sensitive tasks.
- [x] Ghi log lý do pass/fail verification.

**Acceptance criteria**
- Ít nhất 2 luồng nhạy cảm bắt buộc qua verification.

## Day 17: `batch_execute` guardrail spec
- [x] Thiết kế `max_commands_per_batch`, fail-fast, rollback mềm.
- [x] Định nghĩa format kết quả từng command con.

**Acceptance criteria**
- Có tài liệu behavior cho mọi trạng thái lỗi.

## Day 18: `batch_execute` guardrail implementation
- [x] Implement giới hạn, validate input, partial results.
- [x] Thêm test cho case thành công/thất bại hỗn hợp.

**Acceptance criteria**
- Batch chạy ổn định với kết quả minh bạch từng command.

## Day 19: Scene Auto-Repair (audit mode)
- [x] Implement quét missing scripts/references phổ biến.
- [x] Trả về báo cáo mức độ ưu tiên sửa.

**Acceptance criteria**
- Có report audit dùng được trên scene mẫu.

## Day 20: Scene Auto-Repair (repair mode)
- [x] Implement sửa tự động các lỗi an toàn.
- [x] Gắn chế độ dry-run trước khi ghi thay đổi.

**Acceptance criteria**
- Chạy audit + repair end-to-end với log rõ ràng.

## Day 21: Demo tuần 3 + tuning
- [x] Demo 2 kịch bản thực chiến.
- [x] Tối ưu thông điệp lỗi và fallback behavior.

**Acceptance criteria**
- Tính năng signature hoạt động ổn định ở kịch bản đã chọn.

---

## Tuần 4 — Ecosystem & Positioning

## Day 22: CLI bootstrap scope
- [x] Chốt các command tối thiểu cho bootstrap dự án.
- [x] Xác định interface với skills/config.

**Acceptance criteria**
- Có spec command + tham số rõ ràng.

## Day 23: CLI bootstrap implementation
- [x] Implement command cài + cấu hình + kiểm tra kết nối cơ bản.
- [x] Handle lỗi môi trường thường gặp.

**Acceptance criteria**
- Bootstrap chạy thành công ở repo hiện tại.

## Day 24: Skill sync flow
- [x] Thiết kế và implement sync skills theo client.
- [x] Bổ sung cảnh báo khi skill/config lệch phiên bản.

**Acceptance criteria**
- Sync có báo cáo trạng thái thành công/thất bại.

## Day 25: Benchmark harness
- [x] Chọn KPI benchmark (latency, success rate, setup time).
- [x] Viết script benchmark baseline vs sau cải tiến.

**Acceptance criteria**
- Có báo cáo benchmark tái chạy được.

## Day 26: Product positioning docs
- [x] Cập nhật thông điệp định vị “clean + reliable + unique”.
- [x] Viết release notes nhấn mạnh signature capabilities.

**Acceptance criteria**
- README và docs nhất quán thông điệp.

## Day 27: Regression sweep
- [x] Chạy full test matrix liên quan thay đổi.
- [x] Ghi nhận và xử lý regression còn lại.

**Acceptance criteria**
- Không còn regression blocker cho core workflows.

## Day 28: Hardening + fallback scenarios
- [x] Test các đường lỗi: timeout, retry, partial failure.
- [x] Củng cố log observability và troubleshooting.

**Acceptance criteria**
- Các lỗi phổ biến có đường thoát rõ ràng.

## Day 29: Go/No-Go review
- [x] Review KPI với mục tiêu ban đầu.
- [x] Chốt backlog hậu phát hành.

**Acceptance criteria**
- Có quyết định phát hành kèm lý do rõ ràng.

## Day 30: Release + tổng kết
- [x] Đóng gói tài liệu tổng kết 30 ngày.
- [x] Công bố kết quả KPI + định hướng vòng sau.

**Acceptance criteria**
- Release notes hoàn chỉnh, có dữ liệu đo lường.

---

## Mẫu theo dõi thực thi (copy cho sprint board)

| ID | Task | Owner | Priority | Status | ETA | Notes |
|----|------|-------|----------|--------|-----|-------|
| T-001 | Taxonomy mapping |  | P0 | Todo |  |  |
| T-002 | Response envelope |  | P0 | Todo |  |  |
| T-003 | Error contract |  | P0 | Todo |  |  |
| T-004 | Long-running jobs spec |  | P0 | Todo |  |  |
| T-005 | Golden workflow baseline tests |  | P0 | Todo |  |  |

## Danh sách kiểm tra cuối kỳ

- [x] Core workflows pass ổn định.
- [x] Signature features có demo minh chứng.
- [x] Docs khớp hành vi thực tế.
- [x] KPI được đo và lưu bản tổng kết.
