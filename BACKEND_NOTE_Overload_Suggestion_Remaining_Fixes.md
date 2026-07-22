# Backend note: Các lỗi còn lại của Overload Suggestion + FCM

**Gửi:** SmartWash Backend Team  
**Ngày kiểm tra:** 2026-07-22  
**Backend branch:** `feature/smart-booking-suggestions-and-vip-lane-operations`  
**Commit đã kiểm tra:** `d0c4cce` — `Refactor FCM tokens, booking status, and payment handling`  
**Mức độ:** P0 — chưa thể nghiệm thu end-to-end với Customer Mobile App

---

## 1. Kết luận ngắn

Commit `d0c4cce` đã sửa đúng nhiều phần quan trọng:

- Route FCM token đã chuyển về `/api/v1/notifications/token`.
- Response token đã theo envelope chung.
- FCM token được tìm global và reassign sang user hiện tại.
- Có unique index theo token.
- Payload FCM đã chuyển sang data fields camelCase.
- Token FCM invalid/unregistered được xoá.
- `Cancel` đã có transaction, hoàn tiền vào Wallet, hoàn điểm và khôi phục voucher.
- `Switch` đã có transaction Serializable và kiểm tra lại destination capacity.
- `OverloadSuggestion` đã có `ExpiresAt`.

Tuy nhiên, luồng vẫn còn các lỗi P0 sau:

1. Lần tạo suggestion thứ hai cho cùng booking sẽ vi phạm unique index `BookingId`.
2. API GET vẫn trả suggestion đã hết hạn.
3. Transaction decision bắt đầu sau khi booking/suggestion đã được đọc, nên hai request đồng thời vẫn có thể cùng xử lý.
4. Switch tạo voucher nhưng response trả `voucher: null`.
5. API docs và DTO dùng tên field khác nhau.
6. Chính sách voucher trong docs không khớp code thực tế.
7. Cancel không trả chi tiết refund cho Frontend.
8. Migration unique FCM token có thể fail khi database đã có token trùng.
9. Chưa có automated tests cho concurrency/refund/idempotency.

`dotnet build AutoWashPro.sln --no-restore` hiện thành công với **0 errors, 114 warnings**. Build thành công chưa chứng minh các transaction/concurrency case hoạt động đúng.

---

## 2. P0.1 — Data model suggestion đang tự mâu thuẫn

### Hiện trạng

Migration `20260722133638_RefactorFCMAndOverload.cs` tạo:

```text
IX_OverloadSuggestions_BookingId UNIQUE (BookingId)
```

Nhưng `OverloadSuggestionService.CheckAndTriggerOverloadAsync` lại:

1. Tìm các suggestion cũ chưa xử lý.
2. Đánh dấu chúng `IsProcessed = true`.
3. Insert một `OverloadSuggestion` mới cùng `BookingId`.

Unique index chỉ kiểm tra `BookingId`, không quan tâm `IsProcessed`. Vì row cũ vẫn tồn tại, lần insert thứ hai chắc chắn vi phạm constraint.

### Cách tái hiện

1. Booking #123 được phát hiện quá tải lần đầu → insert suggestion thành công.
2. Trigger overload chạy lại cho booking #123.
3. Service set suggestion cũ `IsProcessed = true`.
4. Service insert suggestion mới với `BookingId = 123`.
5. Database báo duplicate key tại `IX_OverloadSuggestions_BookingId`.

### Cách sửa khuyến nghị

Nếu cần giữ lịch sử suggestion, chuyển quan hệ sang one-to-many:

- `Booking` có collection `OverloadSuggestions` thay cho một navigation đơn.
- `OverloadSuggestion.BookingId` dùng index thường, không unique.
- Migration mới:
  - Drop `IX_OverloadSuggestions_BookingId` unique.
  - Create index thường theo `BookingId`.
  - Nên thêm composite index `(BookingId, IsProcessed, ExpiresAt)`.

Nếu nghiệp vụ chỉ cho phép đúng một row suốt đời booking, không insert row mới; update lại row cũ. Tuy nhiên phương án này mất lịch sử/audit, nên không khuyến nghị.

### Quy tắc tạo suggestion mới

Trước khi insert:

1. Tìm suggestion `!IsProcessed && ExpiresAt > UtcNow`.
2. Nếu có, reuse suggestion hiện tại và **không gửi push lặp**.
3. Nếu chỉ có suggestion hết hạn, mark processed/expired rồi tạo row mới.
4. Toàn bộ check + insert cần nằm trong transaction hoặc được bảo vệ bằng constraint/idempotency key.

