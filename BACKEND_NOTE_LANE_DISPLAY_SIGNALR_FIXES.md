# Backend note: hoàn thiện `LaneDisplayHub` để frontend sử dụng realtime

## 1. Bối cảnh và kết luận hiện tại

Backend đã có nền tảng SignalR:

- Hub: `/hubs/lane-display`
- Client event: `ReceiveLaneUpdate`
- Group: `branch:{branchId}:lane-display`
- REST khôi phục trạng thái:
  `GET /api/v1/operations/branches/{branchId}/lane-display/latest`

Tuy nhiên, hiện tại backend **chưa bao phủ đầy đủ luồng camera check-in thực tế** và contract chưa phù hợp với màn hình phân làn tại cổng vào.

Các vấn đề quan trọng:

1. Luồng `POST /api/v1/camera/check-in` gọi
   `BookingService.UpdateBookingStatusByLicensePlateAsync()`, nhưng đường đi này
   không publish đầy đủ event lên `LaneDisplayHub`.
2. Khi check-in mà không có làn trống, backend không publish event `waiting`.
3. DTO hiện bắt buộc `LaneId` và `LaneName`, nên không biểu diễn tốt các trạng
   thái chưa có làn, lỗi, chờ thanh toán hoặc cần Staff hỗ trợ.
4. Các service đang dùng tên event không nhất quán:
   `Assigned`, `Reading`, `Processing`, `Cleared`.
5. `OperationStaffService.CheckInAsync()` đang publish `Reading` sau khi booking
   đã check-in và có làn. Trường hợp này phải là `assigned`.
6. Một số đường gán làn khác như tạo walk-in hoặc webhook thanh toán tự gán
   `ProcessingLaneId` nhưng không publish event.
7. Một số đường hoàn thành/checkout giải phóng làn nhưng không publish `cleared`.
8. Endpoint `latest` nhận `branchId` từ URL nhưng chưa kiểm tra người dùng có
   quyền xem chi nhánh đó hay không.

Frontend sẽ giữ cơ chế local `BroadcastChannel` làm fallback cho Cast/Miracast,
nhưng backend cần trở thành nguồn realtime chính và là source of truth.

---

## 2. Contract realtime đề xuất

### 2.1. Tên SignalR event

Giữ nguyên:

```text
ReceiveLaneUpdate
```

Hub URL:

```text
/hubs/lane-display
```

Group:

```text
branch:{branchId}:lane-display
```

### 2.2. Payload thống nhất

Đề xuất sửa `LaneDisplayEventDTO`:

```csharp
public class LaneDisplayEventDTO
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public int BranchId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // Các giá trị được định nghĩa tại mục 2.3.
    public string Type { get; set; } = null!;

    public int? BookingId { get; set; }
    public string? LicensePlate { get; set; }

    // Phải nullable vì waiting/error/payment/assistance chưa có làn.
    public int? LaneId { get; set; }
    public string? LaneName { get; set; }

    public string? Title { get; set; }
    public string? Message { get; set; }
    public string? ReasonCode { get; set; }
    public DateTime? DisplayUntil { get; set; }
}
```

JSON gửi tới frontend phải dùng camelCase:

```json
{
  "eventId": "uuid",
  "branchId": 1,
  "occurredAt": "2026-07-28T15:30:00Z",
  "type": "assigned",
  "bookingId": 123,
  "licensePlate": "30F33333",
  "laneId": 2,
  "laneName": "Làn 2",
  "title": null,
  "message": null,
  "reasonCode": null,
  "displayUntil": "2026-07-28T15:30:15Z"
}
```

### 2.3. Danh sách event type

Không dùng trực tiếp booking status làm display event type. Display event là
contract riêng cho màn hình khách hàng.

