# 📋 SmartWash API Endpoints

Danh sách tất cả các endpoint của hệ thống SmartWash BE.

---

## 🔐 Auth

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | `register` | AuthController |
| POST | `login` | AuthController |
| POST | `verify-otp` | AuthController |
| POST | `resend-otp` | AuthController |
| POST | `refresh-token` | AuthController |
| POST | `change-password` | AuthController |
| POST | `logout` | AuthController |
| POST | `forgot-password` | AuthController |
| POST | `reset-password` | AuthController |

---

## 👤 User

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `me` | UserController |
| PUT | `me` | UserController |
| DELETE | `me` | UserController |

---

## 📅 Bookings (Customer)

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | `check-compatibility` | BookingsController |
| POST | `available-slots` | BookingsController |
| POST | `check-slots-with-suggestions` | BookingsController |
| POST | `{bookingId}/trigger-email` | BookingsController |
| POST | *(root)* | BookingsController |
| POST | `{id}/payment-link` | BookingsController |
| POST | `walk-in` | BookingsController |
| GET | `me` | BookingsController |
| GET | `user/{userId}` | BookingsController |
| GET | `{id}` | BookingsController |
| PUT | `{id}/cancel` | BookingsController |
| PUT | `{id}/reschedule` | BookingsController |
| PUT | `{id}/condition` | BookingsController |
| GET | `{id}/payment-status` | BookingsController |
| POST | `{id}/accept-relocation` | BookingsController |

---

## 🏢 Branches

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | BranchesController |

---

## 🏪 Business

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | `register` | BusinessController |
| GET | `my-profile` | BusinessController |
| POST | `admin/review-application` | BusinessController |
| GET | `admin/pending-applications` | BusinessController |
| GET | `admin/application/{businessProfileId}` | BusinessController |
| GET | `available-slots` | BusinessController |
| POST | `bookings` | BusinessController |
| PUT | `reschedule/{id}` | BusinessController |
| GET | `vehicles` | BusinessController |
| GET | `vehicles/status/all` | BusinessController |
| GET | `vehicles/status` | BusinessController |
| GET | *(root)* | BusinessController |
| GET | `{id}` | BusinessController |
| POST | `{id}/cancel` | BusinessController |
| GET | `invoice/{bookingId}` | BusinessController |
| GET | `history` | BusinessController |
| GET | `dashboard` | BusinessController |
| GET | `statements/monthly` | BusinessController |
| POST | `washlogs/{washLogId}/assign-lane` | BusinessController |
| GET | `invoices/{invoiceId}/export` | BusinessController |

---

## 📷 Camera

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | `check-in` | CameraController |
| POST | `check-out` | CameraController |

---

## 🚗 Car Models

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | CarModelsController |
| POST | *(root)* | CarModelsController |
| PUT | `{id}` | CarModelsController |
| DELETE | `{id}` | CarModelsController |
| POST | `request` | CarModelsController |
| GET | `~/api/v1/admin/carmodels/pending` | CarModelsController |
| PUT | `~/api/v1/admin/carmodels/{id}/approve` | CarModelsController |
| PUT | `~/api/v1/admin/carmodels/{id}/reject` | CarModelsController |

---

## 🚐 Fleet

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `template` | FleetController |
| POST | `import` | FleetController |
| GET | `pending` | FleetController |
| GET | `staff/pending/all` | FleetController |
| POST | `staff/approve/{id}` | FleetController |
| POST | `staff/reject/{id}` | FleetController |
| GET | `staff/imports` | FleetController |
| GET | `staff/imports/{batchId}` | FleetController |
| POST | `check-in` | FleetController |
| POST | `walk-in` | FleetController |
| POST | `walk-out/{washLogId}` | FleetController |
| POST | `{washLogId}/start-processing` | FleetController |
| GET | `current` | FleetController |
| GET | `queue` | FleetController |
| GET | `history` | FleetController |
| GET | `dashboard` | FleetController |
| POST | `checkout/{washLogId}` | FleetController |

---

## 🧴 Materials

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | MaterialsController |
| GET | `units` | MaterialsController |

---

## 🛠️ Services

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | ServicesController |
| GET | `{id}` | ServicesController |

---

## 🏅 Tier

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | TierController |
| POST | *(root)* | TierController |
| PUT | `{id}` | TierController |