Không nên mỗi lần check-in lại vô hiệu hoá proposal còn hạn rồi tạo proposal khác ngay lập tức.

---

## 3. P0.2 — API GET phải loại suggestion hết hạn

### Hiện trạng

`GetPendingOverloadSuggestionAsync` hiện chỉ lọc:

```text
BookingId đúng
Booking.UserId đúng
IsProcessed == false
```

API chưa lọc `ExpiresAt`, trạng thái booking hoặc giờ hẹn. Trong khi `FE_API_Docs.md` nói suggestion hết hạn sẽ trả rỗng.

### Điều kiện query bắt buộc

Chỉ trả suggestion khi:

```text
s.BookingId == bookingId
s.Booking.UserId == currentUserId
!s.IsProcessed
s.ExpiresAt > UtcNow
s.Booking.Status == "Pending"
s.Booking.ScheduledTime > UtcNow
```

Nếu không có suggestion hợp lệ:

```json
{
  "statusCode": 200,
  "message": "Success",
  "data": null,
  "details": null
}
```

Không trả suggestion hết hạn rồi để POST mới báo lỗi. App cần GET là nguồn authoritative để quyết định có mở modal hay không.

### DTO nên bổ sung

Response nên trả thêm:

```json
{
  "expiresAt": "2026-07-22T15:30:00Z"
}
```

FE có thể đóng modal đúng lúc và không cho submit sau TTL.

---

## 4. P0.3 — Transaction decision đang bắt đầu quá muộn

### Hiện trạng

Trong `HandleOverloadDecisionAsync`:

1. Booking được query ngoài transaction.
2. Suggestion được query ngoài transaction.
3. Kiểm tra `Status`, `IsProcessed`, `ExpiresAt` ngoài transaction.
4. Sau đó nhánh `Switch`/`Cancel` mới bắt đầu transaction.

Hai request đồng thời có thể cùng đọc:

```text
booking.Status == Pending
suggestion.IsProcessed == false
```

Sau đó cả hai cùng đi vào xử lý. UI disable double tap không giải quyết được retry mạng hoặc hai thiết bị cùng account.

### Cách sửa bắt buộc

Transaction phải bắt đầu **trước khi query booking và suggestion**:

1. Begin transaction.
2. Query booking bên trong transaction.
3. Query suggestion bên trong transaction.
4. Revalidate ownership, booking status, processed, expiry.
5. Claim suggestion atomically.
6. Thực hiện Keep/Cancel/Switch.
7. SaveChanges một lần nếu có thể.
8. Commit.

### Claim atomically

Khuyến nghị một trong hai cách:

#### Cách A — RowVersion

- Thêm `RowVersion`/concurrency token vào `OverloadSuggestion`.
- Update `IsProcessed` với optimistic concurrency.
- Request thua concurrency trả `409 Conflict`.

#### Cách B — Conditional update

Trong transaction:

```sql
UPDATE OverloadSuggestions
SET IsProcessed = 1, ProcessedAt = UTC_TIMESTAMP(), Decision = @decision
WHERE Id = @id
  AND IsProcessed = 0
  AND ExpiresAt > UTC_TIMESTAMP();
```

Chỉ tiếp tục nếu affected rows bằng 1. Nếu bằng 0, proposal đã hết hạn hoặc đã được thiết bị/request khác xử lý.

### Idempotency mong muốn

- Cùng decision được gửi lại sau timeout: không đổi capacity/refund/voucher lần hai.
- Decision khác sau khi đã xử lý: trả `409`.
- Hai request Switch đồng thời: chỉ một request cấp voucher.
- Cancel và Switch đồng thời: chỉ một decision thắng.

Nên lưu thêm:

```text
Decision: Keep | Switch | Cancel
ProcessedAt
```

`IsProcessed` đơn lẻ không đủ để audit hoặc trả idempotent response.

---

## 5. P0.4 — Switch tạo voucher nhưng không trả voucher

### Hiện trạng

`HandleOverloadDecisionResponseDTO` có:

```text
VoucherResponseDTO? Voucher
```

Nhánh Switch tạo `Voucher` và `UserVoucher`, nhưng sau commit chỉ gán:

```text
response.Message
response.UpdatedBooking
```

`response.Voucher` không được gán, nên Mobile nhận:

```json
{
  "voucher": null
}
```

