## 🌟 TỔNG QUAN CÁC TÍNH NĂNG MỚI

Hệ thống vừa bổ sung **4 cụm tính năng & nghiệp vụ thông minh** giúp tối ưu hóa vận hành, doanh thu và nâng cao trải nghiệm khách hàng:
1. **Hệ thống Điều phối & Cân bằng tải Chi nhánh bằng GPS (Branch Overload Suggestion & Incentive):** Tự động phát hiện chi nhánh quá tải, dùng công thức địa lý Haversine quét các chi nhánh lân cận trong bán kính 15km đang trống lịch để gợi ý cho khách, đồng thời **tặng ngay Voucher giảm giá 15%** để khuyến khích khách đổi sang chi nhánh đó.
2. **Hệ thống Phân tích & Kích cầu Doanh thu Tự động (Revenue Stimulus Campaign):** Tự động đánh giá doanh thu tháng của chi nhánh so với tháng trước. Nếu doanh thu sụt giảm, hệ thống tự động phát hành phiếu giảm giá động (10% - 30%) và gửi thẳng vào tài khoản của tập khách hàng quen thuộc.
3. **Mở rộng thao tác Làn cho Staff (Flexible Staff Lane Operations):** Gỡ bỏ ràng buộc bắt buộc Staff phải được phân công vào 1 làn cụ thể trong ngày. Staff nay có thể xem, check-in, xử lý và hoàn thành xe trên **mọi làn rửa xe** của chi nhánh để tăng độ linh hoạt trong ca làm việc.
4. **API Camera AI Check-out & Mở Barie tự động hoàn thành lượt rửa (AI Camera Check-out):** Tự động quét biển số xe khi ra xưởng (áp dụng cho cả Khách đặt trước, Khách vãng lai WalkIn, và Xe doanh nghiệp Fleet) để hoàn thành dịch vụ, đóng thời gian `CompletedTime`, tự động trừ kho vật tư và bảo mật kiểm tra thanh toán trước khi mở barie ra.

Dưới đây là đặc tả chi tiết từng API endpoints và hướng dẫn nghiệp vụ/UI cho Frontend.

---

## 🤖 MỤC 0: TỔNG QUAN HỆ SINH THÁI AI NHÚNG TỰ ĐỘNG HÓA (`EMBEDDED AUTOMATED AI ENGINE`) - GIẢI THÍCH CƠ CHẾ HOẠT ĐỘNG CHO FRONTEND

