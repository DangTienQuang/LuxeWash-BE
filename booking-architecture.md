# TỔNG HỢP KIẾN TRÚC VÀ QUY TRÌNH VẬN HÀNH BOOKING - PHÂN LUỒNG VIP - TỐI ƯU DOANH THU

> **Mục tiêu:** Hiểu sâu toàn bộ luồng tiếp đón khách hàng, cơ chế quản lý làn rửa/sức chứa trạm, phân quyền VIP nhiều thứ hạng và chiến lược tối ưu hóa công suất trạm khi khách đến sớm/trễ.

---

## 1. TỔNG QUAN KIẾN TRÚC LÀN RỬA & PHÂN LUỒNG KHÁCH HÀNG (`LANES & QUEUE ALLOCATION`)

### 1.1 Bản chất dữ liệu Làn trên hệ thống (`Lanes`)
Trong bảng `Lanes` của cơ sở dữ liệu `AutoWashDbContext`, mỗi Chi nhánh (`Branch`) được cấu hình nhiều Làn rửa xe độc lập (Ví dụ: Làn 1 - Máy rửa tự động AI, Làn 2 - Khoang chăm sóc chi tiết/Detaling, Làn 3 - Rửa xe tiêu chuẩn).
- Cột `IsBusinessLane`: Được thiết kế đặc thù để định danh làn ưu tiên hoặc dành riêng cho xe hợp đồng doanh nghiệp (Fleet/Business).
- **Với Khách hàng VIP cá nhân (Personal VIP):** Hệ thống **không khóa cứng (hard-lock)** một làn nhất định cho VIP mà áp dụng mô hình **Điều phối Động (`Dynamic Allocation`)**.

### 1.2 Tại sao không khóa cứng "IsVipLane" mà dùng Điều phối Động?
1. **Tránh lãng phí công suất trống:** Nếu khóa chết Làn 2 chỉ cho VIP, khi không có khách VIP nào tại xưởng, Làn 2 sẽ để trống lãng phí trong khi khách thường ở Làn 1 phải xếp hàng dài.
2. **Quyền lực phân luồng linh hoạt của Staff (`Flexible Lane Operations`):** 
   - Với các đơn `Walk-In` hoặc check-in tại quầy, nhân viên tiếp đón (`Staff/Manager`) có quyền gán xe vào **bất kỳ làn nào đang trống và tốt nhất tại thời điểm đó (`ProcessingLaneId`)**.
   - Khách VIP được bảo đảm quyền ưu tiên bằng **Khung giờ giữ suất ưu tiên (`TimeSlot.IsVipOnly`)** và **Quyền được đưa thẳng vào trạng thái `Processing` (Đang rửa)** mà không phải xếp hàng sau các xe `Pending` của khách thường.

```
[Khách VIP tới cổng] ──(AI/POS Quét biển số)──> [Nhận diện Hạng VIP / Pre-booked]
                                                         │
                                    ┌────────────────────┴────────────────────┐
                                    ▼                                         ▼
                           [Khoang Tự Động Làn 1]                    [Khoang Thủ Công Làn 2]
                     • AI mở Barie tự động ngay lập tức.       • Staff bấm Check-in vào Làn 2 trống.
                     • Bỏ qua xếp hàng sau xe vãng lai.        • Nhảy thẳng lên Top 1 Priority Queue.
```

---

## 2. CƠ CHẾ QUẢN LÝ 4 HẠNG THÀNH VIÊN ĐỘNG (`MULTI-TIER ARCHITECTURE`)

Hệ thống được xây dựng với kiến trúc mở qua bảng `Tiers`, cho phép Quản trị viên cấu hình động N hạng thành viên (Thay vì cố định 2 hay 3 hạng). Ví dụ cấu hình 4 hạng tiêu chuẩn:

| Hạng Thành Viên | MinAccumulatedPoints (Điểm tối thiểu) | PointMultiplier (Hệ số tích điểm) | BookingWindowDays (Cửa sổ đặt trước) | Đặc quyền Khung giờ VIP (`IsVipOnly`) |
| :--- | :--- | :--- | :--- | :--- |
| **Đồng (Bronze)** | `0` điểm | **1.0x** | 3 ngày | Không |
| **Bạc (Silver)** | `1,000` điểm | **1.2x** | 7 ngày | Không |
| **Vàng (Gold)** | `5,000` điểm | **1.5x** | 14 ngày | **Có** |
| **Kim Cương (Diamond)** | `15,000` điểm | **2.0x** | 30 ngày | **Có (Tối cao)** |