### Response Switch yêu cầu

```json
{
  "statusCode": 200,
  "message": "Overload suggestion handled successfully.",
  "data": {
    "success": true,
    "message": "Switched to new branch successfully. You received a compensation voucher.",
    "decision": "Switch",
    "updatedBooking": {
      "bookingId": 123,
      "scheduledTime": "2026-07-22T08:00:00Z",
      "status": "Pending",
      "finalAmount": 200000
    },
    "voucher": {
      "voucherId": 456,
      "code": "OVL-ABC123",
      "discountAmount": 20000,
      "expiryDate": "2026-08-22T08:00:00Z",
      "isActive": true
    },
    "refund": null
  },
  "details": null
}
```

Biến voucher cần tồn tại ngoài scope transaction hoặc DTO voucher được dựng trước khi ra khỏi transaction, sau đó gán vào response khi commit thành công.

---

## 6. P0.5 — Chốt lại chính sách voucher Switch

Hiện code và tài liệu mâu thuẫn:

- Code: tạo voucher mới cho `UserVoucher`, không giảm `FinalAmount` booking hiện tại.
- `FE_API_Docs.md`: nói voucher 10% được trừ thẳng vào `FinalAmount` booking hiện tại.
- Response message: nói khách “received a 10% voucher”.

### Chính sách đề xuất để khớp walkthrough và FE plan

Khi Switch:

- Booking chỉ đổi branch/time, giá booking hiện tại giữ nguyên.
- Backend cấp voucher đền bù dùng cho lần sau.
- Voucher có số tiền bằng 10% `OriginalPrice` của booking hiện tại.
- Response trả voucher vừa cấp.
- Mobile hiển thị: `Bạn nhận voucher đền bù trị giá X dùng cho lần đặt tiếp theo.`

Nếu Product muốn giảm trực tiếp booking hiện tại, backend phải bỏ việc tạo voucher dùng sau hoặc định nghĩa rõ khách nhận cả hai. Không được vừa nói “voucher dùng sau” vừa sửa `FinalAmount` mà không có quy tắc kế toán/refund.

Sau khi chốt, sửa `FE_API_Docs.md` và walkthrough cho cùng một nghiệp vụ.

---

## 7. P0.6 — Chuẩn hoá request decision

### Mâu thuẫn hiện tại

DTO backend dùng:

```text
SuggestedBranchId
SuggestedSlotId
SuggestedTime
```

Nhưng `FE_API_Docs.md` hướng dẫn:

```json
{
  "alternativeBranchId": 2
}
```

Nếu FE gửi `alternativeBranchId`, `SuggestedBranchId` sẽ không có giá trị đúng.

### Contract khuyến nghị

An toàn nhất là client chỉ gửi decision:

```json
{
  "decision": "Switch"
}
```

Backend đã có suggestion trong DB nên phải tự lấy:

- `SuggestedBranchId`
- `SuggestedSlotId`
- `SuggestedTime`

Không cần client gửi lại dữ liệu server vừa phát. Cách này tránh:

- Client sửa branch/slot.
- Sai field name.
- Sai timezone/format.
- So sánh `DateTime` chính xác gây reject không cần thiết.

Nếu backend vẫn muốn client echo dữ liệu, contract phải thống nhất:

```json
{
  "decision": "Switch",
  "suggestedBranchId": 2,
  "suggestedSlotId": 10,
  "suggestedTime": "2026-07-22T08:00:00Z"
}
```

Và backend phải validate đủ cả branch, slot, time. Hiện tại backend chưa validate `SuggestedSlotId` request.

Giá trị `Decision` nên được validate bằng enum hoặc allow-list, không nhận string tuỳ ý/casing tuỳ ý.

---

## 8. P0.7 — Response Cancel cần chi tiết refund

### Phần đã làm đúng

Cancel hiện đã:

- Giảm capacity slot cũ.
- Đổi booking sang `Cancelled`.
- Hoàn `FinalAmount` vào Wallet nếu có payment completed.
- Hoàn điểm đã dùng.
- Trả lại voucher đã dùng.
- Thực hiện trong transaction.

### Phần còn thiếu

Response chỉ trả `UpdatedBooking`; FE không biết:

- Có thực sự hoàn tiền hay không.
- Hoàn bao nhiêu.
- Hoàn về Wallet hay PayOS/original method.
- Hoàn bao nhiêu điểm.
- Voucher nào được khôi phục.

### Response Cancel yêu cầu