Trước khi tích hợp các API bên dưới, đội ngũ Frontend và Quản trị hệ thống cần nắm rõ bản chất kiến trúc AI của SmartWash Pro. Hệ thống không sử dụng AI theo kiểu "nhập liệu thủ công vào chatbox" mà vận hành theo mô hình **AI Nhúng Tự Động Hóa Toàn Phần (Automated Embedded AI Pipeline)** gồm **3 trụ cột chính**:

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                   SMARTWASH PRO HYBRID AI ENGINE                                │
├────────────────────────────────┬────────────────────────────────┬───────────────────────────────┤
│    TRỤ CỘT 1: COMPUTER VISION  │     TRỤ CỘT 2: KNOWLEDGE AI    │     TRỤ CỘT 3: REVENUE AI     │
│   (Nhận diện Barie Camera AI)  │  (Hệ chuyên gia Khách hàng)    │   (Kích cầu & Tối ưu Trạm)    │
├────────────────────────────────┼────────────────────────────────┼───────────────────────────────┤
│ • YOLOv8 Detection & ONNX      │ • Khai phá 7 nhóm đặc trưng    │ • Tự động đối chiếu Doanh thu │
│ • Quét biển số & Hãng/Dòng xe  │   hành vi (`FeatureProfiles`)  │ • Tìm ngày trong tuần vắng xe │
│ • Tự động Check-in / Check-out │ • Khớp Kịch bản cá nhân hóa    │ • Phát hiện khách quen rời bỏ │
│ • Tự động tính giá theo phân   │ • Gợi ý giữ chân & CSKH thông  │ • Tự động "Vẽ ra" 2 kịch bản  │
│   khúc (Sedan, SUV, Luxury...) │   minh theo thời gian thực     │   Voucher chờ Manager duyệt   │
└────────────────────────────────┴────────────────────────────────┴───────────────────────────────┘
```

### 🎯 Sự khác biệt giữa AI Thông Thường vs AI Nhúng Tự Động Hóa (Giải thích cho FE & Dev Team)
- **❌ Nếu dùng AI thông thường (Thủ công):** Manager phải xuất file Excel từ DB $\rightarrow$ Copy & Paste số liệu vào ChatGPT $\rightarrow$ Gõ câu hỏi *"Hãy phân tích doanh thu và đề xuất voucher cho tôi"* $\rightarrow$ Đọc câu trả lời văn bản $\rightarrow$ Lại tự đăng nhập vào hệ thống gõ tay tạo từng mã Voucher, tự nhập ngày hết hạn, % giảm giá... rất chậm và dễ sai lệch.
- **✔ Trong SmartWash Pro (Tự động hóa 100%):**
  1. **AI tự đọc Database:** Khi Manager bấm nút *Phân tích Kích cầu* trên Dashboard (hoặc cron job chạy ngầm), Backend AI (`BranchRevenueAnalyticsService`) tự chọc thẳng vào DB (`Bookings`, `Vouchers`, `CustomerFeatureProfiles`) chỉ trong vài mili giây.
  2. **AI tự tính toán toán học & hành vi:** Tự tính % sụt giảm doanh thu tháng này vs tháng trước $\rightarrow$ Tự quét nhịp sinh học xưởng tìm ra 2 ngày vắng xe nhất trong tuần (ví dụ: *Thứ 3, Thứ 4*) $\rightarrow$ Tự đếm số khách hàng VIP đã trên 45 ngày chưa ghé.
  3. **AI tự khởi tạo bản ghi Voucher vào hệ thống:** Code AI tự khởi tạo sẵn các bản ghi `Voucher` trong Database với trạng thái `ApprovalStatus = "Proposed"` (Đang chờ duyệt), điền sẵn mã `OFFPEAK_WEEKDAY...`, `LOYAL_WINBACK...`, mức giảm $15\% - 20\%$ kèm **Lời giải thích AI (`ProposalNote`)** tường tận.
  4. **👉 Nhiệm vụ của Frontend:** FE chỉ việc gọi API lấy danh sách đề xuất (`GET /proposals` & `POST /comprehensive-proposals`) và hiển thị lên UI Dashboard cho Manager xem. Manager chỉ việc bấm **1 nút duy nhất [✔ Phê duyệt (`Approve`)]** là Voucher lập tức chính thức phát hành vào ví khách hàng!

---

## PHẦN 1: DÀNH CHO KHÁCH HÀNG (CUSTOMER APP - WEB & MOBILE)

### 1. API Kiểm tra Khung giờ trống & Gợi ý Chi nhánh Thông minh
Thay vì gọi API cũ (`POST /api/v1/bookings/available-slots`), FE chuyển sang gọi API mới này ở màn hình Đặt lịch (Booking flow) khi khách hàng vừa chọn Chi nhánh (`branchId`) và Ngày đặt (`targetDate`).

* **Endpoint:** `POST /api/v1/bookings/check-slots-with-suggestions`
* **Headers:** `Authorization: Bearer {JWT_TOKEN}` (Dùng cho cả khách đã đăng nhập hoặc khách vãng lai nếu có token, nếu không yêu cầu auth thì gửi body bình thường theo cấu hình Booking hiện tại).
* **Content-Type:** `application/json`

#### 📥 Request Body
```json
{
  "branchId": 1,
  "targetDate": "2026-07-16T00:00:00Z",
  "vehicleId": 10,
  "serviceId": 2,
  "serviceIds": [2, 5],
  "voucherId": null,
  "pointsToUse": 0
}
```
*(Ghi chú: Các trường payload tương tự như API kiểm tra slot cũ `CheckAvailableSlotsRequestDTO`).*

---

#### 📤 Response 1: Trường hợp Chi nhánh bình thường (Không bị quá tải)
Khi chi nhánh đang chọn (`branchId: 1`) có tỷ lệ lấp đầy dưới 80% và có khung giờ trống, hệ thống trả về `isOverloaded: false` và `hasAlternativeSuggestion: false`.

```json
{
  "statusCode": 200,
  "message": "Success",
  "data": {
    "currentBranchId": 1,
    "currentBranchName": "Chi nhánh Trung tâm Quận 1",
    "currentOccupancyRate": 0.45,
    "isOverloaded": false,
    "statusMessage": "Chi nhánh đang có sẵn lịch trống và công suất phục vụ tốt.",
    "hasAlternativeSuggestion": false,
    "suggestedAlternative": null,
    "incentiveVoucher": null,
    "timeSlots": [
      {
        "slotId": 101,
        "branchId": 1,
        "startTime": "08:00:00",
        "endTime": "09:00:00",
        "isAvailable": true,
        "maxCapacity": 10,
        "currentBookedWeight": 4,
        "reason": null
      },
      {
        "slotId": 102,
        "branchId": 1,
        "startTime": "09:00:00",
        "endTime": "10:00:00",
        "isAvailable": true,
        "maxCapacity": 10,
        "currentBookedWeight": 5,
        "reason": null
      }
    ]
  }
}
```
👉 **Xử lý FE:** Hiển thị danh sách `timeSlots` cho khách hàng chọn giờ như bình thường.

---

#### 📤 Response 2: Trường hợp Chi nhánh bị QUÁ TẢI (Đông người >= 80% hoặc hết lịch) -> CÓ GỢI Ý ĐỔI CHI NHÁNH & TẶNG VOUCHER 15%
Khi chi nhánh đang chọn bị quá tải (`isOverloaded: true`), hệ thống tự động quét bằng tọa độ GPS các chi nhánh lân cận (<= 15 km) đang trống lịch (`< 70%`), chọn chi nhánh tối ưu nhất và tự động phát hành **Mã giảm giá 15%** (`incentiveVoucher`) vào thẳng tài khoản khách hàng.

```json
{
  "statusCode": 200,
  "message": "Success",
  "data": {
    "currentBranchId": 1,
    "currentBranchName": "Chi nhánh Trung tâm Quận 1",
    "currentOccupancyRate": 0.88,
    "isOverloaded": true,
    "statusMessage": "Chi nhánh Chi nhánh Trung tâm Quận 1 hiện đang rất đông (88% kín lịch). Thời gian chờ có thể kéo dài.",
    "hasAlternativeSuggestion": true,
    "suggestedAlternative": {
      "branchId": 3,
      "branchName": "Chi nhánh Cao Thắng Quận 3",
      "address": "123 Cao Thắng, Phường 4, Quận 3, TP.HCM",
      "distanceKm": 2.8,
      "occupancyRate": 0.35,
      "availableSlotsCount": 8
    },
    "incentiveVoucher": {
      "voucherId": 88,
      "voucherCode": "SWITCH_BR3_15%",
      "discountPercentage": 15,
      "description": "🎁 Tặng ngay Mã giảm giá 15% khi bạn đặt lịch sang Chi nhánh Cao Thắng Quận 3 hôm nay!",
      "expiresInHours": 24
    },
    "timeSlots": [
      {
        "slotId": 101,
        "branchId": 1,
        "startTime": "08:00:00",
        "endTime": "09:00:00",
        "isAvailable": false,
        "maxCapacity": 10,
        "currentBookedWeight": 10,
        "reason": "Fully booked"
      }
    ]
  }
}
```

#### 💻 Hướng dẫn triển khai UI/UX cho Frontend (Web & Mobile App)
Khi kiểm tra thấy `data.hasAlternativeSuggestion == true`:
1. **Hiển thị Popup / Banner / Card Gợi Ý Thông Minh (Smart Suggestion Card):**
   - **Tiêu đề cảnh báo:** ⚠️ *Chi nhánh bạn chọn đang rất đông (Kín lịch {currentOccupancyRate * 100}%)!*
   - **Nội dung gợi ý:** 💡 *Hệ thống đề xuất bạn chuyển sang **{suggestedAlternative.branchName}** (Chỉ cách **{suggestedAlternative.distanceKm} km** - Địa chỉ: {suggestedAlternative.address}) hiện đang rất thoáng (Chỉ mới lấp đầy {suggestedAlternative.occupancyRate * 100}%).*
   - **Thẻ quà tặng nổi bật (Incentive Badge):** 🎁 **TẶNG NGAY VOUCHER GIẢM 15% (`{incentiveVoucher.voucherCode}`)** *Áp dụng tự động cho hóa đơn của bạn khi đổi sang chi nhánh này!*
2. **Nút bấm hành động (Action Buttons):**
   - Nút chính (Highlight): **[⚡ Đổi sang {suggestedAlternative.branchName} & Nhận giảm 15%]**
     - *Khi khách bấm nút này:* FE tự động cập nhật `selectedBranchId = data.suggestedAlternative.branchId`, tự động gán `selectedVoucherId = data.incentiveVoucher.voucherId` vào form Đặt lịch, sau đó tải lại danh sách slot của chi nhánh mới hoặc gọi lại API với `branchId` mới.
   - Nút phụ: **[Vẫn tiếp tục giữ chi nhánh cũ]**
     - *Khi khách bấm nút này:* Ẩn popup và cho phép khách xem các slot còn lại của chi nhánh cũ.

---

### 2. Lưu ý quan trọng khi Đặt lịch chính thức (`POST /api/v1/bookings`)
Hệ thống Backend đã bổ sung bảo mật và ràng buộc nghiệp vụ cho Voucher chuyển đổi chi nhánh:
- Voucher `SWITCH_BR{id}_15%` hoặc `WINBACK_BR{id}` có gắn định danh chi nhánh (`Voucher.BranchId`).
- Khi FE gọi API tạo Booking (`POST /api/v1/bookings`), nếu khách chọn sử dụng Voucher của chi nhánh A (`BranchId = 3`), thì `request.BranchId` của đơn booking **bắt buộc phải là `3`**.
- Nếu khách thử dùng mã giảm giá của Chi nhánh 3 để đặt lịch tại Chi nhánh 1, Backend sẽ từ chối và trả về HTTP 400 Bad Request:
  `"This voucher is only valid for use at branch #3 (Chi nhánh Cao Thắng Quận 3)."` -> FE cần bắt lỗi này để hiển thị thông báo rõ ràng cho khách.

---

### 3. API Khách Hàng Đồng Ý Dời Lịch Khẩn Cấp (Accept Relocation)
Khi chi nhánh đột xuất bị kẹt tải do quá đông khách Walk-in, hệ thống sẽ bắn Push Notification đề xuất dời lịch (kèm Voucher 50K). Khách hàng bấm **[Đồng Ý]** trên App thì FE gọi API này để hệ thống dời lịch sang chi nhánh thay thế.

* **Endpoint:** `POST /api/v1/bookings/{id}/accept-relocation`
* **Headers:** `Authorization: Bearer {JWT_TOKEN}`
* **Content-Type:** `application/json`

#### 📥 Request Body
```json
{
  "alternativeBranchId": 2,
  "voucherCode": "SURGE_REL_1_123"
}
```

#### 📤 Response
```json
{
  "statusCode": 200,
  "message": "Relocation accepted and voucher applied successfully.",
  "data": {
    "bookingId": 123,
    "licensePlate": "51G-123.45",
    "serviceNames": ["Rửa xe tiêu chuẩn"],
    "scheduledTime": "2026-07-19T10:00:00Z",
    "status": "Pending",
    "originalPrice": 100000,
    "pointDiscountAmount": 0,
    "voucherDiscountAmount": 50000,
    "finalAmount": 50000
  }
}
```
*Ghi chú: Khi API này thành công, backend đã tự động giải phóng công suất (capacity) ở chi nhánh cũ và cộng tải vào chi nhánh mới.*

---
---

## PHẦN 2: DÀNH CHO QUẢN LÝ CHI NHÁNH (MANAGER PORTAL - WEB ADMIN/DASHBOARD)

### 1. API Kiểm tra Sức khỏe Doanh thu & Kích hoạt Chiến dịch Kích cầu (Tháng)
Dành cho trang Dashboard của Quản lý chi nhánh (`Manager Role`). Quản lý có thể chủ động kiểm tra xem doanh thu tháng này so với tháng trước tăng hay giảm. Nếu sụt giảm, hệ thống tự động tạo mã giảm giá kích cầu và gửi cho khách hàng quen thuộc của chi nhánh.

### 1. API Kiểm tra Sức khỏe Doanh thu & Khởi tạo Đề xuất Kích cầu (Tháng)
Dành cho trang Dashboard của Quản lý chi nhánh (`Manager Role`). Quản lý có thể chủ động kiểm tra xem doanh thu tháng này so với tháng trước tăng hay giảm. Nếu sụt giảm, hệ thống **KHÔNG PHÁT VOUCHER TỰ ĐỘNG** mà sẽ tạo một **Đề xuất Voucher ở trạng thái chờ duyệt (`ApprovalStatus = "Proposed"`)** để Manager quyết định có chấp nhận, chỉnh sửa hay từ chối.

* **Endpoint:** `POST /api/v1/manager/check-revenue-stimulus`
* **Query Parameters (Tùy chọn):** `?month=7&year=2026` *(Nếu bỏ trống, hệ thống tự động lấy tháng/năm hiện tại theo UTC)*
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_MANAGER}`
* **Quyền hạn:** `Manager` *(Hệ thống tự động nhận diện `BranchId` từ profile của Manager)*