### 2.1 Cơ chế thăng/giáng hạng tự động (`EvaluateTierForProfileAsync`)
Mỗi khi một đơn hàng hoàn thành (`Completed`), hệ thống quét bảng `Tiers` theo `MinAccumulatedPoints` từ cao xuống thấp (`OrderByDescending`) để tự động cập nhật `TierId` mới nhất cho khách hàng:
```csharp
var eligibleTier = await _context.Tiers
    .Where(t => t.MinAccumulatedPoints <= profile.CurrentYearTierPoints)
    .OrderByDescending(t => t.MinAccumulatedPoints)
    .FirstOrDefaultAsync();
```

### 2.2 Phân quyền và Xử lý khi 4 Hạng cùng check-in một thời điểm
Khi sân chờ trạm rửa có nhiều xe cùng xuất hiện (Đồng, Bạc, Vàng, Kim Cương, Vãng lai):
1. **Tầng 1 - Quyền Giữ Suất Từ Trước:** Khách Kim Cương/Vàng đặt trước (`Pre-booked`) đã được trừ tải khỏi `DailySlotCapacities`. Xe Vãng lai đến sau không bao giờ chiếm được suất của họ.
2. **Tầng 2 - Thứ tự Ưu tiên Hàng đợi (`Queue Prioritization`):** Khách Kim Cương/Vàng luôn được ưu tiên phục vụ trước trong danh sách chờ của nhân viên.
3. **Tầng 3 - Tốc độ tích điểm & Mã giảm giá cá nhân hóa:** Khách Kim Cương tích điểm nhanh gấp đôi ($2.0\times$) và nhận các chiến dịch Voucher độc quyền (`RequiredTierId`).

---

## 3. QUẢN LÝ SỨC CHỨA THEO NGÀY & KỊCH BẢN KHÁCH ĐẾN SỚM (`DAILY CAPACITY & EARLY ARRIVALS`)

### 3.1 Bản chất bảng theo dõi công suất từng ngày (`DailySlotCapacities`)
Mặc dù mỗi Khung giờ có giới hạn tải (`MaxCapacity`), Backend không trừ tải tĩnh trên `TimeSlot` mà lưu tại bảng động **`DailySlotCapacities` (BranchId, SlotId, Date, BookedWeight)**.
- Khi Khách VIP đặt trước 5 ngày (ví dụ đặt ngày `20/07`), hệ thống **chỉ ghi nhận tải `BookedWeight` vào đúng bản ghi ngày `20/07`**.
- Các ngày trước đó (`15, 16, 17, 18, 19/07`) hoàn toàn không bị ảnh hưởng, trạm vẫn rảnh $100\%$ tải cho khách khác.

### 3.2 Xử lý Kịch bản Khách VIP Đến Sớm (`Early Arrival`)

#### ⏰ Trường hợp A: Đến sớm hẳn trước vài ngày (Ví dụ: Đặt ngày 20/07 nhưng Thứ Hai 15/07 mang xe tới)
- **Trên hệ thống:** Khi Lễ tân/AI quét biển số (`LookupLicensePlateAsync`), Backend kiểm tra điều kiện ngày hẹn hôm nay (`b.ScheduledTime.Date == todayInVN`). Do lịch đặt là ngày `20/07`, đơn hẹn sẽ không khớp trong hôm nay $\rightarrow$ Hệ thống tự động chuyển xe sang luồng **Khách VIP đến ngẫu nhiên (`Walk-In VIP`)** của ngày `15/07`.
- **Kết quả:** Khách vẫn được tạo đơn Walk-In vào rửa luôn, tích lũy điểm VIP cho ngày `15/07`. Suất đặt giữ chỗ cho ngày `20/07` **vẫn được bảo lưu nguyên vẹn** (Khách có thể dùng tiếp vào ngày `20/07` hoặc bấm Hủy trên App để được hoàn tiền/điểm về ví).