---

## 🕐 Time Slots

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | TimeSlotsController |
| POST | *(root)* | TimeSlotsController |
| PUT | `{id}` | TimeSlotsController |
| DELETE | `{id}` | TimeSlotsController |

---

## 💳 Transaction

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `transactions` | TransactionController |
| GET | `points/history` | TransactionController |

---

## 🚘 Vehicle (Customer)

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | VehicleController |
| POST | *(root)* | VehicleController |
| PUT | `{licensePlate}` | VehicleController |
| DELETE | `{licensePlate}` | VehicleController |
| GET | `recognize/{licensePlate}` | VehicleController |

---

## 🎟️ Voucher (Customer)

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `me` | VoucherController |
| POST | `redeem` | VoucherController |

---

## 💰 Wallet

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `me` | WalletController |
| POST | `top-up` | WalletController |
| POST | `top-up/callback` | WalletController |

---

## 🧾 Invoice

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `invoices` | InvoiceController |
| GET | `invoices/{invoiceId}` | InvoiceController |
| GET | `invoices/{invoiceId}/pdf` | InvoiceController |
| POST | `billing/monthly` | InvoiceController |

---

## 💸 Payment

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | `payos/webhook` | PaymentController |

---

## 🤖 AI Chatbot

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | `chat` | AIChatBotController |
| GET | `recommendation` | AIChatBotController |

---

## 🚙 Vehicle Detection

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | `detect-plate` | VehicleDetectionController |
| POST | `detect-dual-plate` | VehicleDetectionController |
| POST | `car-recognize` | VehicleDetectionController |
| POST | `check-has-car` | VehicleDetectionController |

---

## 🤖 Automated Wash

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | `check-in` | AutomatedWashController |

---

## 👔 Manager

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `staff` | ManagerController |
| GET | `lanes` | ManagerController |
| GET | `lanes/{laneId}/staff` | ManagerController |
| GET | `timeslots` | ManagerController |
| POST | `lanes` | ManagerController |
| POST | `timeslots` | ManagerController |
| POST | `lanes/assign-staff` | ManagerController |
| DELETE | `lanes/{laneId}/staff/{staffId}` | ManagerController |
| GET | `bookings` | ManagerController |
| POST | `bookings/{bookingId}/checkin-assign` | ManagerController |
| PUT | `lanes/{laneId}` | ManagerController |
| DELETE | `lanes/{laneId}` | ManagerController |
| PUT | `timeslots/{slotId}` | ManagerController |
| DELETE | `timeslots/{slotId}` | ManagerController |
| DELETE | `staff/{userId}` | ManagerController |
| POST | `check-revenue-stimulus` | ManagerController |
| GET | `revenue-stimulus/proposals` | ManagerController |
| PUT | `revenue-stimulus/proposals/{voucherId}` | ManagerController |
| POST | `revenue-stimulus/proposals/{voucherId}/approve` | ManagerController |
| POST | `revenue-stimulus/proposals/{voucherId}/reject` | ManagerController |
| POST | `revenue-stimulus/comprehensive-proposals` | ManagerController |
| POST | `branch-overload/scan-and-notify-relocation` | ManagerController |

---

## 📦 Manager Inventory

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `stocks` | ManagerInventoryController |
| POST | `imports` | ManagerInventoryController |
| GET | `batches` | ManagerInventoryController |
| POST | `batches/{id}/discard` | ManagerInventoryController |
| POST | `adjustments` | ManagerInventoryController |
| GET | `transactions` | ManagerInventoryController |
| GET | `expiring-soon` | ManagerInventoryController |
| GET | `reports/profit` | ManagerInventoryController |
| GET | `extra-usage-requests` | ManagerInventoryController |
| POST | `extra-usage-requests/{id}/approve` | ManagerInventoryController |
| POST | `extra-usage-requests/{id}/reject` | ManagerInventoryController |

---

## ⏰ Manager Overtime

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | ManagerOvertimeRequestController |
| PUT | `{id}/review` | ManagerOvertimeRequestController |

---

## 📆 Manager Shift Assignment

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | ManagerShiftAssignmentController |
| POST | *(root)* | ManagerShiftAssignmentController |
| PUT | `{id}` | ManagerShiftAssignmentController |
| DELETE | `{id}` | ManagerShiftAssignmentController |