#### 📤 Response 1: Doanh thu Tăng trưởng hoặc Ổn định (Không cần kích cầu)
```json
{
  "statusCode": 200,
  "message": "Success",
  "data": {
    "branchId": 1,
    "branchName": "Chi nhánh Trung tâm Quận 1",
    "targetMonth": 7,
    "targetYear": 2026,
    "currentMonthRevenue": 150000000,
    "previousMonthRevenue": 120000000,
    "revenueDropPercentage": 0,
    "isCampaignTriggered": false,
    "approvalStatus": "N/A",
    "message": "Doanh thu tháng 07/2026 của Chi nhánh Trung tâm Quận 1 đạt 150,000,000đ (ổn định hoặc tăng trưởng so với tháng trước 120,000,000đ). Không cần đề xuất phiếu giảm giá.",
    "generatedVoucherCode": null,
    "discountPercentage": 0,
    "grantedUsersCount": 0
  }
}
```

#### 📤 Response 2: Doanh thu Sụt giảm -> TẠO ĐỀ XUẤT VOUCHER CHỜ DUYỆT (`Proposed`)
```json
{
  "statusCode": 200,
  "message": "Success",
  "data": {
    "branchId": 1,
    "branchName": "Chi nhánh Trung tâm Quận 1",
    "targetMonth": 7,
    "targetYear": 2026,
    "currentMonthRevenue": 80000000,
    "previousMonthRevenue": 120000000,
    "revenueDropPercentage": 33.33,
    "isCampaignTriggered": false,
    "approvalStatus": "Proposed",
    "message": "Doanh thu giảm 33.33%. Hệ thống đã tạo ĐỀ XUẤT Voucher (WINBACK_BR1_M07Y2026_15%) chờ Manager xét duyệt (Số khách quen mục tiêu: ~45 người).",
    "generatedVoucherCode": "WINBACK_BR1_M07Y2026_15%",
    "discountPercentage": 15,
    "grantedUsersCount": 0
  }
}
```

---

### 2. API Lấy Danh sách Đề xuất Voucher đang chờ duyệt (`GET /api/v1/manager/revenue-stimulus/proposals`)
FE hiển thị danh sách các đề xuất voucher kích cầu đang chờ xét duyệt (`ApprovalStatus == "Proposed"`) trên Dashboard của Manager.

* **Endpoint:** `GET /api/v1/manager/revenue-stimulus/proposals`
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_MANAGER}`

#### 📤 Response:
```json
{
  "statusCode": 200,
  "message": "Success",
  "data": [
    {
      "voucherId": 88,
      "code": "WINBACK_BR1_M07Y2026_15%",
      "discountAmount": 15,
      "maxUsages": 999999,
      "expiryDays": 30,
      "approvalStatus": "Proposed",
      "proposalNote": "Doanh thu tháng 07/2026 của Chi nhánh Trung tâm Quận 1 giảm 33.33% (còn 80,000,000đ so với 120,000,000đ). Đề xuất Voucher giảm 15% để kéo khách hàng trở lại.",
      "branchId": 1,
      "branchName": "Chi nhánh Trung tâm Quận 1",
      "targetMonth": 7,
      "targetYear": 2026,
      "estimatedTargetCustomers": 45,
      "createdAt": "2026-07-15T08:00:00Z"
    }
  ]
}
```

---

### 3. API Sửa đổi Đề xuất Voucher (`PUT /api/v1/manager/revenue-stimulus/proposals/{voucherId}`)
Cho phép Manager điều chỉnh lại mã code, mức giảm discount (% hoặc tiền mặt), số lượng tối đa và thời gian hết hạn của voucher trước khi duyệt phát hành.

* **Endpoint:** `PUT /api/v1/manager/revenue-stimulus/proposals/{voucherId}`
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_MANAGER}`
* **Request Body:** *(Gửi các trường muốn cập nhật, trường nào giữ nguyên có thể bỏ qua)*
```json
{
  "code": "WINBACK_BR1_VIP_20%",
  "discountAmount": 20,
  "maxUsages": 100,
  "expiryDays": 15,
  "proposalNote": "Quản lý nâng mức giảm lên 20% và giới hạn 100 lượt dùng đầu tiên trong 15 ngày."
}
```

---

### 4. API Phê duyệt Đề xuất Voucher (`POST /api/v1/manager/revenue-stimulus/proposals/{voucherId}/approve`)
Khi Manager bấm **[✔ Chấp nhận & Phát hành Voucher]**, hệ thống chuyển `ApprovalStatus = "Approved"`, kích hoạt `IsActive = true` và **chính thức phát hành Voucher vào ví (`UserVouchers`) của tập khách hàng mục tiêu**.