| Type | Ý nghĩa | Lane bắt buộc |
|---|---|---:|
| `reading` | Camera vừa nhận biển số, hệ thống đang kiểm tra | Không |
| `assigned` | Check-in thành công và đã phân làn | Có |
| `waiting` | Check-in hợp lệ nhưng hiện chưa có làn trống | Không |
| `payment` | Có booking/walk-in nhưng chưa đủ điều kiện thanh toán | Không |
| `assistance` | Biển số hợp lệ nhưng cần Staff tạo/xử lý lượt rửa | Không |
| `error` | Lỗi không thể tiếp tục tự động | Không |
| `cleared` | Xóa trạng thái hiển thị hiện tại, trở về idle | Có thể có |

Quy ước:

- Giá trị `type` dùng chữ thường đúng như bảng.
- Không gửi `Processing` lên màn hình cổng vào. Xe đã nhận làn thì màn hình chỉ
  cần giữ `assigned` trong thời gian ngắn, sau đó trở về idle.
- `cleared` dùng khi lượt rửa hoàn thành/checkout hoặc khi cần xóa một display
  event trước đó.

### 2.4. Thời gian hiển thị đề xuất

Backend có thể gán `DisplayUntil`:

| Type | Thời gian |
|---|---:|
| `reading` | 10–12 giây |
| `assigned` | 15 giây |
| `waiting` | 20 giây |
| `payment` | 20 giây |
| `assistance` | 20 giây |
| `error` | 20 giây |
| `cleared` | ngay lập tức |

Frontend vẫn được phép áp dụng timeout bảo vệ nếu `DisplayUntil` không có.

---

## 3. Các luồng bắt buộc phải publish

### 3.1. Camera bắt đầu đọc biển số

Endpoint liên quan:

```text
POST /api/v1/camera/check-in?plate=...
```

Trước khi thực hiện nghiệp vụ check-in, publish:

```json
{
  "type": "reading",
  "branchId": 1,
  "licensePlate": "30F33333"
}
```

Để làm được điều này, backend phải xác định được `branchId`.

Khuyến nghị:

- Bỏ `[AllowAnonymous]` khỏi camera controller nếu request được gửi từ Staff FE.
- Dùng `[Authorize(Roles = "Staff,Manager")]`.
- Lấy `branchId` từ EmployeeProfile hoặc claim `BranchId`.
- Không dùng mặc định cứng `branchId = 1`.

Nếu camera/device gọi backend trực tiếp, dùng device API key hoặc device token
được gắn cố định với một branch; không để endpoint check-in/check-out công khai.

### 3.2. Check-in thành công và có làn

Sau khi transaction cập nhật booking đã commit:

```json
{
  "type": "assigned",
  "branchId": 1,
  "bookingId": 123,
  "licensePlate": "30F33333",
  "laneId": 2,
  "laneName": "Làn 2"
}
```

Các đường code cần bao phủ:

- `BookingService.UpdateBookingStatusAsync()`
- `BookingService.UpdateBookingStatusByLicensePlateAsync()`
- `BookingService.AutoCheckInAndStartProcessingAsync()`
- `OperationStaffService.CheckInAsync()`
- `ManagerService` khi Manager phân làn thủ công
- `LaneSchedulerService.AssignNextVehicleInQueueAsync()`
- Luồng tạo walk-in đã thanh toán và được gán làn
- Webhook thanh toán walk-in gán được làn
- Luồng fleet/business nếu xe doanh nghiệp cũng đi qua cùng màn hình cổng vào

Lưu ý hiện tại:

```csharp
OperationStaffService.CheckInAsync()
```

đang publish:

```csharp
Type = "Reading"
```

trong khi booking đã có `ProcessingLaneId`. Phải đổi thành:

```csharp
Type = "assigned"
```

### 3.3. Check-in hợp lệ nhưng hết làn

Khi booking đã chuyển sang `CheckedIn` nhưng:

```csharp
ProcessingLaneId == null
```

publish:

```json
{
  "type": "waiting",
  "branchId": 1,
  "bookingId": 123,
  "licensePlate": "30F33333",
  "laneId": null,
  "laneName": null,
  "reasonCode": "NO_AVAILABLE_LANE",
  "message": "Chưa có làn trống. Vui lòng giữ nguyên vị trí trước barie."
}
```