#### ⏰ Trường hợp B: Đến sớm vài tiếng trong cùng ngày hẹn (Ví dụ: Đặt 15:00 chiều nhưng 09:00 sáng đã tới)
- **Trên hệ thống:** Vì cùng ngày hôm nay (`ScheduledTime.Date == todayInVN`), hệ thống tìm ra ngay đơn `PreBooked`.
- **Kết quả:** Nếu xưởng đang có khoang trống, Lễ tân/AI cho phép **Check-in sớm (`Early Check-In`)** chuyển sang `Processing`. Khi rửa xong vào sáng (`Completed`), khung giờ 15:00 chiều của trạm lập tức được trả lại chỗ trống cho khách khác!

---

## 4. CHIẾN LƯỢC TỐI ƯU DOANH THU THEO QUYẾT ĐỊNH HỘI ĐỒNG (`LATE ARRIVALS & WALK-IN BYPASS`)

### 4.1 Bài toán Quản trị Doanh thu (`Revenue & Asset Optimization`)
Hội Đồng Quản Trị chỉ đạo: **"Khi Khách Đặt Trước (`Pre-Booked`) đến trễ giờ, xưởng PHẢI được phép nhường khoang cho Khách Vãng Lai (`Walk-In`) tiến vào lấp chỗ trống ngay lập tức để không bị lãng phí máy móc, tối ưu hóa thời gian và tối đa hóa doanh thu!"**

### 4.2 Tại sao logic code cũ cản trở và cách giải quyết kỹ thuật
Nếu Khách Đặt Trước đi trễ mà đơn hàng vẫn ở trạng thái `Pending` (chưa hủy), thì `BookedWeight` tại khung giờ đó đang bị chiếm trọn (`BookedWeight == MaxCapacity`). Khi Lễ tân tạo đơn `Walk-In`, API sẽ ném lỗi *"Insufficient shop capacity..."*.

Để giải quyết triệt để và thực thi chiến lược của Hội Đồng, hệ thống kết hợp 2 giải pháp:

```
                  [Khách Booking đến trễ quá 15 phút (SlotGraceMinutes = 15)]
                                               │
               ┌───────────────────────────────┴───────────────────────────────┐
               ▼                                                               ▼
   【GIẢI PHÁP 1: HỦY ĐƠN TRỄ NHẢ SUẤT】                       【GIẢI PHÁP 2: QUẢN LÝ BYPASS GHI ĐÈ】
• Lễ tân/Cron Job hủy đơn trễ -> `Cancelled`.            • Quản lý bật cờ `ForceOverrideCapacity = true`.
• `BookedWeight -= CapacityWeight` (Nhả tải).            • Backend bỏ qua check `MaxCapacity` ở dòng 1880.
• Lễ tân tạo đơn Walk-In hợp lệ 100%.                     • Nhét xe Walk-In vào làm NGAY LẬP TỨC!
```

---

## 5. KỊCH BẢN ỨNG XỬ KHI KHÁCH ĐẾN TRỄ PHÚT 20 - 30 VÌ KẸT XE (`TRAFFIC JAM CONTINGENCY`)

Đây là tình huống thực tế thường gặp nhất: Xưởng vừa cho xe Walk-in tiến vào khoang rửa ở phút 15 (theo chỉ đạo của Hội Đồng), thì đến **phút 20 hoặc 30** Khách Đặt Trước mới thoát khỏi chỗ kẹt xe và lái xe tới cổng!

### 5.1 Kịch bản xử lý chuyên nghiệp tại Trạm & Hệ thống