* **Endpoint:** `POST /api/v1/manager/revenue-stimulus/proposals/{voucherId}/approve`
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_MANAGER}`

#### 📤 Response:
```json
{
  "statusCode": 200,
  "message": "Proposal approved and distributed successfully.",
  "data": {
    "branchId": 1,
    "branchName": "Chi nhánh Trung tâm Quận 1",
    "targetMonth": 7,
    "targetYear": 2026,
    "isCampaignTriggered": true,
    "approvalStatus": "Approved",
    "message": "Đã PHÊ DUYỆT thành công đề xuất Voucher 'WINBACK_BR1_VIP_20%'. Đã phát hành và gửi vào ví của 45 khách hàng quen thuộc!",
    "generatedVoucherCode": "WINBACK_BR1_VIP_20%",
    "discountPercentage": 20,
    "grantedUsersCount": 45
  }
}
```

---

### 5. API Từ chối Đề xuất Voucher (`POST /api/v1/manager/revenue-stimulus/proposals/{voucherId}/reject`)
Khi Manager bấm **[✖ Từ chối Đề xuất]**, hệ thống đóng đề xuất (`ApprovalStatus = "Rejected"`, `IsActive = false`) và không gửi phiếu giảm giá nào cho khách.

* **Endpoint:** `POST /api/v1/manager/revenue-stimulus/proposals/{voucherId}/reject`
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_MANAGER}`
* **Request Body (Tùy chọn):**
```json
{
  "rejectReason": "Doanh thu giảm do xưởng sửa chữa nâng cấp hệ thống 5 ngày, không cần kích cầu."
}
```

---

### 6. API Phân Tích Tổng Hợp Doanh Thu, Lưu Lượng & Đề Xuất 2 Kịch Bản Voucher (`POST /api/v1/manager/revenue-stimulus/comprehensive-proposals`)
Đây là API thông minh và toàn diện nhất dành cho Manager khi muốn AI kiểm tra và đối chiếu đồng thời: **(1) Mức doanh thu hiện tại vs tháng trước**, **(2) Thống kê lưu lượng khách ra vào trong tháng và ngày vắng khách**, và **(3) Số lượng khách hàng thân thiết có dấu hiệu rời bỏ (>45 ngày chưa quay lại)**. Dựa trên dữ liệu doanh thu, hệ thống tự động tính toán tỷ lệ giảm giá phù hợp nhất và "vẽ ra" 2 kịch bản đề xuất (`OFFPEAK_WEEKDAY` và `LOYAL_WINBACK`) trong danh sách chờ duyệt.

* **Endpoint:** `POST /api/v1/manager/revenue-stimulus/comprehensive-proposals?month=7&year=2026` *(month, year tùy chọn, mặc định lấy tháng/năm hiện tại)*
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_MANAGER}`

#### 📤 Response:
```json
{
  "statusCode": 200,
  "message": "Comprehensive analysis and proposals generated successfully.",
  "data": {
    "branchId": 1,
    "branchName": "Chi nhánh Trung tâm Quận 1",
    "targetMonth": 7,
    "targetYear": 2026,
    "previousMonthRevenue": 150000000,
    "currentMonthRevenue": 125000000,
    "revenueDropPercentage": 16.67,
    "isRevenueHealthy": false,
    "trafficAndCustomerStats": {
      "totalCheckInsThisMonth": 240,
      "averageDailyCheckIns": 16.0,
      "slowestDaysOfWeek": "Thứ 3, Thứ 4",
      "atRiskLoyalCustomersCount": 38,
      "activeCustomersCount": 185
    },
    "proposedVouchers": [
      {
        "voucherId": 12,
        "code": "OFFPEAK_B1_M07Y2026_15",
        "discountAmount": 15,
        "maxUsages": 100,
        "expiryDays": 30,
        "approvalStatus": "Proposed",
        "proposalNote": "[Phân tích AI] Đối chiếu doanh thu tháng 07/2026 sụt giảm 16.67%, phát hiện lưu lượng vào Thứ 3, Thứ 4 rất thấp (trung bình ~16 xe/ngày). Đề xuất giảm 15% cho các ngày vắng khách nhằm tối ưu công suất xưởng.",
        "branchId": 1,
        "branchName": "Chi nhánh Trung tâm Quận 1",
        "targetMonth": 7,
        "targetYear": 2026,
        "previousMonthRevenue": 150000000,
        "currentMonthRevenue": 125000000,
        "revenueDropPercentage": 16.67,
        "estimatedTargetCustomers": 480,
        "createdAt": "2026-07-15T08:15:00Z"
      },
      {
        "voucherId": 13,
        "code": "LOYAL_B1_M07Y2026_20",
        "discountAmount": 20,
        "maxUsages": 76,
        "expiryDays": 20,
        "approvalStatus": "Proposed",
        "proposalNote": "[Phân tích AI] Đối chiếu doanh thu giảm 16.67%, hệ thống phát hiện có 38 khách hàng thân thiết (>2 lượt) đã hơn 45 ngày chưa quay lại. Đề xuất ưu đãi 20% để tri ân và kéo nhóm VIP quay lại ngay.",
        "branchId": 1,
        "branchName": "Chi nhánh Trung tâm Quận 1",
        "targetMonth": 7,
        "targetYear": 2026,
        "previousMonthRevenue": 150000000,
        "currentMonthRevenue": 125000000,
        "revenueDropPercentage": 16.67,
        "estimatedTargetCustomers": 38,
        "createdAt": "2026-07-15T08:15:00Z"
      }
    ],
    "comprehensiveAnalysisSummary": "Doanh thu tháng 07/2026 đạt 125,000,000đ (giảm 16.67% so với tháng trước 150,000,000đ). Lưu lượng khách trung bình 16 xe/ngày, vắng nhất vào Thứ 3, Thứ 4. Phát hiện 38 khách hàng thân thiết (>45 ngày chưa quay lại). Hệ thống đã tạo 2 đề xuất mã ưu đãi tối ưu hóa theo tỷ lệ doanh thu hiện tại."
  }
}
```

---

#### 💻 Hướng dẫn triển khai UI Dashboard cho Manager (FE):
- Nút bấm chính trên Dashboard: **[🤖 Phân Tích Kích Cầu & Đề Xuất Khuyến Mãi AI]** $\rightarrow$ Khi gọi `POST /api/v1/manager/revenue-stimulus/comprehensive-proposals`.
- **Phần 1 - Bảng Thống Kê Tổng Hợp (Summary Header):** Hiển thị đoạn `comprehensiveAnalysisSummary` cùng các thẻ KPI:
  - Doanh thu: `currentMonthRevenue` (Đang giảm/tăng `revenueDropPercentage`%)
  - Lưu lượng trung bình/ngày: `averageDailyCheckIns` xe/ngày (Vắng nhất: `slowestDaysOfWeek`)
  - Khách quen sắp rời bỏ: `atRiskLoyalCustomersCount` khách (>45 ngày chưa đến)
- **Phần 2 - Danh Sách Các Đề Xuất (`proposedVouchers`):** Hiển thị từng thẻ `VoucherProposalDTO` đang ở trạng thái `Proposed`.
- Cung cấp bộ 3 nút bấm thao tác cho Manager trên từng thẻ:
  1. **[✏️ Sửa đổi thông số (`Modify`)]** $\rightarrow$ Mở modal nhập `% giảm giá`, `Số ngày hết hạn` $\rightarrow$ Gọi `PUT /proposals/{id}`.
  2. **[✔ Phê duyệt & Phát hành ngay (`Approve`)]** $\rightarrow$ Gọi `POST /proposals/{id}/approve` $\rightarrow$ Phát hành thẳng vào ví khách hàng.
  3. **[✖ Từ chối (`Reject`)]** $\rightarrow$ Gọi `POST /proposals/{id}/reject`.

---

### 3. API Quét Tải & Bắn Thông Báo Kẹt Xe Khẩn Cấp (Proactive Relocation)
API này dùng để quét các lịch đặt trước trong 2 tiếng tới tại chi nhánh. Nếu quản lý thấy chi nhánh đang quá tải do khách vãng lai (hoặc hệ thống chạy ngầm CronJob quét định kỳ), API sẽ tự động tạo Voucher giảm giá đền bù 50,000 VND và lên danh sách các khách hàng cần gửi cảnh báo xin dời chi nhánh.

* **Endpoint:** `POST /api/v1/manager/branch-overload/scan-and-notify-relocation`
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_MANAGER}`