Đây là nghiệp vụ quan trọng vì xe chờ **trước barie**.

Không được mở barie nếu chưa có làn và mô hình thực tế không có khu vực chờ bên
trong.

Khi một làn được giải phóng, `AssignNextVehicleInQueueAsync()` phải:

1. Gán làn atomically.
2. Commit database.
3. Publish event `assigned` cho đúng xe vừa được lấy khỏi hàng đợi.

### 3.4. Walk-in chưa thanh toán

Khi đã tạo booking walk-in nhưng giao dịch còn `Pending`, publish:

```json
{
  "type": "payment",
  "branchId": 1,
  "bookingId": 123,
  "licensePlate": "30F33333",
  "reasonCode": "BOOKING_PAYMENT_REQUIRED"
}
```

Sau khi webhook xác nhận thanh toán:

- Nếu gán được làn: publish `assigned`.
- Nếu chưa có làn: publish `waiting`.

Hiện tại `WalletService` có đoạn tự gán `ProcessingLaneId` sau thanh toán nhưng
không publish display event. Đường này cần được sửa hoặc refactor để dùng chung
một service gán làn có publish event.

### 3.5. Khách walk-in/chưa có booking

Nếu camera nhận biển số nhưng chưa có booking phù hợp và Staff phải tạo lượt rửa,
publish:

```json
{
  "type": "assistance",
  "branchId": 1,
  "licensePlate": "30F33333",
  "reasonCode": "WALK_IN_REQUIRES_STAFF"
}
```

Không nên dùng exception text tiếng Anh làm logic cho frontend. Cần trả và
publish `reasonCode` ổn định.

### 3.6. Lỗi check-in

Các lỗi không thuộc payment/walk-in/no-lane publish:

```json
{
  "type": "error",
  "branchId": 1,
  "licensePlate": "30F33333",
  "reasonCode": "CHECK_IN_FAILED",
  "message": "Không thể check-in xe. Vui lòng chờ nhân viên hỗ trợ."
}
```

Không đưa stack trace, database message hoặc thông tin nội bộ lên display.

### 3.7. Hoàn thành/checkout và giải phóng làn

Mọi đường chuyển từ `CheckedIn` hoặc `Processing` sang `Completed` phải publish:

```json
{
  "type": "cleared",
  "branchId": 1,
  "bookingId": 123,
  "licensePlate": "30F33333",
  "laneId": 2,
  "laneName": "Làn 2"
}
```

Các đường cần kiểm tra:

- Staff bấm hoàn thành thủ công.
- Camera checkout thành công.
- Automated completion worker.
- Fleet/business checkout.

Thứ tự đề xuất khi giải phóng làn:

1. Commit booking cũ thành `Completed`.
2. Publish `cleared` cho booking cũ.
3. Chạy `AssignNextVehicleInQueueAsync(laneId)`.
4. Nếu có xe kế tiếp, publish `assigned` cho xe đó.

Hiện tại `OperationStaffService` đã gần đúng luồng này, nhưng
`BookingService.UpdateBookingStatusAsync()` và một số đường camera/fleet chưa
publish `cleared`.

---

## 4. Không để sót event: refactor đề xuất

Hiện tại publish được đặt rải rác trong `ManagerService`,
`OperationStaffService` và `LaneSchedulerService`, nên các đường camera,
walk-in, webhook rất dễ bị bỏ sót.

Khuyến nghị tạo một orchestration service duy nhất, ví dụ:

```text
ILaneAssignmentCoordinator
```

Các method gợi ý:

```csharp
Task<LaneAssignmentResult> AssignLaneForCheckedInBookingAsync(int bookingId);
Task PublishWaitingAsync(int bookingId, string reasonCode);
Task PublishAssignedAsync(int bookingId);
Task ReleaseLaneAndAssignNextAsync(int bookingId);
Task PublishDisplayErrorAsync(
    int branchId,
    string? licensePlate,
    string reasonCode,
    string? safeMessage);
```