```json
{
  "statusCode": 200,
  "message": "Overload suggestion handled successfully.",
  "data": {
    "success": true,
    "decision": "Cancel",
    "message": "Booking cancelled and compensation processed.",
    "updatedBooking": {
      "bookingId": 123,
      "status": "Cancelled",
      "finalAmount": 200000
    },
    "voucher": null,
    "refund": {
      "refundedAmount": 200000,
      "refundDestination": "Wallet",
      "refundedPoints": 100,
      "restoredVoucherId": 88
    }
  },
  "details": null
}
```

Code hiện đang cộng tiền vào Wallet kể cả booking thanh toán PayOS. Nếu đây là chính sách chính thức, response/docs phải ghi rõ `refundDestination = Wallet`; không nói hoàn về PayOS.

Nếu booking chưa thanh toán, trả `refundedAmount = 0`, không để FE suy đoán từ message.

---

## 9. P0.8 — Migration unique FCM token cần xử lý dữ liệu trùng cũ

Migration mới chỉ chạy:

```text
CREATE UNIQUE INDEX IX_UserFcmTokens_Token ON UserFcmTokens(Token)
```

Nếu production/staging đã có cùng token ở nhiều row từ code cũ, migration sẽ fail.

### Cần làm trước khi tạo index

- Tìm token trùng.
- Giữ row mới nhất theo `LastUsedAt`/`CreatedAt`.
- Xoá các row duplicate cũ.
- Sau đó mới tạo unique index.

Nếu migration chưa deploy, sửa migration hiện tại. Nếu đã deploy ở một số môi trường, tạo corrective migration an toàn và kiểm tra trạng thái từng database.

Ngoài ra, các `OverloadSuggestion` cũ được gán `ExpiresAt = DateTime.MinValue`. Nên đánh dấu chúng processed/expired trong data migration để GET không trả dữ liệu cũ.

---

## 10. P1 — Trigger push cần throttle và idempotency

`OverloadNotifiedAt` đã được ghi nhưng chưa được dùng để lọc/throttle.

Khi trigger chạy lại:

- Không tạo suggestion mới nếu đang có suggestion còn hạn.
- Không gửi lại push cho cùng suggestion.
- Có thể resend sau một khoảng rõ ràng nếu Product yêu cầu, nhưng phải dùng cùng suggestion id và giới hạn số lần.

Nên bổ sung `suggestionId` vào payload FCM và response GET. Mobile vẫn fetch authoritative data theo booking/suggestion id.

Payload đề xuất:

```json
{
  "type": "OVERLOAD_SUGGESTION",
  "suggestionId": "99",
  "bookingId": "123",
  "suggestedBranchId": "2",
  "suggestedBranchName": "SmartWash Quận 7",
  "suggestedSlotId": "45",
  "suggestedTime": "2026-07-22T08:00:00Z",
  "expiresAt": "2026-07-22T07:55:00Z"
}
```

---

## 11. P1 — Error contract

Các conflict nghiệp vụ không nên trả chung `400`:

| HTTP | Trường hợp |
|---:|---|
| 400 | Decision/payload không hợp lệ |
| 401 | Token hết hạn/không hợp lệ |
| 403 | Booking không thuộc user |
| 404 | Không tìm thấy booking/suggestion |
| 409 | Suggestion đã xử lý hoặc hết hạn |
| 409 | Destination slot vừa hết chỗ |
| 409 | Booking không còn Pending |

Envelope thống nhất:

```json
{
  "statusCode": 409,
  "message": "The overload suggestion has expired.",
  "data": null,
  "details": null
}
```

Mobile sẽ dựa vào status để đóng stale modal hoặc cho retry network, không nên parse text message để đoán loại lỗi.

---

## 12. P1 — FCM platform và warning

Backend hiện gửi trực tiếp Firebase Admin tới FCM registration token:

- Android: phù hợp với native FCM token.
- iOS: cần chốt Firebase Messaging native/APNs/Expo Push Service; không mặc định xem APNs token là FCM token.

Nếu release đầu chỉ hỗ trợ Android push, cần ghi rõ. iOS Mobile sẽ dùng fallback GET khi mở app.

Build hiện cảnh báo `Message.Token` obsolete trong FirebaseAdmin `3.6.0`. Cần kiểm tra migration API được package yêu cầu, nhưng đây chưa phải blocker bằng các lỗi transaction/data ở trên.

---

## 13. Automated tests bắt buộc