#### 📤 Response:
```json
{
  "statusCode": 200,
  "message": "Scanned for overloaded bookings. Relocation proposals generated and simulated notifications sent.",
  "data": [
    {
      "bookingId": 123,
      "customerName": "Nguyễn Văn A",
      "licensePlate": "51G-123.45",
      "scheduledTime": "2026-07-19T10:00:00Z",
      "originalBranchId": 1,
      "alternativeBranchId": 2,
      "voucherCode": "SURGE_REL_1_123",
      "discountAmount": 50000
    }
  ]
}
```

#### 💻 Hướng dẫn UI cho Manager (FE):
- Manager bấm nút **[Quét cảnh báo Kẹt tải]** trên Dashboard.
- Hiển thị danh sách khách hàng (`data`) vừa được hệ thống tự động bắn Push Notification đề nghị dời lịch.
- Manager có thể theo dõi xem khách nào đã đồng ý dời đi để điều phối thợ rửa xe.

---
---

## PHẦN 3: DÀNH CHO QUẢN TRỊ VIÊN HỆ THỐNG (SUPER ADMIN PORTAL / CRON JOB)

### 1. API Xem Báo Cáo Phân Tích Doanh Thu Chi Nhánh (Admin Only)
* **Endpoint:** `GET /api/v1/admin/revenue-analytics/evaluate-branch/{branchId}?month=7&year=2026`
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_ADMIN}`
* **Mô tả:** Trả về đối tượng `BranchMonthlyRevenueDTO` chứa chi tiết doanh thu và tỷ lệ tăng/giảm của chi nhánh bất kỳ mà không kích hoạt phát voucher. Dùng để vẽ biểu đồ thống kê cho Super Admin.

### 2. API Kích hoạt Chiến dịch Kích cầu cho 1 Chi nhánh (Admin Only)
* **Endpoint:** `POST /api/v1/admin/revenue-analytics/trigger-campaign/{branchId}?month=7&year=2026`
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_ADMIN}`
* **Mô tả:** Kích hoạt chiến dịch kích cầu thủ công cho riêng chi nhánh có `branchId`.