Mọi nơi cần gán làn phải gọi coordinator thay vì tự:

```csharp
booking.ProcessingLaneId = laneId;
```

Điều này đặc biệt áp dụng cho:

- `BookingService`
- `OperationStaffService`
- `ManagerService`
- `WalletService`
- `BusinessBookingService`
- `LaneSchedulerService`

### Nguyên tắc transaction

- Không publish SignalR trước khi database commit.
- Nếu publish trước commit, frontend có thể hiển thị làn nhưng transaction sau
  đó rollback.
- SignalR không phải transaction. Với demo có thể commit rồi publish và log lỗi.
- Với production nên dùng transactional outbox để đảm bảo event không bị mất khi
  server chết ngay sau commit.

---

## 5. Sửa `LaneDisplayPublisherService`

### 5.1. Trạng thái hiện tại

Service đang lưu:

```text
BranchId -> LaneId -> LatestState
```

Cấu trúc này không lưu được tốt các event:

- `reading`
- `waiting`
- `payment`
- `assistance`
- `error`

vì các event trên chưa có `LaneId`.

### 5.2. Cấu trúc đề xuất

Lưu thêm event mới nhất theo branch:

```csharp
ConcurrentDictionary<int, LaneDisplayEventDTO> _latestBranchEvent;
```

Vẫn có thể giữ lane state riêng nếu Manager cần:

```csharp
ConcurrentDictionary<int,
    ConcurrentDictionary<int, LaneDisplayLatestStateDTO>> _laneStates;
```

Khi publish:

1. Luôn cập nhật `_latestBranchEvent[branchId]`.
2. Chỉ cập nhật `_laneStates` nếu `LaneId.HasValue`.
3. Gửi `ReceiveLaneUpdate` tới group của branch.

### 5.3. Không để event cũ sống vô hạn

Khi gọi endpoint `latest`:

- Nếu `DisplayUntil < DateTime.UtcNow`, không trả event đó làm active event.
- `cleared` phải làm latest state trở về idle.
- Có thể trả event hết hạn trong history nếu cần debug, nhưng không trả dưới
  `latestEvent`.

---

## 6. Sửa endpoint `latest`

Đề xuất response:

```json
{
  "statusCode": 200,
  "message": "Success",
  "data": {
    "branchId": 1,
    "serverTime": "2026-07-28T15:30:00Z",
    "latestEvent": {
      "eventId": "uuid",
      "branchId": 1,
      "occurredAt": "2026-07-28T15:29:55Z",
      "type": "assigned",
      "bookingId": 123,
      "licensePlate": "30F33333",
      "laneId": 2,
      "laneName": "Làn 2",
      "displayUntil": "2026-07-28T15:30:10Z"
    },
    "lanes": []
  }
}
```

Frontend cần `latestEvent` branch-wide để khôi phục màn hình sau refresh hoặc
SignalR reconnect. Danh sách `lanes` là tùy chọn.

### Phân quyền bắt buộc

Hiện endpoint nhận bất kỳ `branchId` nào từ URL. Cần kiểm tra:

- Staff/Manager chỉ được đọc branch trong EmployeeProfile/token.
- Admin chỉ được đọc branch hợp lệ nếu nghiệp vụ cho phép.
- Sai branch trả `403`.

Không chỉ dựa vào branchId do frontend gửi.

---

## 7. Sửa `LaneDisplayHub`

Hub hiện tự tìm EmployeeProfile từ `NameIdentifier`, cách này có thể giữ lại vì
`EmployeeId` đang là shared primary key với `UserId`.

Đề xuất:

1. Giới hạn role:

   ```csharp
   [Authorize(Roles = "Staff,Manager")]
   ```

   Nếu Admin được phép mở display, cần cơ chế chọn branch có kiểm tra quyền;
   không tự cho Admin join tất cả branch.