| Tiêu Chí | Kịch bản khi dùng Giải Pháp 1 (Đã Hủy Đơn lúc Phút 15) | Kịch bản khi dùng Giải Pháp 2 (Đơn vẫn giữ `Pending`) |
| :--- | :--- | :--- |
| **Trạng thái đơn trên DB** | Đơn cũ lúc 09:00 đã chuyển `Cancelled / Expired`. | Đơn cũ 09:00 vẫn ở trạng thái `Pending`. |
| **Thao tác POS của Lễ tân** | Tiếp nhận khách sang luồng **Walk-In mới** (hoặc bấm **Reschedule** khôi phục sang khung giờ hiện tại `09:30 - 10:30`). | Lễ tân / AI vẫn quét ra đơn `PreBooked` hợp lệ vì cùng ngày (`todayInVN`) $\rightarrow$ Bấm **Late Check-In (`CheckedIn`)**. |
| **Thứ tự Hàng đợi tại xưởng** | Nếu là Khách VIP (Vàng/Kim Cương), hệ thống tự động đẩy lên **TOP 1 Priority Queue** ngay sau chiếc xe đang rửa trong khoang. | Xe vào sân chờ (`CheckedIn`). Đợi chiếc xe Walk-In trong khoang rửa xong là vào làm ngay. |
| **Xử lý tiền đã thanh toán trước (Prepaid)** | Tiền đơn cũ đã được hoàn tự động vào Ví (`Wallet.Balance`). Khi tạo đơn mới, Lễ tân chỉ cần bấm chọn thanh toán qua Ví (`Wallet Payment`). | Đơn cũ được tiếp tục xử lý đến khi `Completed` $\rightarrow$ Tiền đã thanh toán trước được giữ nguyên hoàn tất. |

### 5.2 Kịch bản Giao tiếp chuẩn mực cho Lễ Tân (`Standard Communication Script`)
Khi khách đến trễ 25 phút vì kẹt xe, Lễ tân/Staff tuyệt đối không trách móc hay từ chối phục vụ, mà ứng xử thấu tình đạt lý:

> **Lễ tân nói:** *"Dạ chào anh/chị! Lúc nãy quá giờ hẹn 15 phút xưởng tưởng mình có việc đột xuất không tới được nên vừa nhường khoang cho một chiếc xe vãng lai tiến vào rửa trước rồi ạ. Chiếc xe đó sắp rửa xong rồi (chỉ còn khoảng 5-10 phút nữa thôi). Em đã làm thủ tục check-in ưu tiên cho xe mình ngay bây giờ, mời anh/chị vào phòng chờ uống nước, ngay khi khoang rửa trống là xưởng đưa xe mình vào làm ngay lập tức nhé ạ!"*

👉 **Kết quả đạt được:** 
1. Xưởng **thu trọn doanh thu** từ chiếc xe Walk-In vừa thế chỗ.
2. Khách đến trễ không bị mất đơn hay bị phạt, chỉ cần nhượng bộ ngồi chờ vài phút.
3. Tối đa hóa $100\%$ công suất xưởng, hài lòng cả Hội Đồng Quản Trị lẫn Khách hàng VIP!

---

## 6. CHI TIẾT CÁC TÍNH NĂNG ĐÃ NÂNG CẤP & TÍCH HỢP TRONG CODEBASE (`IMPLEMENTED TECHNICAL UPGRADES`)

> **🎉 TRẠNG THÁI TRIỂN KHAI: [✅ ĐÃ HOÀN THÀNH & TÍCH HỢP TRONG CODEBASE - NGÀY 15/07/2026]**  
> Cả 3 điểm nâng cấp kỹ thuật dưới đây đã được lập trình trực tiếp vào Backend (`SmartWash-BE`) và biên dịch thành công (`0 Error(s)`).

### 🛠️ Nâng cấp 1: Tối ưu Sắp xếp Hàng đợi Ưu tiên Theo Thứ bậc Hạng (`Priority Queue Sorting`) `[✅ ĐÃ XONG]`
- **Vị trí code đã nâng cấp:** `OperationStaffService.cs` $\rightarrow$ Hàm `GetAssignedBookingsAsync` & `StaffBookingDTO.cs`.
- **Chi tiết thực thi:** 
  1. Bổ sung 2 thuộc tính mới `CustomerTierName` (Tên hạng) và `CustomerTierPoints` (Điểm tích lũy tối thiểu) vào `StaffBookingDTO`.
  2. JOIN các bảng `Bookings -> User -> CustomerProfile -> Tier` và áp dụng sắp xếp kép: Ưu tiên theo ngưỡng điểm `MinAccumulatedPoints` giảm dần (`OrderByDescending`), sau đó đến thời gian đặt lịch `ScheduledTime` tăng dần (`ThenBy`).