### 3. API Kích hoạt Quét & Kích cầu Toàn bộ Chi nhánh trên Toàn Quốc (Admin & Cron Job)
* **Endpoint:** `POST /api/v1/admin/revenue-analytics/trigger-all-campaigns?month=7&year=2026`
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_ADMIN}`
* **Mô tả:** Quét toàn bộ danh sách chi nhánh hoạt động. Chi nhánh nào bị giảm doanh thu sẽ tự động nhận voucher kích cầu cho tệp khách quen.
* **Response:** Trả về một mảng `List<MonthlyRevenueCampaignResultDTO>` tổng hợp kết quả của từng chi nhánh.
* **Ứng dụng FE:** Tạo nút bấm trong Admin Control Panel: **[🚀 Chạy chiến dịch Kích cầu Toàn hệ thống Tháng {m}]** hoặc cấu hình gọi tự động vào ngày 1 hàng tháng qua Worker/Cron Job.

---
---

## PHẦN 4: DÀNH CHO NHÂN VIÊN XƯỞNG & HỆ THỐNG BARIE AI (STAFF PORTAL & CAMERA AI CHECK-OUT)

### 1. Cập nhật Thay đổi Nghiệp vụ: Mở rộng quyền thao tác Làn cho Staff (`OperationStaffController`)
Nhằm tăng độ linh hoạt trong quá trình vận hành xưởng, Backend đã **GỠ BỎ HOÀN TOÀN ràng buộc phân công làn (`StaffLaneAssignment`)** khi Staff xem, Check-in và cập nhật trạng thái lượt rửa xe.

#### 🔄 Tác động trực tiếp đến logic & giao diện Staff App (FE):
1. **API Kiểm tra Làn được phân công trong ngày:**
   - **Endpoint:** `GET /api/v1/operation-staff/lane-assignment`
   - **Thay đổi từ Backend:** Nếu Staff trong ngày đó **chưa được phân công vào làn cụ thể nào**, thay vì trả về `null` hoặc báo lỗi cấm truy cập, API sẽ tự động trả về object mặc định đại diện cho toàn bộ xưởng:
     ```json
     {
       "laneId": 0,
       "laneName": "Mọi làn rửa xe (All Lanes)",
       "assignedDate": "2026-07-16T00:00:00Z"
     }
     ```
   - 👉 **Hướng dẫn xử lý FE:** Khi `laneId == 0`, FE **KHÔNG ĐƯỢC ẨN** màn hình làm việc của Staff hoặc báo lỗi "Chưa phân công làn". Hãy hiển thị Badge/Tiêu đề khu vực làm việc là: `🏢 Khu vực: Mọi làn rửa xe (All Lanes)` và cho phép Staff thao tác bình thường.

2. **API Lấy Danh sách Lượt rửa/Công việc đang diễn ra trên Xưởng (Priority Queue):**
   - **Endpoint:** `GET /api/v1/operation-staff/tasks` *(hoặc `/api/v1/staff/tasks/bookings`)*
   - **Thay đổi từ Backend:** 
     - API không còn bị lọc/khóa theo `LaneId` riêng của Staff hay lọc theo `ProcessingStaffId == currentStaffId`. Nay API trả về **TOÀN BỘ danh sách xe trong chi nhánh đang có trạng thái `CheckedIn` hoặc `Processing` trong ngày**.
     - **[NÂNG CẤP MỚI - PRIORITY QUEUE SORTING]:** Danh sách xe trả về đã được Backend **tự động sắp xếp ưu tiên theo thứ bậc Hạng thành viên từ cao xuống thấp (`MinAccumulatedPoints -> ScheduledTime`)**: Kim Cương (`15,000`) $\rightarrow$ Vàng (`5,000`) $\rightarrow$ Bạc (`1,000`) $\rightarrow$ Đồng (`0`) $\rightarrow$ Vãng lai (`-1`).
     - Mỗi đối tượng `StaffBookingDTO` trả về được bổ sung 2 trường: `customerTierName` (Ví dụ: `"Diamond"`, `"Gold"`, `"WalkIn / Standard"`) và `customerTierPoints` (Điểm tích lũy tối thiểu của hạng).
   - 👉 **Hướng dẫn xử lý FE (Staff POS / Tablet):** 
     - Hiển thị toàn bộ xe đang chờ theo đúng thứ tự mảng trả về (vì Backend đã sort chuẩn ưu tiên VIP lên đầu).
     - Hiển thị Huy hiệu Hạng VIP (`customerTierName`) ngay bên cạnh biển số xe với màu sắc tương ứng (👑 Vàng cho Gold, 💎 Xanh kim cương cho Diamond) để Staff nhận biết và phục vụ xe VIP trước tiên!

3. **API Check-in Xe (`Pending` $\rightarrow$ `CheckedIn`):**
   - **Endpoint:** `POST /api/v1/operation-staff/bookings/{bookingId}/checkin`
   - **Thay đổi từ Backend:** Staff có thể Check-in cho bất kỳ xe nào đang chờ (`Pending`) mà không lo bị vướng lỗi "You are not assigned to any lane today". Hệ thống tự động gán xe vào làn hoạt động của chi nhánh và ghi nhận Staff Check-in.

4. **API Cập nhật Trạng thái Xe (`CheckedIn` $\rightarrow$ `Processing` $\rightarrow$ `Completed`):**
   - **Endpoint:** `PUT /api/v1/operation-staff/bookings/{bookingId}/status`
   - **Payload:** `{ "status": "Processing" }` hoặc `{ "status": "Completed" }`
   - **Thay đổi từ Backend:** Gỡ bỏ ngoại lệ cấm nhân viên khác hoàn thành lượt rửa của xe không do mình bắt đầu (`You are not assigned to this vehicle`). Bất kỳ Staff nào cũng có thể tiếp quản và bấm `Completed` cho một xe đang `Processing` ở bất kỳ làn nào ("cho staff có thể vào chơi mọi làn rửa"). Khi bấm `Completed`, hệ thống tự động cập nhật `ProcessingStaffId` cho Staff thực hiện thao tác hoàn thành để ghi nhận công việc và cộng thưởng (`ConsumeForCompletedBookingAsync`).
   - 👉 **Hướng dẫn xử lý FE:** Nút bấm **[▶ Bắt đầu rửa xe]** (`Processing`) và **[✔ Hoàn thành rửa xe]** (`Completed`) luôn hiển thị ở trạng thái **Enable (Cho phép bấm)** đối với toàn bộ danh sách xe tương ứng trên màn hình của mọi Staff trong ca làm việc.

---

### 2. API Camera AI Check-out Tự động Hoàn thành Lượt rửa & Mở Barie (`check-out`)
Đây là API mới dành riêng cho Hệ thống Camera AI nhận diện biển số xe (LPR) đặt tại cổng ra (Exit Barrier) hoặc Kiot/Tablet kiểm tra xe xuất xưởng của Thu ngân.

* **Endpoint:** `POST /api/v1/camera/check-out?plate={licensePlate}`
* **Query Parameter:** `plate` *(Bắt buộc - Biển số xe AI nhận dạng được lúc xe di chuyển ra cổng, ví dụ: `51G-123.45` hoặc `51G12345`)*
* **Headers:** `AllowAnonymous` *(Hoặc kèm token theo cấu hình bảo mật nội bộ thiết bị IoT/Camera)*
* **Phạm vi áp dụng toàn diện:** Backend tự động quét và hoàn thành lượt rửa đang hoạt động (`CheckedIn` hoặc `Processing` / `Assigned`) cho **cả 3 nhóm khách hàng**:
  1. Khách đặt lịch trước (`Personal` / `Business Booking`)
  2. Khách vãng lai (`WalkIn Booking` tạo tại quầy)
  3. Xe doanh nghiệp Check-in theo hạm đội (`FleetWashLog`)

#### ⚙️ Các tác vụ tự động ngầm khi API `check-out` được gọi thành công:
- Tự động đổi trạng thái lượt rửa (`Booking` / `FleetWashLog`) sang **`Completed`**.
- Tự động đóng thời gian kết thúc `CompletedTime = DateTime.UtcNow` và tính toán thời lượng rửa thực tế `ActualDurationMinutes`.
- Tự động trừ kho vật tư theo định mức dịch vụ (`BookingMaterialUsageService.ConsumeForCompletedBookingAsync`).
- Tự động tích điểm thành viên và kích hoạt kiểm tra mốc chiến dịch khuyến mãi (`VoucherCampaignService`).

---

#### 📤 Response 1: Xe hợp lệ & Đã hoàn tất thanh toán (Mở Barie thành công)
Trường hợp xe miễn phí (`FinalAmount == 0`) hoặc xe có phí (`FinalAmount > 0`) và **ĐÃ THANH TOÁN** (`PaymentStatus == Completed`).

```json
{
  "statusCode": 200,
  "message": "Vehicle check-out completed, barrier opening!",
  "data": {
    "bookingId": 105,
    "licensePlate": "51G12345",
    "serviceNames": [
      "Rửa xe cơ bản (Basic Wash)",
      "Hút bụi nội thất (Interior Vacuum)"
    ],
    "scheduledTime": "2026-07-16T08:00:00Z",
    "status": "Completed",
    "originalPrice": 150000,
    "pointDiscountAmount": 0,
    "voucherDiscountAmount": 0,
    "finalAmount": 150000,
    "processingStartTime": "2026-07-16T08:05:00+07:00",
    "completedTime": "2026-07-16T08:35:00+07:00",
    "actualDurationMinutes": 30
  }
}
```
👉 **Hướng dẫn triển khai FE (Màn hình Kiot Cổng ra / Barie AI Display):**
- Hiển thị Banner Xanh lá (Success): **"✔ XE 51G-123.45 HOÀN TẤT DỊCH VỤ (30 PHÚT) - ĐANG MỞ BARIE!"**
- Phát âm thanh lời chào: *"Xin cảm ơn quý khách. Barie đã mở, chúc quý khách lái xe an toàn!"*
- Gửi lệnh mở cổng (nếu Kiot điều khiển trực tiếp relay barie).

---

#### 📤 Response 2: Xe CHƯA THANH TOÁN (Từ chối mở Barie & Cảnh báo Thu ngân)
> [!IMPORTANT]
> **Chính sách bảo mật doanh thu:** Nếu xe có hóa đơn phí (`FinalAmount > 0`) nhưng **chưa hoàn tất thanh toán** (`HasCompletedBookingPaymentAsync == false`), hệ thống lập tức **TỪ CHỐI hoàn thành và GIỮ BARIE ĐÓNG**, trả về lỗi HTTP 400 Bad Request để ngăn xe rời xưởng mà chưa trả tiền.

```json
{
  "statusCode": 400,
  "message": "Booking is unpaid; cannot check out barrier."
}
```
👉 **Hướng dẫn triển khai FE (Màn hình Kiot Cổng ra & Màn hình POS Thu ngân):**
- Khi nhận lỗi HTTP 400 với `message: "Booking is unpaid..."`:
  - **Màn hình Kiot Cổng ra:** Hiển thị Cảnh báo Đỏ rực (Error): **"⛔ CẢNH BÁO: XE 51G-123.45 CHƯA THANH TOÁN! BARIE GIỮ ĐÓNG."** kèm phát âm thanh cảnh báo: *"Vui lòng hoàn tất thanh toán hóa đơn trước khi rời khỏi xưởng!"*
  - **Màn hình Thu ngân (POS):** Phát âm thanh báo động nhẹ/Alert Popup thông báo cho nhân viên thu ngân: *"🔔 Xe 51G-123.45 đang ra cổng nhưng chưa thanh toán! Vui lòng thu tiền ngay."*

---

#### 📤 Response 3: Không tìm thấy lượt rửa đang hoạt động
Nếu biển số xe không hợp lệ hoặc xe này không có lượt rửa nào ở trạng thái `CheckedIn` / `Processing` trong xưởng (xe lạ đậu nhầm hoặc xe đã check-out từ trước):
```json
{
  "statusCode": 404,
  "message": "No active wash session (CheckedIn/Processing) found for vehicle 51G12345 to complete check-out."
}
```
👉 **Hướng dẫn xử lý FE:** Hiển thị thông báo: *"⚠️ Không tìm thấy lượt rửa đang hoạt động cho biển số 51G-123.45. Vui lòng liên hệ nhân viên xưởng để được hỗ trợ."*

---

### 3. Đặc tả & Hướng dẫn FE cho Luồng Check-in Cổng Vào (`Entry Check-in`): Khách Có Đăng Ký vs Khách Vãng Lai & Đặc Quyền Khách VIP
Phần này mô tả trọn vẹn luồng tiếp đón khi xe tiến vào trạm rửa (`Entry Flow`), giúp Frontend (Staff Tablet/POS & Camera AI Kiot) xử lý phân luồng chính xác giữa **Khách có đăng ký booking trước**, **Khách vãng lai (Walk-In)** và các ưu đãi đặc quyền dành cho **Khách VIP (Gold / Platinum)**.

#### 📍 Bước 1: Quét và Nhận diện Biển số tại Cổng Vào (`LookupLicensePlateAsync`)
Khi xe vừa tới cổng trạm rửa, Camera AI (hoặc Lễ tân nhập biển số trên POS) gọi API để tra cứu trạng thái xe:
* **Endpoint:** `GET /api/v1/admin/bookings/by-license-plate/{licensePlate}`
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_STAFF_HOAC_ADMIN}` *(Token chứa `BranchId` của chi nhánh hiện tại)*
* **Quyền hạn:** `Staff`, `Manager`, `Admin`