2. Khi không tìm được branch:

   ```csharp
   Context.Abort();
   return;
   ```

   Không tiếp tục chạy `base.OnConnectedAsync()` sau khi abort.

3. Log có cấu trúc:

   - connectionId
   - userId
   - branchId
   - connected/disconnected
   - exception khi disconnect

4. Không nhận `branchId` tùy ý từ client để join group.

5. Giữ hỗ trợ JWT qua query `access_token` cho WebSocket/SSE như hiện tại.

---

## 8. Security liên quan camera và màn hình

### Camera controller

`CameraController` và `AutomatedWashController` hiện có `[AllowAnonymous]`.
Trong mô hình thực tế, các endpoint này có thể thay đổi trạng thái booking và mở
barie, vì vậy không nên public.

Vì camera nối vào Staff PC và Staff FE gọi API, lựa chọn đơn giản:

```csharp
[Authorize(Roles = "Staff,Manager")]
```

Sau đó lấy branch từ user đang đăng nhập.

Nếu camera/device gọi API trực tiếp:

- Cấp device credential riêng.
- Mỗi credential chỉ thuộc một branch.
- Có thể rotate/revoke credential.
- Rate limit theo device.
- Không dùng một API key chung cho tất cả chi nhánh.

### Display Cast/Miracast

Trường hợp hiện tại display được Cast/Miracast từ Staff PC nên có thể dùng token
Staff đang đăng nhập.

Không lưu Staff password hoặc refresh token trên một thiết bị display công cộng
độc lập. Nếu sau này display trở thành thiết bị độc lập thật, nên tạo
branch-scoped display token với quyền chỉ đọc lane display.

---

## 9. Idempotency và chống event trùng

Camera có thể đọc cùng một biển số nhiều frame liên tiếp. Backend cần tránh:

- Check-in hai lần.
- Publish nhiều event `assigned` giống nhau.
- Mở barie nhiều lần.

Yêu cầu:

- `EventId` duy nhất.
- Có deduplication window theo:

  ```text
  branchId + normalizedLicensePlate + type
  ```

- Window gợi ý: 3–5 giây cho `reading`, 10–15 giây cho `assigned`.
- Nếu booking đã `CheckedIn` và cùng lane, API có thể trả kết quả idempotent
  thay vì exception chung.
- Frontend vẫn dedupe theo `eventId`, nhưng backend phải bảo vệ nghiệp vụ.

---

## 10. Scale-out và restart

Hiện state được lưu bằng `ConcurrentDictionary` trong memory.

Điều này chấp nhận được cho demo một instance, nhưng cần ghi nhận:

- Restart app làm mất event tạm thời.
- Nhiều backend instance sẽ có state khác nhau.
- SignalR group/event giữa nhiều instance cần Redis backplane hoặc Azure
  SignalR.
- Latest state nên lấy database/cache phân tán làm source of truth.

Đối với demo:

- Một backend instance.
- REST `latest` reconstruct từ database.
- Event `waiting/error/payment` là event ngắn hạn; sau restart có thể trở về
  idle.

---

## 11. Acceptance criteria

Backend được xem là hoàn thành khi đạt tất cả điều kiện sau:

### Kết nối

- Staff có token hợp lệ kết nối được `/hubs/lane-display`.
- Không có token nhận `401`.
- Staff branch 1 chỉ join group branch 1.
- Staff branch 1 không nhận event branch 2.
- Reconnect dùng token mới vẫn hoạt động.

### Camera check-in

- Quét biển số hợp lệ, có làn:
  nhận `reading`, sau đó `assigned`.
- Event `assigned` có đúng plate, booking, lane ID và lane name.
- Không có làn:
  nhận `reading`, sau đó `waiting`.
- `waiting` có `laneId = null`, barie không được mở.
- Chưa thanh toán:
  nhận `payment`.
- Walk-in cần Staff:
  nhận `assistance`.
- Lỗi không xác định:
  nhận `error` với message an toàn.