---

## 🔄 Manager Shift Swap

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | ManagerShiftSwapRequestController |
| PUT | `{id}/review` | ManagerShiftSwapRequestController |

---

## 🗓️ Manager Work Shift

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | ManagerWorkShiftController |
| POST | *(root)* | ManagerWorkShiftController |
| PUT | `{id}` | ManagerWorkShiftController |
| DELETE | `{id}` | ManagerWorkShiftController |

---

## 🧑‍🔧 Operation Staff

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `lane-assignment` | OperationStaffController |
| GET | `tasks` | OperationStaffController |
| GET | `/api/v1/staff/tasks/bookings` | OperationStaffController |
| POST | `lanes/swap` | OperationStaffController |
| POST | `bookings/{bookingId}/checkin` | OperationStaffController |
| PUT | `bookings/{bookingId}/status` | OperationStaffController |

---

## 📋 Staff Bookings

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | StaffBookingsController |
| PUT | `{id}/status` | StaffBookingsController |
| PUT | `status-by-license-plate` | StaffBookingsController |
| GET | `by-license-plate/{licensePlate}` | StaffBookingsController |
| PUT | `{id}/no-show` | StaffBookingsController |
| PUT | `{detailId}/report-mismatch` | StaffBookingsController |
| POST | `force-cancel` | StaffBookingsController |

---

## 🧴 Staff Material Usage

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | `bookings/{bookingId}/extra` | StaffMaterialUsageController |

---

## 🙋 Staff Self Service

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `shifts` | StaffSelfServiceController |
| GET | `overtime-requests` | StaffSelfServiceController |
| POST | `overtime-requests` | StaffSelfServiceController |
| GET | `shift-swap-requests` | StaffSelfServiceController |
| POST | `shift-swap-requests` | StaffSelfServiceController |

---

## 🎟️ Staff Vouchers

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | `consume` | StaffVouchersController |

---

## 🛡️ Admin — Branches

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | AdminBranchesController |
| GET | `{id}` | AdminBranchesController |
| POST | *(root)* | AdminBranchesController |
| PUT | `{id}` | AdminBranchesController |
| GET | `{id}/employees` | AdminBranchesController |

---

## 🛡️ Admin — Employees

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | *(root)* | AdminEmployeesController |
| PUT | `{id}/transfer` | AdminEmployeesController |

---

## 🛡️ Admin — Inventory

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `stocks` | AdminInventoryController |
| GET | `batches` | AdminInventoryController |
| GET | `condition-multipliers` | AdminInventoryController |
| PUT | `condition-multipliers/{id}` | AdminInventoryController |
| GET | `reports/profit` | AdminInventoryController |
| GET | `branches/{branchId}/settings` | AdminInventoryController |
| PUT | `branches/{branchId}/settings` | AdminInventoryController |

---

## 🛡️ Admin — Lanes

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | AdminLanesController |
| GET | `{id}` | AdminLanesController |
| POST | *(root)* | AdminLanesController |
| POST | `business` | AdminLanesController |
| PUT | `{id}` | AdminLanesController |

---

## 🛡️ Admin — Managers

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | AdminManageManagerController |
| GET | `{id}` | AdminManageManagerController |
| POST | *(root)* | AdminManageManagerController |
| PUT | `{id}` | AdminManageManagerController |
| PUT | `{id}/status` | AdminManageManagerController |
| DELETE | `{id}` | AdminManageManagerController |

---

## 🛡️ Admin — Staff

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | AdminManageStaffController |
| GET | `{id}` | AdminManageStaffController |
| POST | *(root)* | AdminManageStaffController |
| PUT | `{id}` | AdminManageStaffController |
| PUT | `{id}/status` | AdminManageStaffController |
| DELETE | `{id}` | AdminManageStaffController |

---

## 🛡️ Admin — Materials

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | AdminMaterialsController |
| POST | *(root)* | AdminMaterialsController |
| PUT | `{id}` | AdminMaterialsController |

---

## 🛡️ Admin — Material Units

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | AdminMaterialUnitsController |
| POST | *(root)* | AdminMaterialUnitsController |
| PUT | `{id}` | AdminMaterialUnitsController |

---