##### 📤 Response 1A: Khách đã Đặt lịch trước (`CustomerType: "PreBooked"`)
Hệ thống tìm thấy lượt đặt lịch hợp lệ trong ngày hôm nay với trạng thái `Pending` hoặc `Confirmed`.
```json
{
  "statusCode": 200,
  "message": "Success",
  "data": {
    "customerType": "PreBooked",
    "data": {
      "bookingId": 101,
      "licensePlate": "51F-12345",
      "serviceNames": [
        "Rửa xe chăm sóc VIP (VIP Wash)",
        "Dưỡng bóng sơn xe (Ceramic Coating)"
      ],
      "scheduledTime": "2026-07-16T09:00:00Z",
      "status": "Pending",
      "originalPrice": 250000,
      "pointDiscountAmount": 20000,
      "voucherDiscountAmount": 30000,
      "finalAmount": 200000,
      "paymentStatus": "Completed"
    }
  }
}
```
👉 **Hướng dẫn triển khai FE (Staff POS / Tablet Tiếp đón):**
- Hiển thị Card thông tin đơn đặt trước: Biển số, Danh sách dịch vụ, Giờ hẹn, `finalAmount`.
- **Kiểm tra trạng thái thanh toán (`paymentStatus`):**
  - Nếu `paymentStatus == "Completed"` (hoặc `finalAmount == 0`): Hiển thị huy hiệu xanh **"✔ ĐÃ THANH TOÁN ONLINE"**. Nút bấm hành động: **[🚀 Cho xe vào khoang rửa tự động]** (gọi API Check-in Camera AI bên dưới).
  - Nếu `paymentStatus == "Unpaid"`: Hiển thị cảnh báo vàng/đỏ **"⚠️ CHƯA THANH TOÁN (Hóa đơn: 200,000đ)"**. Nút bấm hành động: **[💳 Tạo mã QR / Thu tiền tại quầy]** $\rightarrow$ Sau khi thanh toán xong mới cho vào khoang rửa.

##### 📤 Response 1B: Khách Vãng Lai / Xe chưa đặt lịch (`CustomerType: "WalkIn"`)
Khi xe không có booking nào hôm nay, API kiểm tra hồ sơ khách hàng và trả về `WalkIn`:
```json
{
  "statusCode": 200,
  "message": "Success",
  "data": {
    "customerType": "WalkIn",
    "data": {
      "userId": 5,
      "customerName": "Nguyễn Văn A (Khách cũ)",
      "phoneNumber": "0901234567",
      "vehicleId": 15
    }
  }
}
```
*(Ghi chú: Nếu xe hoàn toàn lạ chưa từng đăng ký trên App, trường `data.data` sẽ là `null`).*

👉 **Hướng dẫn triển khai FE (Staff POS / Tablet Tiếp đón):**
- Hiển thị Badge: **"🚶 KHÁCH VÃNG LAI (WALK-IN)"** kèm thông tin Khách quen (nếu `data.data != null`) hoặc Xe mới (nếu `data.data == null`).
- Nút bấm hành động duy nhất: **[➕ Tạo Đơn Walk-In Ngay]** $\rightarrow$ Mở form chọn dịch vụ và thanh toán tại chỗ (chuyển sang Bước 2B).

---

#### 📍 Bước 2A: Luồng Check-in Khoang Rửa Tự Động cho Khách Đã Đặt Trước (`PreBooked Check-in`)
* **Endpoint:** `POST /api/v1/automated-wash/check-in?plate={licensePlate}&branchId={branchId}&autoStart=true`
* **Headers:** `AllowAnonymous` *(hoặc Token Kiot Camera AI)*
* **Mô tả:** Camera trước khoang rửa tự động quét biển số, tự động mở rào chắn Barie và chuyển trạng thái đơn hàng sang `Processing` (Đang rửa xe).

##### 📤 Response thành công (Mở Barie & Bắt đầu rửa):
```json
{
  "statusCode": 200,
  "message": "Vehicle 51F-12345 is valid! Barrier opened and wash cycle started automatically.",
  "data": {
    "bookingId": 101,
    "licensePlate": "51F12345",
    "status": "Processing",
    "processingStartTime": "2026-07-16T09:05:00+07:00"
  }
}
```
👉 **UI Kiot Cổng vào:** Hiển thị màn hình xanh: **"✔ XIN CHÀO XE 51F-123.45! BARIE ĐÃ MỞ, MỜI VÀO KHOANG RỬA."**

##### 📤 Response bị từ chối do CHƯA THANH TOÁN (Lỗi 400):
> [!IMPORTANT]
> **Quy tắc chặn Barie đầu vào:** Nếu đơn đặt trước có `FinalAmount > 0` nhưng chưa hoàn tất thanh toán (`PaymentStatus == "Unpaid"`), Camera AI sẽ **cấm mở rào chắn rẽ vào khoang rửa tự động** và trả lỗi 400.

```json
{
  "statusCode": 400,
  "message": "Booking is unpaid; cannot check in to the automated bay."
}
```
👉 **UI Kiot Cổng vào & Staff Alert:** Hiển thị thông báo Đỏ: **"⛔ XE CHƯA THANH TOÁN ĐƠN ĐẶT TRƯỚC! Vui lòng liên hệ Lễ tân / thanh toán qua App để mở Barie."**

---