### Queue

- Khi làn được giải phóng, xe đứng đầu hàng đợi được gán atomically.
- Xe vừa được gán nhận event `assigned`.
- Hai request check-in đồng thời không được nhận cùng một làn.

### Hoàn thành/checkout

- Staff hoàn thành thủ công phát `cleared`.
- Camera checkout phát `cleared`.
- Nếu có xe chờ, thứ tự event là `cleared` rồi `assigned` cho xe tiếp theo.
- Nếu không có xe chờ, REST latest không còn trả assigned event cũ.

### REST latest

- Sau refresh frontend lấy lại được `latestEvent`.
- Event hết `DisplayUntil` không được coi là active.
- User truy cập sai branch nhận `403`.

### Walk-in/payment

- Walk-in thanh toán ngay và có làn phát `assigned`.
- Walk-in chưa thanh toán phát `payment`.
- Webhook thanh toán xong:
  - có làn -> `assigned`;
  - hết làn -> `waiting`.

---

## 12. Test tích hợp đề xuất

Nên có integration test sử dụng `WebApplicationFactory` và SignalR client:

1. Login Staff branch A.
2. Kết nối hub bằng `accessTokenFactory`.
3. Gọi camera check-in.
4. Chờ `ReceiveLaneUpdate`.
5. Assert payload và group isolation.

Các test tối thiểu:

```text
CameraCheckIn_FreeLane_PublishesAssigned
CameraCheckIn_NoLane_PublishesWaiting
CameraCheckIn_Unpaid_PublishesPayment
CameraCheckIn_UnknownPlate_PublishesAssistance
ManualAssignment_PublishesAssigned
PaymentWebhook_FreeLane_PublishesAssigned
PaymentWebhook_NoLane_PublishesWaiting
Completion_PublishesCleared
Completion_WithQueue_PublishesAssignedForNextVehicle
LatestState_ReturnsLatestNonExpiredEvent
LatestState_OtherBranch_ReturnsForbidden
Hub_WithoutToken_ReturnsUnauthorized
Hub_DoesNotLeakEventsAcrossBranches
ConcurrentCheckIn_DoesNotAssignSameLane
DuplicateCameraFrames_DoNotDuplicateCheckIn
```

---

## 13. Thứ tự triển khai khuyến nghị

### P0 — bắt buộc để frontend dùng được

1. Chuẩn hóa `LaneDisplayEventDTO`, cho lane nullable.
2. Chuẩn hóa event type chữ thường.
3. Publish `assigned/waiting` trong đường camera check-in.
4. Sửa `OperationStaffService` từ `Reading` thành `assigned`.
5. Publish trong walk-in và webhook thanh toán.
6. Publish `cleared` trong mọi đường completion/checkout.
7. Trả `latestEvent` branch-wide.
8. Kiểm tra phân quyền branch cho endpoint latest.

### P1 — an toàn cho triển khai thực tế

1. Bảo vệ camera endpoint bằng Staff/Manager JWT hoặc device credential.
2. Centralize lane assignment/release vào coordinator.
3. Deduplicate camera events.
4. Logging có cấu trúc.
5. Integration test SignalR.

### P2 — khi scale production

1. Transactional outbox.
2. Redis/Azure SignalR backplane.
3. Distributed cache cho latest state.
4. Display-only device identity/token.

---

## 14. Không yêu cầu thay đổi nghiệp vụ hiện tại

Các điểm cần giữ nguyên:

- Staff vẫn có nút hoàn thành thủ công khi camera cổng ra không nhận diện được.
- `Completed` vẫn đồng thời kết thúc lượt rửa và giải phóng làn như hệ thống
  hiện tại.
- Camera cổng ra và barie cổng ra vẫn hoạt động theo luồng checkout hiện tại.
- Màn hình cổng vào chỉ hiển thị làn, không hiển thị hướng rẽ.
- Khi hết làn, xe chờ trước barie.
- Frontend không cần voice/speech.

