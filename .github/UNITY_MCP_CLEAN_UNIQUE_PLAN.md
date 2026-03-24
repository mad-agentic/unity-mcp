# Unity MCP — Kế Hoạch Clean & Đặc Biệt (30 ngày)

## 1) Mục tiêu chiến lược

Xây dựng Unity MCP theo mô hình **Clean Core + Signature Layer**:

- **Clean Core**: dễ bảo trì, ổn định, testable, có chuẩn lỗi và chuẩn vận hành rõ ràng.
- **Signature Layer**: khác biệt sản phẩm (khó sao chép), tạo lợi thế nhận diện và giá trị người dùng.

## 2) Kết quả kỳ vọng sau 30 ngày

### 2.1 Clean
- Chuẩn hóa tool taxonomy và capability levels.
- Chuẩn hóa long-running jobs (`pending/status/result`) cho nhóm tác vụ lâu.
- Chuẩn hóa error contract (mã lỗi, thông điệp, hướng xử lý).
- Thiết lập bộ “golden workflows” có test hồi quy.

### 2.2 Đặc biệt
- Triển khai vòng lặp **AI Safety + Verification Loop** cho tác vụ API/scene/script nhạy cảm.
- Triển khai `batch_execute` có guardrail (giới hạn số lệnh, fail-fast, rollback mềm).
- Ra mắt hero capability: **Scene Auto-Repair** (phát hiện lỗi scene và gợi ý/sửa nhanh).

## 3) Nguyên tắc thiết kế (để luôn clean)

1. **Một tool = một trách nhiệm chính**.
2. **Không thêm tool mới nếu chưa có test + docs recipe**.
3. **Workflow completeness > số lượng tool**.
4. **Ưu tiên backward-compatible**; breaking change cần migration note.
5. **Mọi lỗi phải actionable**: nêu nguyên nhân + cách sửa + bước tiếp theo.

## 4) Kiến trúc mục tiêu

## 4.1 Clean Core
- Tool groups theo domain + maturity:
  - `core`
  - `advanced`
  - `experimental`
- Chuẩn envelope cho response:
  - `status`
  - `message`
  - `data`
  - `error` (nếu có)
  - `next_actions` (khuyến nghị)

## 4.2 Signature Layer
- **Verification Loop**:
  - Trước khi thao tác script/scene phức tạp: xác thực context dự án + API liên quan + trạng thái editor.
- **Batch with Guardrail**:
  - `max_commands_per_batch`
  - chiến lược dừng khi lỗi
  - rollback mềm (best-effort) + log rõ phần thành công/thất bại.
- **Scene Auto-Repair**:
  - phát hiện missing script/reference phổ biến.
  - chế độ `audit` (không ghi) và `repair` (có ghi).

## 5) Roadmap 4 phase

## Phase 1 (Tuần 1): Foundation Clean Core

Mục tiêu:
- Chốt chuẩn taxonomy, job state, error contract.

Đầu ra:
- Tài liệu chuẩn naming/tool group.
- Spec long-running jobs.
- Spec error code matrix.

Điều kiện hoàn thành:
- Có checklist chuẩn hóa được review nội bộ.
- Có baseline test cho tool hiện tại.

## Phase 2 (Tuần 2): Developer Experience Clean

Mục tiêu:
- Rút ngắn setup và giảm friction cho người dùng mới.

Đầu ra:
- Luồng setup một lệnh.
- Cơ chế bật/tắt tool groups runtime.
- Bộ docs 3 tầng (Quick Start, Recipes, Reference).

Điều kiện hoàn thành:
- Time-to-first-success giảm rõ rệt.
- Onboarding không cần xử lý thủ công phức tạp.

## Phase 3 (Tuần 3): Signature Features

Mục tiêu:
- Tạo điểm khác biệt định vị sản phẩm.

Đầu ra:
- Verification Loop khả dụng.
- `batch_execute` guardrail khả dụng.
- Scene Auto-Repair bản đầu.

Điều kiện hoàn thành:
- Demo end-to-end trên 2 kịch bản thực tế.
- Có metrics thành công/thất bại minh bạch.

## Phase 4 (Tuần 4): Ecosystem & Positioning

Mục tiêu:
- Đóng gói giá trị sản phẩm + sẵn sàng scale.

Đầu ra:
- CLI bootstrap + skill sync.
- Bộ benchmark hiệu năng/chất lượng.
- Tuyên bố định vị sản phẩm nhất quán trong docs.

Điều kiện hoàn thành:
- Có báo cáo benchmark nội bộ.
- Có release notes rõ điểm clean + unique.

## 6) KPI theo dõi

- **Reliability**:
  - Tool success rate.
  - Tỷ lệ retry thành công.
- **DX**:
  - Time-to-first-success.
  - Số bước setup trung bình.
- **Quality**:
  - Tỷ lệ lỗi có actionable message.
  - Số regression trên golden workflows.
- **Differentiation**:
  - Tần suất dùng Verification Loop / Auto-Repair / Batch guardrail.

## 7) Rủi ro & giảm thiểu

- **Scope creep**: khóa scope theo phase, chỉ mở khi đạt DoD phase trước.
- **Feature phức tạp gây nợ kỹ thuật**: bắt buộc test + docs trước merge.
- **Phá compatibility**: thêm migration notes + fallback behavior.
- **Hiệu năng giảm khi thêm guardrail**: benchmark trước/sau, giới hạn cấu hình hợp lý.

## 8) Definition of Done (DoD) toàn chương trình

Hoàn thành khi:
- 100% hạng mục phase có test tương ứng.
- Docs và task recipes khớp hành vi thực tế.
- Không có blocker P0/P1 tồn tại ở core workflows.
- Có bản tổng kết kết quả KPI + kế hoạch vòng tiếp theo.

## 9) Định hướng vòng kế tiếp (sau 30 ngày)

- Mở rộng hero capability sang package/build/test automation sâu hơn.
- Tăng tự động hóa quality gates trong CI.
- Mở các vertical tools theo ưu tiên người dùng thực tế.