- **Thứ tự hàng đợi thực tế trên POS/Tablet Staff:**
  1. 💎 **Kim Cương (Diamond Member)** - `15,000 điểm` (Luôn đứng TOP 1)
  2. 👑 **Vàng (Gold Member)** - `5,000 điểm` (TOP 2)
  3. 🥈 **Bạc (Silver Member)** - `1,000 điểm` (TOP 3)
  4. 🥉 **Đồng (Bronze Member)** - `0 điểm` (TOP 4)
  5. 🚶 **Vãng lai (Walk-In / Standard)** - `-1 điểm` (Phục vụ sau các xe thành viên cùng thời điểm)

### 🛠️ Nâng cấp 2: Mở rộng Điều kiện Khung giờ VIP Động (`Dynamic IsVipOnly Check`) `[✅ ĐÃ XONG]`
- **Vị trí code đã nâng cấp:** `BookingService.cs` $\rightarrow$ Hàm `GetAvailableSlotsAsync` (dòng 97) và `CreateBookingAsync` (dòng 936).
- **Chi tiết thực thi:** Loại bỏ việc kiểm tra cứng chuỗi `"gold" || "platinum"`. Hệ thống đánh giá khách VIP dựa trên ngưỡng điểm $\ge 5,000$ hoặc tên hạng mở rộng (`Diamond`, `Platinum`, `Gold`):
  ```csharp
  bool isVip = userProfile.Tier != null && (userProfile.Tier.MinAccumulatedPoints >= 5000 
               || string.Equals(userProfile.Tier.TierName, "Gold", StringComparison.OrdinalIgnoreCase) 
               || string.Equals(userProfile.Tier.TierName, "Platinum", StringComparison.OrdinalIgnoreCase) 
               || string.Equals(userProfile.Tier.TierName, "Diamond", StringComparison.OrdinalIgnoreCase));
  ```
- **Ý nghĩa vận hành:** Khi trạm mở rộng các hạng VIP mới hoặc điều chỉnh tên hạng, toàn bộ luồng chọn khung giờ độc quyền (`IsVipOnly`) trên Customer App tự động tương thích mà không cần sửa code Backend.

### 🛠️ Nâng cấp 3: Bổ sung Cờ `ForceOverrideCapacity` Cho Quản Lý Lấp Chỗ Trống Khi Khách Trễ `[✅ ĐÃ XONG]`
- **Vị trí code đã nâng cấp:** `BookingDTOs.cs` (`CreateWalkInBookingDTO`) & `BookingService.cs` (`CreateWalkInBookingAsync`).
- **Chi tiết thực thi:** Thêm cờ `public bool ForceOverrideCapacity { get; set; } = false;` vào DTO request. Trong logic xử lý Walk-in:
  ```csharp
  if (!request.ForceOverrideCapacity && dailyCapacity.BookedWeight + maxCapacityWeight > slot.MaxCapacity)
  {
      throw new AutoWashPro.BLL.Exceptions.BadRequestException("Insufficient shop capacity for this vehicle right now. Please try again later.");
  }
  ```
- **Cơ chế vận hành:** Khi Khách Đặt Trước đi trễ quá 15 phút mà đơn vẫn đang giữ suất `Pending` (chưa hủy), Lễ tân/Quản lý chỉ cần bật cờ `ForceOverrideCapacity: true` khi tạo đơn Walk-in. Hệ thống bỏ qua bước chặn tải, lập tức đưa xe vãng lai vào khoang rửa (`Processing`), đảm bảo xưởng luôn lấp đầy công suất theo đúng chỉ đạo của Hội Đồng Quản Trị!

---

## 6. KIẾN TRÚC HỆ SINH THÁI AI NHÚNG TỰ ĐỘNG HÓA (`EMBEDDED AUTOMATED AI ENGINE`) & CƠ CHẾ KÍCH CẦU DOANH THU

Hệ thống SmartWash Pro tích hợp một **Hệ sinh thái AI lai (Hybrid AI Ecosystem)** vận hành hoàn toàn ngầm bên dưới luồng nghiệp vụ (Embedded Pipeline), thay cho việc con người phải xuất file hoặc paste số liệu thủ công vào các AI Chatbot bên ngoài.

### 6.1 Sơ đồ 3 Trụ cột AI trong Hệ thống
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