#### 📍 Bước 2B: Luồng Tạo Đơn cho Khách Vãng Lai (`Create Walk-In Booking`)
Vì khách vãng lai không có `BookingId` sẵn nên Camera AI không thể tự động mở rào chắn ngay lập tức (`API check-in` sẽ báo lỗi `404 Not Found`). Nhân viên tại quầy thực hiện tạo đơn Walk-in:

* **Endpoint:** `POST /api/v1/bookings/walk-in`
* **Headers:** `Authorization: Bearer {JWT_TOKEN_CUA_STAFF}`
* **Quyền hạn:** `Staff`, `Manager`, `Admin`

##### 📥 Request Body
```json
{
  "userId": 5,           // Truyền userId lấy được từ Bước 1B (hoặc 0 nếu là xe lạ hoàn toàn)
  "branchId": 1,
  "licensePlate": "30G-88888",
  "vehicleTypeId": 1,
  "serviceIds": [1, 3],  // Các dịch vụ khách chọn tại quầy
  "paymentMethod": "Cash", // Cash, VNPay, Card...
  "notes": "Khách vãng lai đến trạm",
  "forceOverrideCapacity": false // [NÂNG CẤP MỚI]: Quản lý bật true để nhét xe Walk-in vào khi khách Booking đi trễ quá giờ hẹn và trạm đang kín tải!
}
```

##### 📤 Response thành công (Đơn tạo & Vào thẳng trạng thái `Processing`):
```json
{
  "statusCode": 201,
  "message": "Walk-in booking created successfully.",
  "data": {
    "bookingId": 102,
    "licensePlate": "30G88888",
    "serviceNames": ["Rửa xe tiêu chuẩn", "Xịt gầm xe"],
    "scheduledTime": "2026-07-16T09:10:00Z",
    "status": "Processing",
    "finalAmount": 120000,
    "processingStartTime": "2026-07-16T09:10:00+07:00"
  }
}
```
> [!TIP]
> **Đặc điểm kỹ thuật cốt lõi của Walk-In:** Khác với khách đặt trước phải qua trạng thái `Pending` rồi mới `Check-in`, API tạo đơn Walk-In (`CreateWalkInBookingAsync`) sẽ **gán ngay lập tức `Status = "Processing"` (Đang rửa)** và tự động tạo Giao dịch thanh toán (`TransactionType = WalkInPayment`, `Status = Completed`). Do đó ngay khi Staff bấm Tạo đơn xong, xe được tính là đang rửa trong khoang và Staff có thể ra lệnh mở cổng/vào khoang ngay.

---

#### 👑 Bước 3: Đặc tả Xử lý & Đặc Quyền cho Khách VIP (`Gold` / `Platinum Members`)
Hệ thống AutoWashPro cung cấp cơ chế ưu đãi đặc quyền toàn diện cho khách hàng có thành viên VIP (`Gold` và `Platinum`) mà Frontend cần lưu ý khi xây dựng giao diện:

##### 1. Khung giờ độc quyền VIP trên Customer App (`TimeSlot.IsVipOnly = true`)
- Trong API danh sách khung giờ (`GetAvailableSlotsAsync` & `check-slots-with-suggestions`), có các khung giờ/khoang rửa được cắm cờ `isVipOnly: true`.
- **Nếu là Khách VIP (Diamond/Gold/Platinum hoặc Hạng có điểm $\ge 5,000$):** Các slot này hiển thị `isAvailable: true` bổi bật với huy hiệu **[👑 VIP Exclusive Slot]**. Khách VIP được ưu tiên giữ chỗ riêng, không bao giờ lo hết suất hay xếp hàng dài.
- **Nếu là Khách Thường (Bronze/Silver/Guest):** Các slot này trả về `isAvailable: false`, `reason: "VIP only"`.
  - 👉 **UX cho FE:** Khi khách thường cố bấm hoặc xem slot này, hiển thị Card khuyến khích nâng hạng: *"🔒 Khung giờ ưu tiên dành riêng cho Hội viên VIP (từ hạng Gold trở lên). Tích thêm điểm để nâng hạng ngay!"*
  - Nếu khách thường cố tình gửi request đặt slot VIP (`ValidateBookingCompatibilityAsync`), API trả lỗi 400: `"This time slot is exclusive to VIP members (Gold tier or above)."`

##### 2. Cửa sổ đặt lịch dài ngày hơn (`BookingWindowDays`)
- Khách VIP có `BookingWindowDays` lớn hơn (ví dụ: được giữ chỗ trước 14-30 ngày so với 3-7 ngày của khách thường). FE sử dụng thông số `BookingWindowDays` từ `TierResponseDTO` của profile khách hàng để giới hạn khoảng ngày (`maxDate`) trên bộ chọn lịch Calendar.

##### 3. Quy trình Tiếp đón Khách VIP tại Trạm Rửa (`VIP Arrival Handing`)
- Khi Lễ tân quét biển số xe ở **Bước 1** (`LookupLicensePlateAsync`), nếu kiểm tra profile/tier của khách thuộc hạng **Gold / Platinum**, FE cần hiển thị giao diện tiếp đón đặc biệt:
  - **Banner nhận diện VIP:** Đổi viền thẻ xe sang màu Vàng Gold/Bạch Kim rực rỡ kèm hiệu ứng và huy hiệu: **"👑 HỘI VIÊN VIP - GOLD MEMBER"** hoặc **"👑 HỘI VIÊN VIP - PLATINUM MEMBER"**.
  - **Lời chào ưu tiên trên Kiot AI:** *"Xin chào quý khách VIP Nguyễn Văn A! Cảm ơn quý khách đã trở lại."*
- Nếu Khách VIP đến không đặt trước (`Walk-In VIP`), nhân viên tạo đơn qua API `walk-in` và xe được ưu tiên điều phối vào làn nhanh nhất.

##### 4. Nhân điểm tích lũy sau khi hoàn thành dịch vụ (`Post-Wash Point Multiplier`)
- Khi xe VIP hoàn tất rửa xe (`check-out` hoặc `ProcessOverdueAutomatedWashesAsync`), hệ thống tự động áp dụng **Hệ số nhân điểm (`Tier.PointMultiplier`)** của hạng VIP (ví dụ $1.5\times$, $2.0\times$):
  $$\text{Points Earned} = \left(\frac{\text{FinalAmount}}{\text{VndPerEarnedPoint}}\right) \times \text{PointMultiplier}$$
- 👉 **UI Notification trên Customer App:** Sau khi hoàn tất lượt rửa, gửi thông báo Push/Popup chúc mừng: *"🎉 Bạn vừa hoàn thành dịch vụ và tích lũy được **+{pointsEarned} điểm thưởng** (Đã nhân hệ số x{PointMultiplier} đặc quyền VIP {TierName})!"*

---

## 🛠️ TỔNG KẾT & LIÊN HỆ HỖ TRỢ
- Mọi API trên đều trả về format chuẩn JSON: `{ "statusCode": 200, "message": "Success", "data": ... }`.
- Nếu gặp lỗi nghiệp vụ (Voucher hết hạn, sai chi nhánh, xe chưa thanh toán, không tìm thấy...), Backend sẽ trả về `statusCode: 400/404` kèm trường `message` mô tả chi tiết tiếng Anh/tiếng Việt.
- Chúc đội ngũ Frontend tích hợp thành công và tạo ra giao diện thật mượt mà, ấn tượng cho người dùng! 🚀