## 🛡️ Admin — Revenue Analytics

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `evaluate-branch/{branchId}` | AdminRevenueAnalyticsController |
| POST | `trigger-campaign/{branchId}` | AdminRevenueAnalyticsController |
| POST | `trigger-all-campaigns` | AdminRevenueAnalyticsController |

---

## 🛡️ Admin — Service Material Usage

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | AdminServiceMaterialUsageController |
| POST | *(root)* | AdminServiceMaterialUsageController |
| PUT | `{usageId}` | AdminServiceMaterialUsageController |

---

## 🛡️ Admin — Services

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | AdminServicesController |
| POST | *(root)* | AdminServicesController |
| PUT | `{id}` | AdminServicesController |
| DELETE | `{id}` | AdminServicesController |

---

## 🛡️ Admin — Users

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | AdminUserController |
| GET | `{id}` | AdminUserController |
| PUT | `{id}/status` | AdminUserController |
| POST | `sync-points` | AdminUserController |

---

## 🛡️ Admin — Vehicles

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | `other-types` | AdminVehicleController |
| PUT | `{licensePlate}/type` | AdminVehicleController |
| POST | `{licensePlate}/approve-new-type` | AdminVehicleController |
| POST | `{licensePlate}/reject-new-type` | AdminVehicleController |

---

## 🛡️ Admin — Vehicle Types

| Method | Endpoint | Controller |
|--------|----------|------------|
| POST | *(root)* | AdminVehicleTypeController |
| PUT | `{id}` | AdminVehicleTypeController |
| DELETE | `{id}` | AdminVehicleTypeController |
| GET | *(root)* | AdminVehicleTypeController |

---

## 🛡️ Admin — Vouchers

| Method | Endpoint | Controller |
|--------|----------|------------|
| GET | *(root)* | AdminVouchersController |
| POST | *(root)* | AdminVouchersController |
| PUT | `{id}` | AdminVouchersController |
| DELETE | `{id}` | AdminVouchersController |
| POST | `{id}/grant` | AdminVouchersController |
| POST | `birthday` | AdminVouchersController |
| POST | `age` | AdminVouchersController |
| POST | `winback` | AdminVouchersController |
| POST | `vip` | AdminVouchersController |
| POST | `milestone` | AdminVouchersController |
| POST | `process-campaigns` | AdminVouchersController |
| POST | `trigger-weather` | AdminVouchersController |
| POST | `simulate-weather` | AdminVouchersController |

---

## 🌟 Tóm Tắt Tích Hợp Frontend & Nghiệp Vụ Cốt Lõi (FE Integration Summary)

Hệ thống cung cấp 4 luồng nghiệp vụ thông minh chính cần lưu ý khi tích hợp FE:

1. **Cân Bằng Tải & Gợi Ý Chi Nhánh (Branch Overload & Incentive)**
   - **Tình huống:** Chi nhánh được chọn quá tải (>=80%).
   - **Giải pháp:** API POST /bookings/check-slots-with-suggestions tự động quét bán kính 15km, gợi ý chi nhánh trống lịch và tặng ngay Voucher 15% để khuyến khích đổi chi nhánh. FE cần hiện popup gợi ý rõ ràng.

2. **Kích Cầu Doanh Thu Tự Động (Revenue Stimulus AI)**
   - **Tình huống:** Manager kiểm tra doanh thu cuối tháng. Nếu sụt giảm, hệ thống AI tự động phân tích và tạo sẵn kịch bản Voucher (ví dụ: giảm giá ngày vắng khách, kéo khách VIP quay lại).
   - **API chính:** POST /manager/revenue-stimulus/comprehensive-proposals.
   - **Giải pháp:** FE hiển thị Dashboard cho Manager xem đề xuất. Manager có quyền Sửa (PUT), Duyệt (POST /approve - tự động gửi voucher cho khách), hoặc Từ chối (POST /reject).

3. **Mở Rộng Thao Tác Làn Cho Nhân Viên (Flexible Staff Lane)**
   - **Tình huống:** Nhân viên không bị trói buộc vào 1 làn, giúp linh hoạt vận hành.
   - **Giải pháp:** API GET /operation-staff/tasks trả về toàn bộ xe trong chi nhánh, tự động sắp xếp theo thứ tự VIP (Kim Cương -> Vàng -> Bạc -> Đồng). Staff có quyền check-in và hoàn thành (Completed) xe ở bất kỳ làn nào.