### 6.2 So sánh AI Thủ công vs AI Nhúng Tự Động Hóa trong SmartWash Pro
- **Nếu dùng AI Chatbot thủ công:** Quản lý phải tự xuất báo cáo Excel $\rightarrow$ Copy & Paste số liệu vào ChatGPT/Claude $\rightarrow$ Gõ câu lệnh hỏi $\rightarrow$ Đọc kết quả văn bản $\rightarrow$ Lại tự đăng nhập vào hệ thống gõ tay tạo từng mã Voucher, tự chọn ngày hết hạn, % giảm giá $\rightarrow$ Rất tốn thời gian và dễ sai lệch.
- **Trong SmartWash Pro (Tự động hóa 100%):**
  1. **Tự chọc thẳng vào Database:** Khi Quản lý bấm nút *Phân tích Kích cầu* (`POST /api/v1/manager/revenue-stimulus/comprehensive-proposals`), Backend (`BranchRevenueAnalyticsService`) tự động đọc trực tiếp dữ liệu từ các bảng `Bookings`, `Vouchers`, `CustomerFeatureProfiles` trong tích tắc.
  2. **Tự chạy suy luận & tính toán hành vi:** 
     - Tự tính toán tỷ lệ sụt giảm doanh thu tháng này so với tháng trước (`CurrentMonthRevenue` vs `PreviousMonthRevenue`).
     - Tự gom nhóm lịch sử `Bookings` theo ngày trong tuần để phát hiện 2 ngày có lưu lượng xe thấp nhất (`SlowestDaysOfWeek`, ví dụ: *Thứ 3, Thứ 4*).
     - Tự lọc tập khách hàng thân thiết ($\ge 2$ lần ghé) đã **trên 45 ngày chưa ghé xưởng** (`AtRiskLoyalCustomersCount`).
  3. **Tự khởi tạo bản ghi Voucher vào hệ thống:** AI trực tiếp khởi tạo sẵn 2 bản ghi `Voucher` vào Database với trạng thái `ApprovalStatus = "Proposed"` (Đang chờ duyệt):
     - **Voucher 1 (`OFFPEAK_WEEKDAY`)**: Giảm $10\% - 20\%$ cho các ngày trong tuần vắng xe để lấp đầy công suất Làn rửa.
     - **Voucher 2 (`LOYAL_WINBACK`)**: Giảm $15\% - 25\%$ tri ân nhóm khách VIP >45 ngày chưa tới để kéo họ quay lại ngay.
     - Kèm theo **Ghi chú giải thích AI (`ProposalNote`)** tường tận cho Quản lý hiểu rõ lý do đề xuất.
  4. **👉 Trải nghiệm tối thượng cho Quản lý:** Quản lý chỉ việc mở màn hình Dashboard là đã thấy AI phân tích xong, vẽ sẵn Voucher và giải thích lý do. Quản lý chỉ cần bấm **1 nút duy nhất [✔ Phê duyệt (`Approve`)]** là Voucher chính thức phát hành thẳng vào ví của khách hàng!

### 6.3 Cơ chế Điều phối Cứu hộ Kẹt Tải Khẩn Cấp (Proactive Walk-in Surge Relocation)
Khi chi nhánh bất ngờ đón lượng khách Vãng lai (Walk-in) tăng vọt, hệ thống hỗ trợ cơ chế quét và "sơ tán" khách đặt trước để bảo vệ trải nghiệm của họ:
1. **Quét và Phát hiện (`Scan & Notify`)**: Hệ thống chủ động rà soát các lịch `Pending` trong 2 giờ tới, tự động tìm chi nhánh lân cận trống lịch.
2. **Tặng Voucher đền bù**: Hệ thống lập tức tạo 1 Voucher "Đền bù dời lịch" trị giá 50,000 VND.
3. **Cơ chế Dời lịch tự động (`Accept Relocation`)**: Khi khách hàng đồng ý, hệ thống tự động dời `BranchId`, áp dụng Voucher giảm 50K trực tiếp vào hóa đơn. Quan trọng nhất, Backend tự động **trừ Tải trọng (`CapacityWeight`) ở chi nhánh bị kẹt** và cộng sang chi nhánh mới, giúp giải phóng ngay lập tức không gian làm việc cho thợ.

---
*Tài liệu tổng hợp bởi đội ngũ Kiến trúc sư Hệ thống SmartWash Pro.*