Hiện repository chưa có test project. Trước khi bàn giao FE, cần ít nhất một integration test project với database test/container.

### Suggestion creation

- Tạo suggestion lần đầu thành công.
- Trigger lần hai khi suggestion còn hạn không insert/push lặp.
- Sau khi suggestion hết hạn có thể tạo suggestion mới cùng booking.
- Không vi phạm unique/index constraint.
- Không tạo suggestion cho booking không Pending/đã qua giờ.

### GET pending

- User chỉ đọc được suggestion của chính mình.
- Pending + còn hạn trả data.
- Hết hạn trả `data: null`.
- Processed trả `data: null`.
- Booking Cancelled/Completed trả `data: null`.

### Decision concurrency

- Hai request Switch đồng thời: chỉ một thành công, một voucher, capacity thay đổi một lần.
- Switch và Cancel đồng thời: chỉ một decision thắng.
- Hai request Cancel đồng thời: chỉ một refund.
- Retry request sau timeout không refund/cấp voucher lần hai.

### Switch

- Revalidate destination slot capacity.
- Slot full trả `409`, không thay booking/capacity/voucher.
- Thành công đổi đúng branch/slot/time.
- Capacity nguồn giảm và đích tăng đúng một lần.
- Response trả voucher đầy đủ.

### Cancel

- Booking Wallet paid được hoàn đúng amount.
- Booking PayOS paid được cộng đúng amount vào Wallet nếu đó là policy.
- Booking unpaid không tạo refund transaction.
- Điểm/voucher được hoàn đúng một lần.
- Response refund khớp dữ liệu database.

### FCM token

- Register cùng token cho cùng user không tạo duplicate.
- Register cùng token cho user khác reassign đúng.
- Migration chạy được khi có duplicate token cũ.
- Remove token chỉ xoá token thuộc current user.

---

## 14. API contract cuối cùng đề xuất

### Register token

```http
POST /api/v1/notifications/token
```

```json
{
  "token": "native-fcm-token"
}
```

### Remove token

```http
DELETE /api/v1/notifications/token
```

```json
{
  "token": "native-fcm-token"
}
```

### Get pending suggestion

```http
GET /api/v1/bookings/{bookingId}/overload-suggestion
```

```json
{
  "statusCode": 200,
  "message": "Success",
  "data": {
    "suggestionId": 99,
    "bookingId": 123,
    "suggestedBranchId": 2,
    "suggestedBranchName": "SmartWash Quận 7",
    "suggestedSlotId": 45,
    "suggestedTime": "2026-07-22T08:00:00Z",
    "expiresAt": "2026-07-22T07:55:00Z"
  },
  "details": null
}
```

### Handle decision

Preferred request:

```http
POST /api/v1/bookings/{bookingId}/handle-overload-suggestion
```

```json
{
  "decision": "Switch"
}
```

Backend lấy branch/slot/time từ suggestion trong database.

Response phải có:

- `decision`
- `updatedBooking`
- `voucher` khi Switch
- `refund` khi Cancel

---

## 15. Thứ tự sửa khuyến nghị

1. Sửa data model/index `OverloadSuggestion` và migration.
2. Đưa toàn bộ decision query + claim vào transaction.
3. Sửa GET expiry/status filtering.
4. Sửa trigger reuse/throttle suggestion còn hạn.
5. Trả voucher trong response Switch.
6. Thêm refund details trong response Cancel.
7. Chốt request chỉ gửi `decision` hoặc đồng bộ tên field.
8. Đồng bộ `FE_API_Docs.md` với code và chính sách voucher.
9. Làm data-safe migration cho duplicate FCM tokens.
10. Thêm automated integration/concurrency tests.

---

## 16. Definition of Done

Backend chỉ được xem là sẵn sàng cho Mobile khi:

- Tạo lại suggestion cho cùng booking không lỗi constraint.
- GET không trả suggestion expired/processed/booking invalid.
- Hai request đồng thời chỉ có một decision thắng.
- Capacity, refund và voucher không bao giờ chạy hai lần.
- Switch response có voucher đầy đủ.
- Cancel response có refund details đầy đủ.
- DTO, Swagger và `FE_API_Docs.md` dùng cùng field/cùng nghiệp vụ.
- Migration chạy được trên database đã có dữ liệu/token trùng.
- Automated tests concurrency/refund/idempotency đều pass.
- Swagger staging đã có contract cuối và Mobile test được không cần mock.