4. **Camera AI Check-out & Mở Barie Tự Động**
   - **Tình huống:** Xe ra cổng sau khi hoàn thành dịch vụ.
   - **API chính:** POST /camera/check-out?plate=....
   - **Giải pháp:** Hệ thống tự động hoàn thành lượt rửa, trừ kho vật tư. **Lưu ý quan trọng:** Nếu xe chưa thanh toán (Unpaid), hệ thống sẽ trả lỗi HTTP 400 và từ chối mở barie. FE Kiot/Thu ngân cần hiển thị cảnh báo đỏ và phát âm thanh.

---

## 🧪 Hướng Dẫn Sử Dụng API & Viết Test Case (Testing Guide)

Để đảm bảo chất lượng hệ thống, khi viết Test Case (Unit Test cho Service hoặc Integration Test cho Controller), cần tuân thủ cấu trúc kiểm thử sau cho mỗi API:

### 1. Cấu trúc một Test Case chuẩn

Mỗi API/Service cần được test theo 3 khía cạnh (Mô hình Arrange - Act - Assert):
*   **Tiền điều kiện (Pre-conditions / Arrange):** Khởi tạo dữ liệu mẫu (Mock Data) trong DB, thiết lập quyền hạn User (Role), Token hợp lệ.
*   **Đầu vào (Inputs / Act):** Truyền tham số Body, Query Parameters, Path Variables. Phải test cả *Happy Path* (dữ liệu chuẩn) và *Edge Cases* (dữ liệu sai, thiếu, biên, vượt quá giới hạn).
*   **Hậu điều kiện (Post-conditions / Assert):** 
    *   Mã trạng thái HTTP (200, 400, 403, 404).
    *   Cấu trúc JSON trả về (statusCode, message, data).
    *   Sự thay đổi trong Database (Vd: Trạng thái hóa đơn thay đổi, số lượng vật tư bị trừ, điểm thưởng được cộng).

### 2. Ví dụ Test Case cho API: Camera AI Check-out (POST /camera/check-out)

**A. Happy Path (Thành công - Mở Barie)**
*   **Pre-condition:** Có 1 Booking ở trạng thái Processing, đã thanh toán (PaymentStatus = Completed).
*   **Input:** plate=51G12345
*   **Action:** Gọi API POST /camera/check-out.
*   **Assert:** 
    *   HTTP Status 200.
    *   Trạng thái Booking chuyển sang Completed.
    *   Service trừ kho vật tư (ConsumeMaterial) được gọi thành công với số lượng đúng định mức.

**B. Negative Case 1: Lỗi chưa thanh toán (Từ chối mở Barie)**
*   **Pre-condition:** Booking đang ở trạng thái Processing, có thu phí (FinalAmount > 0), trạng thái thanh toán Unpaid.
*   **Input:** plate=51G12345
*   **Action:** Gọi API.
*   **Assert:** 
    *   HTTP Status 400 Bad Request.
    *   Thông báo lỗi: *"Booking is unpaid; cannot check out barrier."*
    *   Booking VẪN GIỮ NGUYÊN trạng thái Processing, vật tư không bị trừ.

**C. Negative Case 2: Biển số không tồn tại trong hệ thống**
*   **Pre-condition:** Không có xe nào mang biển số 99A99999 đang được rửa tại xưởng.
*   **Input:** plate=99A99999
*   **Action:** Gọi API.
*   **Assert:** 
    *   HTTP Status 404 Not Found.

### 3. Gợi ý chiến lược Mocking (Dành cho Unit Test Services)

Khi viết test cho các lớp Service, hãy sử dụng thư viện Mock (như Moq hoặc NSubstitute trong C#) để cô lập logic:
*   **IMaterialUsageService:** Giả lập việc gọi hàm trừ kho để đảm bảo service chính truyền đúng ID dịch vụ và số lượng, mà không cần thao tác thật với DB.
*   **INotificationService:** Kiểm tra xem hàm bắn Push Notification / Email có được gọi đúng số lần (ví dụ: Times.Once()) sau khi duyệt Voucher hay dời lịch thành công hay không.
*   **IUserRepository:** Giả lập các quyền của Manager hoặc Admin khi test luồng phân quyền mà không cần tạo Account thật.
