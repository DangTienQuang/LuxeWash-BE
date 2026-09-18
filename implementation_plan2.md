# Đặc tả triển khai kỹ thuật: Sự cố buồng rửa và xử lý lịch đặt

> Tài liệu này là đặc tả để triển khai trong ba repository `SmartWash-BE`, `LuxeWash_WEB_FE`, `LuxeWash_Mobile_FE`. Các mục 1–9 là tóm tắt sản phẩm; các mục 10 trở đi là hợp đồng kỹ thuật và tiêu chí hoàn thành. Trước khi sửa code, đối chiếu lại tên lớp/API với HEAD hiện tại; không xem các đường dẫn trong tài liệu là bằng chứng code chưa thay đổi.

> **Cách giao cho AI:** Khi người dùng yêu cầu *triển khai* tài liệu này, hãy thực hiện code và test theo mục 21, không chỉ trả lại một plan khác. Tài liệu tự nó không phải quyền chạy migration/deploy trên production. Nếu code thực tế khác đặc tả, đọc và đánh giá ảnh hưởng trước; ghi rõ điểm lệch, chọn giải pháp giữ invariant mục 22 và báo lại trong kết quả.

## 1. Mục tiêu

Manager báo cáo một hoặc nhiều buồng rửa hỏng, hoặc sự cố toàn chi nhánh như mất điện/nước, cùng thời gian dự kiến khắc phục (ETA). Hệ thống giảm công suất nhận lịch trong đúng khoảng thời gian bị ảnh hưởng; nếu không còn buồng phù hợp thì ngừng nhận lịch. Với lịch đã đặt nhưng không thể phục vụ, khách chọn **hủy** hoặc **chuyển sang chi nhánh khác còn chỗ trong cùng ngày/khung giờ**. Cả hai lựa chọn, sau khi xử lý thành công, đều được cấp voucher giảm **20% cho lần rửa tiếp theo**, dùng một lần và hết hạn sau **6 tháng theo lịch**. Manager có thể gia hạn ETA hoặc xác nhận khắc phục sớm.

Phạm vi gồm backend, web Manager và mobile khách hàng. Không thay đổi cấu hình khung giờ lặp lại hằng ngày để xử lý một sự cố tạm thời.

## 2. Hiện trạng và giới hạn của code hiện có

- `TimeSlot.MaxCapacity` là công suất cấu hình; `DailySlotCapacity` hiện lưu `BookedWeight`, **không có trường `MaxWeight`**. Các luồng xem chỗ trống, tạo lịch và chuyển lịch đang so với `MaxCapacity` cố định.
- `Lane` có `IsBusinessLane` và `IsVipLane`. Chỉ lưu số buồng hỏng sẽ không đủ để xác định loại lịch nào còn phục vụ được.
- `OverloadSuggestion` đã có thông báo và chuyển chi nhánh nhưng chỉ quét khoảng thời gian ngắn, chỉ xử lý lịch `Pending`, có hạn gợi ý ngắn và chính sách voucher khác 20%/6 tháng. Tái sử dụng hạ tầng cần thiết, **không dùng nguyên luồng quá tải làm luồng sự cố**.
- Backend có thông báo trong app và push qua FCM. Mobile hiện đăng ký remote push cho Android development/release build; cần bổ sung và kiểm thử iOS nếu yêu cầu thông báo đẩy trên iPhone. Khách vẫn phải thấy yêu cầu xử lý khi mở app dù push thất bại.
- `ForceCancelBookingsAsync` hiện hủy hàng loạt và không có bước khách quyết định; không gọi trực tiếp thao tác này cho luồng mới.

## 3. Quy tắc nghiệp vụ

1. Sự cố ghi nhận **ID các buồng bị ảnh hưởng**, không chỉ số lượng. Với mất điện/nước toàn chi nhánh, đánh dấu loại sự cố toàn chi nhánh và coi công suất phù hợp bằng 0.
2. Khung giờ bị ảnh hưởng nếu khoảng `[slotStart, slotEnd)` giao với `[incidentStart, estimatedEnd)`. Tính cho mọi ngày trong khoảng sự cố, kể cả ngày kế tiếp. Khi chỉ giao một phần khung giờ, bản đầu lấy mức công suất an toàn thấp nhất cho khung giờ đó.
3. `effectiveCapacity` của từng ngày/khung giờ được tính từ các buồng còn hoạt động, có xét trọng số/dịch vụ; không vượt `TimeSlot.MaxCapacity`. Sau kiểm tra công suất tổng, còn phải kiểm tra khả năng xếp lịch lên **buồng phù hợp** cho loại booking. Số chỗ còn lại = `effectiveCapacity - BookedWeight - số chỗ đang giữ để chuyển lịch`. Không mặc định hỏng một buồng luôn giảm đúng một đơn vị công suất. Công thức và dữ liệu cấu hình cụ thể nằm ở mục 12.
4. Nếu lịch đã đặt vượt công suất mới, chỉ số lịch **vượt** cần khách quyết định; không gửi cảnh báo cho các lịch còn phục vụ được. Mặc định giữ lịch đặt trước; Manager xem danh sách và xử lý ngoại lệ trước khi gửi. Lịch đã check-in/đang rửa được nhân viên xử lý trực tiếp, không tự đưa vào lựa chọn trên app.
5. Chuyển đến chi nhánh đang hoạt động, có buồng/dịch vụ/loại xe phù hợp và còn chỗ trong cùng ngày/khung giờ tương đương. Không giả định hai chi nhánh dùng cùng `slotId`. Backend kiểm tra lại công suất khi khách xác nhận; nếu nơi mới vừa hết chỗ thì trả lựa chọn mới.
6. Hủy do sự cố không phạt khách; hoàn tiền đã thanh toán, điểm đã dùng và khôi phục voucher cũ. Chuyển chi nhánh giữ nguyên giá khách đã chấp nhận, không tự thu thêm.
7. Voucher đền bù được cấp **một lần cho mỗi case gián đoạn của booking** sau khi quyết định hủy/chuyển thành công; nếu nhiều incident chồng nhau cùng gây gián đoạn một booking, không tạo nhiều voucher chỉ vì số incident. Voucher không áp dụng cho chính booking vừa chuyển; `DiscountPercent = 20`, hạn `issuedAt.AddMonths(6)`, dùng một lần. Theo đúng yêu cầu 20%, bản đầu **không áp trần giảm giá**; xem mục 16 để tránh bị validation voucher hiện tại chặn.
8. Gia hạn chỉ tính thêm phần thời gian phát sinh và các lịch mới bị ảnh hưởng; không làm lại quyết định đã hoàn tất. Khắc phục sớm mở lại công suất tương lai, không âm thầm đảo ngược quyết định hủy/chuyển thành công.

## 4. Backend

### 4.1. Dữ liệu và migration

Tạo `BranchIncident` với `Id`, `BranchId`, `Type` (buồng hỏng/điện/nước/khác), `Reason`, `StartedAt`, `EstimatedEndAt`, `ActualEndAt`, `Status`, `CreatedBy`, `CreatedAt`, `UpdatedAt` và phiên bản chống ghi đè đồng thời. Tạo quan hệ `IncidentLane` chứa các `LaneId` bị ảnh hưởng và bảng lịch sử thay đổi ETA/trạng thái để audit.

Tạo `IncidentAffectedBooking` liên kết incident và booking, có trạng thái `AwaitingCustomer`, `Transferred`, `Cancelled`, `Kept`, `NeedsManualHandling`, hạn phản hồi, lựa chọn/chi nhánh đích và thời điểm quyết định. Đặt unique constraint cho cặp `incidentId + bookingId` và cơ chế DB ngăn hai case actionable cùng lúc cho một booking; lưu khóa cấp voucher/hoàn tiền idempotent. Không dùng `Booking.Status` để biểu diễn việc *đang chờ khách trả lời*. Hết hạn phản hồi **không** tự biến booking thành một trạng thái terminal thiếu quy tắc hoàn tiền.

### 4.2. Bộ tính công suất dùng chung

Tạo `IncidentCapacityService` trả công suất hiệu lực và lý do đóng/giảm chỗ cho branch/date/slot/loại booking. Bản ghi sự cố là nguồn sự thật; không ghi giảm vĩnh viễn `TimeSlot.MaxCapacity`, cũng không dựa duy nhất vào `Lane.IsActive` để mô tả khoảng bảo trì.

Tích hợp vào **mọi đường nhận chỗ**: xem slot cá nhân/doanh nghiệp, kiểm tra tương thích, tạo lịch cá nhân/doanh nghiệp, walk-in, đổi lịch và chuyển chi nhánh. Kiểm tra ở màn hình chỉ phục vụ hiển thị; trước khi ghi booking vẫn kiểm tra lại trong transaction/concurrency control để tránh đặt vượt chỗ.

### 4.3. Đánh giá ảnh hưởng và thông báo

Khi tạo/gia hạn sự cố: tính lại từng slot bị ảnh hưởng, đối chiếu `BookedWeight` với công suất mới, tạo danh sách lịch cần xử lý và phương án chi nhánh đích. Manager xem bản xem trước trước khi xác nhận. Sau khi lưu thành công, ghi sự kiện vào notification outbox/background job để phát **push + thông báo trong app**, có retry khi gửi thất bại. Push chỉ chứa `incidentId`, `bookingId` hoặc đường dẫn; danh sách chi nhánh còn chỗ phải tải mới từ API vì sức chứa có thể thay đổi.

Lịch có thời gian quá gần để khách tự xử lý đi vào danh sách Manager/nhân viên cần liên hệ trực tiếp. Lịch doanh nghiệp cũng phải được tính vào công suất; quyền quyết định lịch doanh nghiệp cần chốt riêng trước khi mở luồng tự phục vụ.

### 4.4. API dự kiến

Tên route là hợp đồng đề xuất, cần thống nhất với convention hiện tại trước khi code:

| Đối tượng | API | Mục đích |
| --- | --- | --- |
| Manager | `POST /api/v1/manager/incidents/preview` | Xem công suất và lịch bị ảnh hưởng trước khi tạo. |
| Manager | `POST /api/v1/manager/incidents` | Tạo sự cố và phát yêu cầu xử lý. |
| Manager | `GET /api/v1/manager/incidents`, `GET /api/v1/manager/incidents/{id}/impact` | Theo dõi sự cố và phản hồi của khách. |
| Manager | `POST /api/v1/manager/incidents/{id}/extend-preview`, `POST /api/v1/manager/incidents/{id}/extend` | Xem trước và gia hạn ETA. |
| Manager | `POST /api/v1/manager/incidents/{id}/resolve` | Ghi nhận khắc phục, mở lại công suất. |
| Manager | `POST /api/v1/manager/incidents/{id}/cases/{caseId}/manual-decision` | Xử lý case cần liên hệ trực tiếp, có audit/ghi nhận đồng ý của khách. |
| Khách | `GET /api/v1/bookings/{id}/incident-options` | Lấy trạng thái, hạn phản hồi và chi nhánh còn chỗ. |
| Khách | `POST /api/v1/bookings/{id}/incident-decision` | Gửi `Cancel`/`Transfer` (hoặc `Keep` sau khắc phục) với `caseId` và idempotency key. |

Backend xác thực Manager thuộc chi nhánh từ token/quyền truy cập, không tin `branchId` client tự khai. API của khách kiểm tra booking thuộc đúng tài khoản, yêu cầu còn hiệu lực và booking còn được phép xử lý.

### 4.5. Transaction, tiền và voucher

Quyết định `Cancel` hoặc `Transfer` cập nhật booking, sức chứa nguồn/đích, thanh toán/hoàn tiền, điểm, voucher cũ và voucher đền bù trong luồng có transaction và chống xử lý lặp. Khi chuyển, giữ chỗ đích ngắn hạn hoặc kiểm tra/ghi atomically; nếu không còn chỗ thì trả `409` để khách chọn lại. Lỗi gửi thông báo sau commit không được biến một quyết định đã thành công thành phản hồi API thất bại.

## 5. Frontend web Manager

- Thêm mục **Sự cố chi nhánh** từ màn hình buồng rửa/lịch đặt. Form chọn loại sự cố, buồng hỏng cụ thể, thời điểm bắt đầu, ETA và lý do; cảnh báo nếu toàn chi nhánh ngừng hoạt động.
- Trước khi xác nhận, hiển thị theo từng ngày/khung giờ: công suất cũ/mới, lịch đã đặt, số lịch thiếu chỗ, khách cần thông báo và ca cần liên hệ thủ công.
- Trang chi tiết hiển thị lịch sử ETA, trạng thái gửi thông báo, khách đã hủy/đã chuyển/chưa phản hồi, nút **Gia hạn** và **Đã khắc phục**. Có tải lại và thông báo lỗi; không coi gửi push thất bại là khách đã nhận thông tin.
- Màn hình đặt lịch/lịch quản lý hiển thị slot đóng hoặc giảm công suất, lý do và ETA theo dữ liệu API.

## 6. Mobile khách hàng

- Thêm loại thông báo `INCIDENT_ACTION_REQUIRED` trong hộp thông báo và push. Bấm push khi app đang mở, chạy nền hoặc khởi động lạnh đều dẫn tới màn hình quyết định; nếu chưa đăng nhập thì quay lại đúng booking sau đăng nhập.
- Màn hình quyết định hiển thị lịch gốc, lý do/ETA, hạn phản hồi, lựa chọn **Hủy và nhận hoàn tiền** hoặc **Chuyển chi nhánh và giữ khung giờ**, danh sách chi nhánh phù hợp, chính sách voucher 20%/6 tháng. Có bước xác nhận cuối.
- Luôn tải lại options từ API khi mở màn hình, không dùng danh sách chi nhánh nhúng trong push. Có trạng thái loading/empty/error/offline; giữ lựa chọn khi gửi thất bại, khóa bấm lặp trong lúc gửi, tải lại sau `409` khi đích hết chỗ.
- Sau thành công, hiển thị booking mới hoặc chi tiết hoàn tiền và voucher. Các booking còn `AwaitingCustomer` vẫn hiện trong danh sách lịch dù push không đến. Bổ sung đường push iOS nếu sản phẩm cần iPhone; kiểm thử trên development/release build, không dùng Expo Go Android để xác nhận remote push.

## 7. Gia hạn, khôi phục và không phản hồi

Gia hạn: lưu ETA cũ/mới, tính lại các slot ở phần thời gian tăng thêm và phát yêu cầu cho khách mới bị ảnh hưởng. Nếu một slot cũ thiếu chỗ hơn, quét lại số lịch vượt công suất của slot đó. Khắc phục sớm: lưu `ActualEndAt`, ngừng áp dụng công suất giảm từ thời điểm đó, mở lại chỗ tương lai và cập nhật khách còn đang chờ.

Không hủy lịch chỉ vì push không được mở: backend cần hạn phản hồi, job nhắc và danh sách Manager gọi điện. Nếu đến giờ hẹn vẫn không thể phục vụ và không có quyết định, áp chính sách auto-cancel/hoàn tiền/voucher **đã định nghĩa ở mục 10**; không để booking treo hoặc cho check-in vào buồng hỏng.

## 8. Kiểm thử và điều kiện nghiệm thu

- Hỏng 1 buồng nhưng còn đủ công suất: giảm chỗ mới, không gửi yêu cầu cho lịch vẫn phục vụ được.
- Hỏng nhiều/tất cả buồng: slot và lịch bị ảnh hưởng đúng ở ngày hiện tại/ngày kế tiếp; toàn bộ ngừng hoạt động thì không tạo được lịch mới.
- Hỏng buồng VIP/doanh nghiệp: công suất giảm đúng theo loại lịch; không chuyển đến buồng/chi nhánh không phù hợp.
- Hai khách cùng nhận slot cuối, gửi quyết định hai lần, API timeout rồi retry: không vượt công suất, không hoàn tiền hoặc cấp voucher hai lần.
- Hủy trả đúng tiền/điểm và khôi phục voucher cũ; chuyển giữ giá; voucher mới đúng 20%, dùng một lần cho lượt tiếp theo, hết hạn sau 6 tháng.
- Gia hạn nhiều lần, sửa xong sớm, push thất bại/offline, khách không phản hồi: booking, công suất và danh sách cần xử lý nhất quán.
- Test API và UI Manager/mobile trên dữ liệu gần thực tế; kiểm thử thiết bị Android/iPhone nếu yêu cầu push cả hai.

## 9. Thứ tự triển khai

1. Dùng chính sách mặc định ở mục 10 và chốt API contract; làm migration, bộ tính công suất và test mọi đường đặt lịch.
2. Làm đánh giá ảnh hưởng, quyết định hủy/chuyển, hoàn tiền/voucher, notification outbox và test đồng thời/idempotency.
3. Làm web Manager (tạo/xem trước/gia hạn/khắc phục/theo dõi), rồi mobile (thông báo/màn hình quyết định).
4. Kiểm thử tích hợp, pilot một chi nhánh và giám sát tỷ lệ thông báo thất bại, lịch chưa phản hồi, slot vượt công suất.

## 10. Chính sách mặc định để AI có thể triển khai không bị treo

Các mặc định dưới đây chỉ dùng cho bản đầu; đưa vào cấu hình server và ghi rõ trong UI để có thể đổi về sau.

1. **Khách không phản hồi:** hạn phản hồi mặc định là 30 phút trước giờ bắt đầu. Nếu sự cố được báo khi còn dưới 30 phút, đánh dấu `NeedsManualHandling` ngay và yêu cầu Manager liên hệ; không âm thầm tự hủy trước giờ hẹn. Khi đến giờ mà chi nhánh vẫn không thể phục vụ và chưa có quyết định, job hủy do hệ thống, hoàn tiền/điểm/voucher cũ và cấp cùng voucher 20% như ca khách chủ động hủy. Gửi thông báo kết quả. Không coi khách là no-show.
2. **Sửa xong sớm:** với booking còn chờ và đủ công suất ở chi nhánh gốc, thêm lựa chọn `Keep` để khách giữ lịch. Không cấp voucher 20% cho `Keep`; nếu trước đó khách đã hủy/chuyển thành công thì không đảo ngược. Nếu khách không bấm Keep nhưng chi nhánh đã đủ khả năng phục vụ khi đến hạn xử lý, hệ thống tự đóng case là `Kept`, giữ booking và báo khách; không auto-cancel một lịch đã phục vụ được. Nếu chỉ được chọn hai nút theo quyết định sản phẩm sau này, thay `Keep` bằng thông báo giữ lịch tự động và kiểm thử tương ứng.
3. **Voucher 20%:** không trần, không mức đơn tối thiểu, áp dụng mọi chi nhánh/dịch vụ/loại xe cho **booking cá nhân tiếp theo**; không áp dụng cho booking nguồn vừa được chuyển. Mỗi case gián đoạn được tối đa một voucher, kể cả retry, nhiều incident chồng nhau và tự hủy do hết hạn. Booking doanh nghiệp vẫn được tính vào công suất nhưng việc di chuyển/hoàn tiền doanh nghiệp do Manager xử lý thủ công ở bản đầu; không tự cấp voucher cá nhân cho tài khoản doanh nghiệp.
4. **Khách không có app/push token:** lưu thông báo in-app, gửi email nếu tài khoản có email, hiển thị trong hàng Manager cần gọi điện. SMS nằm ngoài phạm vi bản đầu.
5. **ETA chỉ là ước tính:** không tự mở lại buồng khi đến ETA. Nếu ETA qua mà Manager chưa `Resolve`, sự cố tiếp tục chặn công suất, báo quá hạn và yêu cầu Manager gia hạn hoặc xác nhận đã sửa xong.

## 11. Bản đồ code hiện tại và phạm vi chỉnh sửa

### Backend (`SmartWash-BE`, .NET 8 + EF Core/MySQL)

| Nơi | Thay đổi cần thực hiện |
| --- | --- |
| `DAL/Entities/BranchIncident.cs`, `IncidentLane.cs`, `IncidentAffectedBooking.cs`, `IncidentChange.cs`, `LaneSlotCapacity.cs` | Entity mới; cân nhắc bảng `IncidentNotificationDelivery` nếu cần theo dõi gửi theo kênh. |
| `DAL/Data/AutoWashDbContext.cs`, `DAL/Migrations/*` | `DbSet`, Fluent indexes/foreign keys, migration; không sửa migration cũ đã áp dụng. |
| `BLL/Services/Operations/IncidentCapacityService.cs` | Một nguồn tính công suất/kiểm tra lane cho tất cả luồng đặt lịch. |
| `BLL/Services/Operations/IncidentService.cs` | Preview, create, extend, resolve, impact/reconciliation, audit. |
| `BLL/Services/Bookings/BookingService.cs` | Tích hợp vào `GetAvailableSlotsAsync`, `ValidateBookingCompatibilityAsync`, `CreateBookingAsync`, walk-in, `RescheduleBookingAsync`, quyết định hủy/chuyển. Không reuse trực tiếp `HandleOverloadDecisionAsync` hoặc `ForceCancelBookingsAsync`. |
| `BLL/Services/Business/BusinessBookingService.cs`, `BLL/Services/Bookings/LaneSchedulerService.cs` | Kiểm tra sự cố trong xem slot, tạo/đổi lịch doanh nghiệp và mô phỏng xếp lane; loại lane hỏng khỏi lịch dự kiến. |
| `BLL/Services/Operations/OverloadSuggestionService.cs` | Khi tìm chi nhánh đích phải dùng công suất hiệu lực; tránh gửi một gợi ý quá tải mâu thuẫn với một case sự cố đang chờ. |
| `BLL/Services/Integrations/*`, `BLL/BackgroundServices/OutboxProcessorService.cs` | Loại notification mới, gửi/retry/dedup. Worker hiện **không** xử lý loại outbox tùy ý: cần thêm handler và test. |
| `API/Controllers/Manager/ManagerIncidentsController.cs`, `API/Controllers/Bookings/BookingsController.cs`, `API/Program.cs` | Route, authorization/DI/job registration. Route Manager phải kiểm tra chi nhánh của Manager; không gắn vào `StaffBookingsController` vì controller đó có ràng buộc role khác. |

### Web và mobile

| Repository | Nơi nối vào |
| --- | --- |
| `LuxeWash_WEB_FE` | `src/App.jsx`, `src/components/layout/ManagerSidebar.jsx`, `ManagerLayout.jsx`, `src/pages/manager/ManagerLanesPage.jsx`, `ManagerBookingsPage.jsx`; thêm trang `ManagerIncidentsPage.jsx` và `src/api/manager.incidents.api.js` dùng `src/api/client.js`. |
| `LuxeWash_Mobile_FE` | Expo SDK 54, `src/services/api/bookingService.ts`, `notificationService.ts`, `src/services/pushNotificationService.ts`, `src/contexts/NotificationContext.tsx`, `src/app/notifications/index.tsx`, `src/app/(main)/appointments.tsx`; thêm route chi tiết case trong `src/app` theo cấu trúc Expo Router hiện tại. Dùng `apiClient` hiện có, không tạo HTTP client thứ hai. |

### Rà soát bắt buộc trước khi code

1. Ghi nhận `git status` của cả ba repo, không ghi đè thay đổi sẵn có. Đọc thực tế các luồng thanh toán Wallet/PayOS, voucher, business booking, status/check-in, exception middleware và migration trước khi sửa.
2. Vẽ ma trận mọi nơi đọc `TimeSlot.MaxCapacity` hoặc ghi `DailySlotCapacity.BookedWeight` (bao gồm API không có trong bảng trên); cập nhật **tất cả** nơi nhận/chuyển chỗ. Tìm bằng `rg` trong cả BLL/API trước và sau triển khai.
3. Xác định các giá trị `Booking.Status` thật sự đang dùng (`Pending`, `Confirmed`, `CheckedIn`, `Processing`, `Completed`, `Cancelled`, `CancelledBySystem`...) và mọi đường cập nhật công suất. Không thêm status mới vào `Booking` nếu chưa cập nhật toàn bộ consumer.
4. Chạy test/build baseline trước thay đổi. Backend hiện chưa thấy test project trong solution; phải tạo test project thay vì báo `dotnet test` thành công với 0 test.

## 12. Thiết kế dữ liệu chi tiết

### 12.1. Các bảng và ràng buộc

| Bảng | Cột chính và quy tắc | Index/ràng buộc |
| --- | --- | --- |
| `BranchIncident` | `Id bigint`, `BranchId`, `Type` (`LaneFailure`, `PowerOutage`, `WaterOutage`, `Other`), `Scope` (`SelectedLanes`, `WholeBranch`), `Reason`, `StartedAtVn`, `EstimatedEndAtVn`, `ActualEndAtVn?`, `Status` (`Active`, `Resolved`), `CreatedByUserId`, `ResolvedByUserId?`, `CreatedAtVn`, `UpdatedAtVn`, `Version` application-managed | Index `(BranchId, Status, StartedAtVn, EstimatedEndAtVn)`; `EstimatedEndAtVn > StartedAtVn`; scope SelectedLanes cần ít nhất một lane. Không xóa cứng incident đã tạo. |
| `IncidentLane` | `IncidentId`, `LaneId` | PK `(IncidentId, LaneId)`; lane thuộc đúng branch và được xác thực ở service; FK restrict delete. WholeBranch có thể không cần hàng con. |
| `IncidentChange` | `Id`, `IncidentId`, `ActorUserId`, `Action` (`Created`, `Extended`, `Resolved`, `OverrideAffectedBooking`), `OldEstimatedEndAtVn?`, `NewEstimatedEndAtVn?`, `OccurredAtVn`, `Note`, JSON snapshot tối thiểu | Index `(IncidentId, OccurredAtVn)`; chỉ append, không sửa/xóa. |
| `IncidentAffectedBooking` | `Id`, `IncidentId` (incident đầu tiên), `BookingId`, `ActiveBookingId?`, `UserId?`, `Status` (`AwaitingCustomer`, `NeedsManualHandling`, `Transferred`, `Cancelled`, `Kept`), `ResponseDeadlineAtVn`, `DecidedAtVn?`, `Decision?`, `OriginalBranchId`, `OriginalScheduledTimeVn`, `TargetBranchId?`, `TargetSlotId?`, `CompensationUserVoucherId?`, `Version` | Unique `(IncidentId, BookingId)`; unique `ActiveBookingId` (nullable), set `ActiveBookingId=BookingId` khi actionable và null khi terminal; index `(Status, ResponseDeadlineAtVn)` và `(UserId, Status)`; FK Restrict. |
| `IncidentCaseCause` | `CaseId`, `IncidentId` | PK `(CaseId, IncidentId)`; liên kết thêm incident chồng nhau vào case actionable đang có, không tạo notification/voucher thứ hai chỉ vì có thêm nguyên nhân. |
| `IncidentFinancialOperation` | `CaseId`, `Kind` (`MoneyRefund`, `PointsRefund`, `RestoreOriginalVoucher`, `CompensationVoucher`), `Amount?`, `ExternalReference?`, `CreatedAtVn` | Unique `(CaseId, Kind)`; dùng làm idempotency marker/ledger liên kết `Transaction`, `PointLedger` và `UserVoucher`. Không lưu mật khẩu/payment secret. |
| `LaneSlotCapacity` | `LaneId`, `SlotId`, `MaxWeightUnits >= 0` (đóng góp của lane vào slot), `IsCalibrated` | Unique `(LaneId, SlotId)`; lane/slot cùng branch; `MaxWeightUnits` là cấu hình, không phải số booking. Chỉ coi slot đã hiệu chỉnh khi mọi lane liên quan có `IsCalibrated=true`. |
| `IncidentDelivery` | `AffectedBookingId`, `Channel`, `EventKind`, `State`, `AttemptCount`, `LastAttemptAtVn`, `IncidentVersion` | Unique `(AffectedBookingId, Channel, EventKind, IncidentVersion)` để retry không tạo bản thông báo trong app vô hạn. |

Trong `UserVoucher` bổ sung `SourceIncidentAffectedBookingId?` (unique khi khác null) và `ExcludedBookingId?`; áp dụng kiểm tra excluded booking ở điểm sử dụng voucher. **Phải có unique constraint ở database**, không chỉ `AnyAsync` trước khi insert. MySQL cho phép nhiều NULL trong unique index nên `ActiveBookingId` nullable khóa được một case actionable/booking mà vẫn giữ lịch sử terminal. Cấu hình `Version` là application-managed concurrency token trong EF (`IsConcurrencyToken`, tăng mỗi lần ghi), không giả định MySQL có SQL Server `rowversion`. Tránh lưu FCM/APNs/Expo token trên entity incident.

### 12.2. Migration và dữ liệu cũ

- EF migration mới trong `DAL/Migrations`, cập nhật snapshot; thử `dotnet ef database update` trên DB thử nghiệm và kiểm tra rollback của migration, **không tự chạy trên DB production**.
- `LaneSlotCapacity` cần backfill cho mọi cặp lane/slot hiện hoạt. Vì DB cũ không lưu sức chứa theo lane, seed tạm theo quotient/remainder của `TimeSlot.MaxCapacity` trên các lane phù hợp, gắn `IsCalibrated=false` để Manager/Admin xác nhận theo năng lực thực tế; thêm endpoint/form hiệu chỉnh cấu hình riêng, audit người xác nhận. Không bật partial-outage tại chi nhánh chưa hiệu chỉnh; nếu thiếu config thì fail closed (không nhận lịch mới trong slot bị sự cố), hiển thị lỗi cấu hình cho Manager/Admin. Không đoán công suất bằng phép trừ số lane đơn giản.
- Giữ nguyên `TimeSlot.MaxCapacity` và `DailySlotCapacity.BookedWeight`; không dùng migration để đặt capacity của tất cả slot hôm đó về 0. Incident là lớp phủ có thời hạn.
- Xem lại FK và cascade delete: xóa lane/branch/bookings liên quan incident phải bị chặn hoặc soft-delete; dữ liệu lịch sử/hoàn tiền cần còn để audit.

### 12.3. Thời gian

Backend hiện dùng `TimeHelper.VnNow` và `Booking.ScheduledTime` dạng giờ Việt Nam không offset. Với tính năng mới, nhận request ISO-8601 **có offset** (ví dụ `2026-09-18T14:30:00+07:00`), chuyển đúng một lần sang giờ Việt Nam trước khi so sánh/lưu `*AtVn`. Response cũng trả ISO có `+07:00`; không gắn hậu tố `Z` cho một giá trị giờ VN. Không dùng `DateTime.Now` phụ thuộc timezone máy chủ. Cố định một helper chuyển đổi và test máy chạy UTC/VN, slot qua 00:00, ETA đúng bằng slot start/end. Khoảng thời gian đều dùng quy tắc half-open `[start, end)`.

### 12.4. State machine

`BranchIncident`: `Active -> Resolved` là transition terminal; ETA quá hạn vẫn `Active` và có cờ tính toán `isOverdue`, không tự chuyển `Resolved`. Gia hạn chỉ ở `Active`, version tăng; `Resolved` không gia hạn lại. Báo sai sự cố thì `Resolve` kèm audit note, không xóa record.

`IncidentAffectedBooking`: `AwaitingCustomer -> Cancelled | Transferred | Kept | NeedsManualHandling`; khi qua hạn phản hồi nhưng chưa tới giờ hẹn, chuyển `NeedsManualHandling`, không hủy ngay. Booking quá sát giờ/không có user/chủ thể doanh nghiệp cũng bắt đầu ở `NeedsManualHandling`; `NeedsManualHandling -> Cancelled | Transferred | Kept` qua thao tác Manager có audit hoặc auto-cancel/auto-keep đúng thời điểm theo mục 10. `Keep` (khách, Manager hoặc job) chỉ khi booking gốc lại đủ khả năng phục vụ. Khi case final, `ActiveBookingId=null`. Không cho final -> actionable hoặc final -> final khác; nếu sự cố mới xuất hiện thì tạo case mới. Nếu một case đang chờ và có incident chồng nhau, chỉ thêm cause/recalculate deadline, không reset lựa chọn hoặc cấp voucher thứ hai.

## 13. Thuật toán công suất và bảo vệ đường đặt lịch

### 13.1. Hàm thuần cần có

`GetEffectiveSlotCapacity(branchId, date, slotId, bookingContext, nowVn)` trả `{baseCapacity, effectiveCapacity, bookedWeight, availableWeight, closedReason, incidentIds, eligibleLaneIds, capacityVersion}`. `bookingContext` gồm Personal/Business, VIP eligibility, vehicle type, service IDs, capacity weight và thời lượng ước tính nếu scheduler dùng. Có thể tách hàm tính công suất tổng và `CanPlaceOnEligibleLane` để không lẫn `BookedWeight` chung với ràng buộc buồng riêng.

1. Lấy slot thuộc branch; xác định `[slotStart, slotEnd)` kể cả slot qua nửa đêm.
2. Lấy tập incident `Active` giao khoảng slot. Nếu có WholeBranch thì `effectiveCapacity=0`. Với SelectedLanes, lấy **hợp** của lane IDs bị loại; nếu hai incident chồng lên nhau thì không trừ lane hai lần. Lane `IsActive=false` từ quản lý thông thường cũng không được tính.
3. `effectiveCapacity = min(slot.MaxCapacity, sum(LaneSlotCapacity.MaxWeightUnits của các lane còn dùng được))`. Không có cấu hình đã xác nhận: slot có incident trả 0 và `CONFIGURATION_REQUIRED`; slot không có incident giữ hành vi cũ để không phá hệ thống hiện tại. Khi thêm/xóa lane hoặc sửa `TimeSlot.MaxCapacity`, đánh dấu cấu hình liên quan chưa calibrated và không tự tuyên bố lane mới đủ công suất.
4. Chạy kiểm tra loại lane/scheduler cho request. Theo routing hiện tại, business cần `IsBusinessLane=true`, personal dùng lane `IsBusinessLane=false`; giữ nguyên quy tắc `TimeSlot.IsVipOnly` cho hạng khách và xác minh cách dùng `Lane.IsVipLane` trước khi thay đổi. Tổng capacity còn nhưng không còn lane có thể nhận loại booking đó thì trả không khả dụng; không suy diễn mỗi loại booking có một `BookedWeight` riêng nếu bảng vẫn dùng pool chung.
5. `bookedWeight` từ `DailySlotCapacity`, đối soát với booking chưa terminal để phát hiện drift. Với slot bị hỏng, nếu `bookedWeight > effectiveCapacity` thì slot đóng nhận lịch mới nhưng vẫn tạo impact cases cho lịch đã có.

`ETA` không phải thời điểm tự `Resolve`. Trước ETA, hệ thống dự báo chỉ giảm sức chứa tới ETA. Khi `nowVn >= ETA` mà incident vẫn `Active`, background reconciliation coi phạm vi còn lại là chưa xác định/đang hỏng, chặn booking mới cho mọi slot tương lai trong cửa sổ đặt lịch của chi nhánh cho đến khi Manager gia hạn hoặc Resolve; gửi cảnh báo quá hạn. Nếu Manager gia hạn sau đó, tính lại từ ETA cũ đến ETA mới và các slot vừa chặn.

### 13.2. Điểm chặn bắt buộc

Tất cả điểm sau phải gọi cùng policy, không sao chép công thức: `BookingService.GetAvailableSlotsAsync`, `GetAvailableSlotsWithSuggestionAsync`, `ValidateBookingCompatibilityAsync`, `CreateBookingAsync`, `CreateWalkInBookingAsync`, `RescheduleBookingAsync`, chuyển booking theo incident; `BusinessBookingService.GetAvailableSlotsForBusinessAsync`, `CreateBusinessBookingAsync`, `RescheduleBookingAsync`, `WalkInAsync`; `LaneSchedulerService.ScheduleFleetAcrossSlotsAsync` và admission/check-in nếu lane được đánh dấu hỏng. Kiểm tra thêm các method mới phát hiện bằng `rg MaxCapacity|BookedWeight|IsActive`.

Không chỉ sửa endpoint xem slot: create/transfer phải **re-check bên trong transaction**. Với MySQL/Pomelo, dùng transaction ngắn, khóa/cập nhật hàng `DailySlotCapacity` theo thứ tự ổn định `(branchId, date, slotId)`; nếu hàng chưa tồn tại thì insert dưới unique `(SlotId, Date, BranchId)` và xử lý race. Re-read incident đang Active sau khi có khóa; update booked weight chỉ nếu điều kiện công suất mới còn đúng. Bắt deadlock/concurrency và retry tối đa 3 lần với context/transaction mới, sau đó trả `409 CAPACITY_CHANGED`. Không giữ transaction khi gọi FCM/email hay chờ khách. Luồng tạo incident commit incident trước rồi enqueue reconciliation; request đặt mới sau commit nhìn thấy capacity mới, còn booking chen vào lúc chuyển trạng thái được job reconcile bắt lại.

### 13.3. Đối soát `BookedWeight`

`DailySlotCapacity.BookedWeight` đã được nhiều luồng cập nhật, có nguy cơ lệch. Tạo query đối soát theo branch/date/slot từ booking còn giữ chỗ (`Pending`/`Confirmed` và những trạng thái thực tế vẫn chiếm chỗ theo policy hiện tại). Preview trả `bookedWeightStored`, `bookedWeightCalculated`, `drift`. Không tự âm thầm trừ/chỉnh trong request incident; log cảnh báo và cung cấp script/job sửa có kiểm thử riêng. Các thao tác Cancel/Transfer chỉ trừ/cộng **một lần**, có idempotency và không `Math.Max(0, ...)` để che lỗi ledger: nếu âm thì rollback + báo lỗi vận hành.

## 14. Chọn booking bị ảnh hưởng

1. Duyệt slot giao sự cố theo ngày, tính lại công suất và khả năng xếp vào lane đủ điều kiện. Lấy booking ở chi nhánh gốc, thời gian trong slot, chưa terminal; sắp xếp ổn định theo `(ScheduledTime, CreatedAt, BookingId)` để kết quả preview và commit giống nhau.
2. Booking `CheckedIn`/`Processing` hoặc xe đang trong buồng: đưa `NeedsManualHandling`, báo Manager/nhân viên; không gửi nút chuyển/hủy tự phục vụ. Với Pending/Confirmed, giữ booking sớm nhất mà vẫn xếp được, phần còn lại tạo `AwaitingCustomer`. Nếu chỉ hỏng lane VIP/business nhưng tổng weight chưa vượt, vẫn đánh giá lane compatibility; không chỉ so tổng số.
3. Nếu một booking bị ảnh hưởng bởi nhiều incident chồng nhau, giữ **một case actionable tại một thời điểm cho booking** (`ActiveBookingId` unique), thêm quan hệ `IncidentCaseCause` cho incident mới, cập nhật ETA/reason và gửi tối đa một thông báo cập nhật có dedup. Sau khi một case terminal, một sự cố mới thực sự độc lập mới được tạo case mới. Không gửi overload suggestion khác khi đang có case incident actionable.
4. Preview là ảnh chụp có `capacityVersion`/`generatedAt`; create phải tính lại trong transaction, nếu danh sách thay đổi thì trả `409 IMPACT_PREVIEW_STALE` kèm preview mới, không âm thầm thông báo người khác với bản Manager đã duyệt. `previewVersion` là hash của dữ liệu đã chuẩn hóa: input incident, ID/version của active incidents giao khoảng, lane capacity config, ID/status/weight/time của bookings liên quan và `BookedWeight`; không dùng riêng timestamp. Manager override chỉ được hoán đổi booking trong tập giữ/chuyển sao cho tập **được giữ vẫn xếp được trên lane**; bắt buộc lý do và audit.
5. Case không có chi nhánh thay thế vẫn hiển thị nút Cancel; UI chuyển chi nhánh disabled cùng lý do. Trường hợp quá sát giờ, không có `UserId`, hoặc lịch doanh nghiệp: ManualHandling + kênh liên hệ phù hợp.

## 15. API contract chi tiết

Giữ quy ước backend hiện tại: route dưới `/api/v1`, JSON camelCase, response thành công `{statusCode,message,data}`, lỗi từ `ExceptionMiddleware` `{statusCode,errorCode,message,details}`. Các tên dưới đây là mục tiêu tích hợp; cập nhật OpenAPI/Swagger và hai API client cùng lúc. ID kiểu số nguyên, tiền VND dạng số nguyên/decimal server-side, thời gian ISO có offset. Dùng validation model và mã lỗi ổn định, không dựa vào so khớp chuỗi thông báo.

### 15.1. Manager

`POST /api/v1/manager/incidents/preview` và `POST /api/v1/manager/incidents` nhận cùng body:

```json
{
  "type": "LaneFailure",
  "scope": "SelectedLanes",
  "laneIds": [12, 14],
  "reason": "Buồng 1 và 3 mất áp lực nước",
  "startedAt": "2026-09-18T14:00:00+07:00",
  "estimatedEndAt": "2026-09-19T11:00:00+07:00",
  "previewVersion": "..."
}
```

`previewVersion` chỉ bắt buộc ở create, không cần ở preview. Với `WholeBranch`, `laneIds=[]`. API phải giới hạn ETA > startedAt, khoảng bắt đầu không vô lý, lane thuộc chi nhánh Manager và là lane chưa bị xóa; không nhận `branchId` tùy ý trong body. Validate lý do, giới hạn độ dài/khoảng ETA bằng config. Nếu đã có incident giao khoảng, cho phép nhưng tính **hợp lane**, không trừ lặp; UI cảnh báo.

Giá trị mặc định cho validation: `startedAt` không quá 24 giờ trước server-now VN và không quá 5 phút trong tương lai; `estimatedEndAt > max(startedAt, nowVn)` và không quá 30 ngày sau `startedAt`; `reason` sau trim dài 10–500 ký tự; không cho `laneIds` trùng hoặc rỗng khi SelectedLanes. Các ngưỡng này đặt trong server config, không hard-code trong nhiều controller. Với incident bắt đầu trong quá khứ, chỉ đánh giá booking còn khả năng phục vụ/hoàn tiền; không gửi lựa chọn chuyển cho giờ đã qua.

Response preview trả tối thiểu:

```json
{
  "branchId": 1,
  "previewVersion": "sha256-or-monotonic-version",
  "generatedAt": "2026-09-18T13:55:00+07:00",
  "slots": [{"date":"2026-09-18","slotId":8,"startAt":"2026-09-18T14:00:00+07:00","baseCapacity":6,"effectiveCapacity":3,"bookedWeight":5,"overflowWeight":2,"closedReason":"INCIDENT_PARTIAL"}],
  "affectedBookings": [{"bookingId":330,"status":"Pending","scheduledAt":"2026-09-18T14:00:00+07:00","handling":"CustomerDecision"}],
  "warnings": []
}
```

`POST create` trả `201` với incident ID/version, summary impact và notification job status; không chờ gửi push trong HTTP request. `GET /manager/incidents?status=Active&from=...&to=...` trả phân trang; `GET /manager/incidents/{id}` trả chi tiết/audit; `GET /manager/incidents/{id}/impact` trả phân trang theo `AwaitingCustomer/NeedsManualHandling/Transferred/Cancelled/Kept` và delivery state.

`POST /manager/incidents/{id}/extend` body `{ "estimatedEndAt":"...+07:00", "reason":"...", "expectedVersion":3 }`. Chỉ active, end mới phải lớn hơn end cũ và lớn hơn now. `POST /manager/incidents/{id}/resolve` body `{ "note":"...", "expectedVersion":4 }`; server ghi `ActualEndAtVn=now`, không tin client tự lùi thời gian. Conflict version trả `409 INCIDENT_VERSION_CHANGED`. Nếu muốn sửa danh sách lane hỏng, tạo action rõ ràng/incident mới; không lén thay ở `extend`.

`POST /manager/incidents/{id}/extend-preview` nhận ETA mới/reason nhưng không ghi; trả delta slot/booking để web xác nhận trước khi gọi `extend`. `extend` nhận `previewVersion` và re-evaluate; nếu stale trả 409 như create. Dùng cùng hàm hash preview ở mục 14, có thêm `expectedVersion` của incident; timestamp một mình không đủ.

`POST /manager/incidents/{id}/cases/{caseId}/manual-decision` chỉ dùng cho `NeedsManualHandling`, body `{ "decision":"Cancel|Transfer|Keep", "targetBranchId":null, "targetSlotId":null, "contactMethod":"Phone|InPerson", "customerConsentRecorded":true, "note":"...", "expectedVersion":2 }`. Manager phải ghi rõ đã liên hệ/được đồng ý; backend dùng cùng service Cancel/Transfer, không viết lại logic tài chính. Với booking doanh nghiệp, kiểm tra người đại diện/quy trình thanh toán riêng; không dùng voucher cá nhân. Nếu chưa được khách đồng ý và chưa tới hạn auto-cancel, Manager không được tự ý chuyển lịch.

### 15.2. Khách hàng

`GET /api/v1/bookings/{bookingId}/incident-options` trả 200 kể cả khi không có case, với `data=null` (chọn contract này nhất quán). Khi có case: `caseId`, `incidentId`, `caseStatus`, `originalBooking`, `reason`, `eta`, `responseDeadlineAt`, `allowedActions`, `alternatives[]`, `refundPreview`, `voucherTerms`, `version`. Mỗi alternative chứa `branchId`, `branchName`, `slotId`, `startAt`, `endAt`, `distanceKm?`, `availableWeight`, `priceDifferenceCharged=0`; không trả `expiresAt` khi bản đầu chưa có hold. Không trả token/device data. `GET /api/v1/bookings/me` hoặc DTO booking phải có `hasPendingIncidentAction`/`incidentCaseId` để danh sách lịch hiển thị yêu cầu ngay cả khi notification không đến.

`POST /api/v1/bookings/{bookingId}/incident-decision` nhận `Idempotency-Key` header và body:

```json
{"incidentId":42,"caseId":91,"expectedVersion":2,"decision":"Transfer","targetBranchId":5,"targetSlotId":73}
```

Với `Cancel` và `Keep`, bỏ các trường target. Server tự lấy userId từ JWT, không nhận từ body. Chỉ cho `Keep` nếu incident đã resolved và booking gốc còn khả thi. Response trả `decision`, `booking`, `refund {amount,destination,pointsRestored,originalVoucherRestored}`, `compensationVoucher {voucherId,discountPercent,expiresAt}` (null cho Keep), `caseStatus`; request lặp cùng key trả **cùng kết quả**, request khác sau khi đã quyết định trả `409 DECISION_ALREADY_FINAL`. Chuyển đích hết chỗ: `409 DESTINATION_CAPACITY_CHANGED`; case vẫn mở, mobile refetch alternatives. Khách hết hạn: `409 DECISION_EXPIRED` (hoặc đã được job xử lý thì trả kết quả final idempotent).

### 15.3. Error/status matrix

| HTTP | `errorCode` | Xử lý client |
| --- | --- | --- |
| 400 | `INCIDENT_INVALID_WINDOW`, `LANE_NOT_IN_BRANCH`, `INVALID_DESTINATION` | Hiện lỗi tại field; không retry tự động. |
| 401/403 | Auth hoặc `INCIDENT_BRANCH_FORBIDDEN` | Đăng nhập lại hoặc báo không có quyền. |
| 404 | `INCIDENT_NOT_FOUND`, `CASE_NOT_FOUND` | Refresh danh sách, không hiện modal rỗng. |
| 409 | `IMPACT_PREVIEW_STALE`, `INCIDENT_VERSION_CHANGED`, `DESTINATION_CAPACITY_CHANGED`, `DECISION_ALREADY_FINAL`, `DECISION_EXPIRED`, `CAPACITY_CHANGED` | Refetch đúng resource; không blind retry với payload cũ. |
| 500/503 | `INCIDENT_PROCESSING_FAILED`/hạ tầng | Giữ draft, cho retry với **cùng** idempotency key; theo dõi outbox. |

## 16. Quyết định hủy/chuyển, thanh toán và voucher

### 16.1. Hủy

Trong transaction: khóa case/booking/slot, xác nhận status `Pending`/`Confirmed`, đánh dấu booking `CancelledBySystem` với lý do incident, giải phóng `CapacityWeight` đúng một lần, vô hiệu payment link/transaction Pending để webhook đến muộn không kích hoạt lại booking. Với thanh toán đã hoàn tất, hoàn **đúng số tiền thực thu** về Wallet theo cơ chế hiện tại (không hứa hoàn về tài khoản ngân hàng nếu chưa có integration đó), tạo ledger `Refund` có khóa duy nhất theo case/operation; trả điểm đã trừ, khôi phục voucher cũ và usage counters theo logic hủy hiện có. Booking chưa thanh toán không phát sinh tiền hoàn. Sau đó cấp voucher 20%, cập nhật case `Cancelled`, ghi outbox xác nhận. Không gọi method hủy thông thường nếu nó áp điều kiện thời gian/phí hủy không phù hợp.

Với auto-cancel do hết giờ theo mục 10, dùng **cùng service transaction** và idempotency key nội bộ `incident:{id}:booking:{id}:auto-cancel`; không viết một đường hủy thứ hai. Đối với booking đã check-in/processing, job không auto-cancel; Manager xử lý thủ công.

### 16.2. Chuyển

Trong một transaction ngắn: khóa case/booking và hai `DailySlotCapacity` theo thứ tự khóa ổn định; kiểm tra case còn actionable, đích vẫn khác branch gốc, cùng ngày/khung giờ tương đương, service/vehicle tương thích, branch hoạt động, lane không bị incident khác chặn, tổng capacity và scheduler còn cho phép. “Cùng giờ” nghĩa là slot đích chứa **thời điểm bắt đầu gốc** và scheduler đủ thời lượng rửa; giữ `ScheduledTime` bằng giờ gốc nếu khả thi, không âm thầm dời sang giờ khác. Trừ weight nguồn, cộng weight đích, cập nhật **cùng BookingId** (`BranchId`, `ScheduledTime`, `UpdatedAt`), lưu nguồn/đích cũ-mới vào case/audit. Giữ nguyên `OriginalPrice`, `FinalAmount`, `PointsUsed`, `AppliedVoucherId`, transaction thanh toán và QR/booking identity trừ khi logic QR hiện tại bắt buộc phát hành lại; phải kiểm thử. Không tái tính giá hay thu tiền bổ sung. Nếu chi nhánh mới có giá thấp hơn, bản đầu cũng giữ giá đã đồng ý; bộ phận kế toán xử lý phân bổ doanh thu giữa hai chi nhánh bằng record chuyển, không mutate hóa đơn đã phát hành.

Không cần hold slot dài nếu xác nhận atomically; `alternatives[]` là ảnh chụp tham khảo, không phải bảo đảm chỗ. Có thể thêm hold TTL sau này, nhưng khi đó hold phải được trừ vào capacity trên **mọi** đường đặt lịch và dọn hết hạn.

### 16.3. Voucher 20% cho lần rửa tiếp theo

Tạo dedicated method `IssueIncidentCompensationAsync(case, booking)` tham gia **cùng DbContext/transaction**, không gọi `GenerateCompensationVoucherAsync` vì method đó tạo 30.000đ/7 ngày. Entity `Voucher` hiện hỗ trợ `DiscountPercent`, `MaxDiscountAmount`, `UserVoucher`; generic create validator hiện yêu cầu cap cho voucher phần trăm. Dedicated issuance cần tạo voucher system-issued với `DiscountAmount=0`, `DiscountPercent=20`, `MaxDiscountAmount=null`, `MinOrderAmount=0`, `MaxUsages=1`, `MaxUsagePerUser=1`, `PointsRequired=0`, `IsActive=true`, `RequiredTierId=null`, `VehicleTypeId=null`, `BranchId=null`, `VoucherType=Discount`, `StartDate=issuedAt`, `ExpiryDate=issuedAt.AddMonths(6)`. Không thay validation voucher admin để lách lỗi; nếu policy chung bắt buộc cap, tách rule system compensation có test rõ.

`UserVoucher` có `ReceivedDate=issuedAt`, `ExpiryDate` giống voucher, `TriggerKey="INCIDENT_CASE:{caseId}"` và `SourceIncidentAffectedBookingId` unique. Mã voucher dạng `INC-{caseId}-{random}` nằm trong giới hạn 50 ký tự, unique DB; retry collision. Đảm bảo API liệt kê voucher và `CalculateBookingPricingAsync` nhận voucher này đúng điều kiện; không cho booking nguồn đã chuyển dùng lại (`ExcludedBookingId`). Với hủy, voucher dùng được trên booking **mới** sau quyết định. Với chuyển, voucher dùng cho booking tạo sau thời điểm quyết định, không cho áp lên booking nguồn; nếu hệ thống có endpoint sửa voucher của booking cũ thì chặn theo excluded ID. Nếu nhiều incident cùng ảnh hưởng một booking, không cấp lặp chỉ do thông báo lặp: unique/idempotency theo case.

## 17. Thông báo, background jobs và thiết bị

1. Transaction tạo case ghi **một outbox event** `IncidentActionRequired` với key `(caseId, version)`; gửi bên ngoài transaction. `OutboxProcessorService` hiện chỉ biết loại lane/barrier, nên phải thêm handler incident hoặc worker riêng. Không để loại outbox mới rơi vào nhánh `Unsupported outbox message type` và không đánh dấu processed khi chưa thật sự xử lý.
2. Worker tạo `UserNotification` loại `INCIDENT_ACTION_REQUIRED` một lần, thống nhất `ReferenceId=bookingId`; gửi SignalR cho app mở và push cho thiết bị. Dùng `CreateInAppNotificationAsync` (không tự gửi push) cho bản ghi in-app rồi gọi **một** push provider theo token; không gọi `CreateNotificationAsync` (đã gửi push) rồi gửi thêm push. Push payload tối thiểu `{type, incidentId, caseId, bookingId, version}`; **không** nhúng `alternatives` vì chỗ trống thay đổi. Có retry/backoff, dead-letter sau ngưỡng, màn hình Manager hiển thị failed delivery. Push gửi thành công chỉ có nghĩa provider nhận request, không coi là khách đã đọc.
3. Job định kỳ kiểm tra case sắp hết hạn/chưa phản hồi, ETA quá hạn, lỗi delivery và incident vừa được resolve. Nhắc khách một lần theo key dedup; Manager thấy hàng cần gọi điện. Đến lịch mà chưa quyết định: nếu đã đủ công suất, đóng `Kept`; nếu vẫn thiếu công suất, áp policy auto-cancel mục 10 bằng cùng cancellation service. Tránh tác vụ gửi email/push kiểu fire-and-forget trong request.
4. Mobile hiện lấy native FCM token Android và backend dùng Firebase Admin. **APNs token iOS không gửi được bằng `FirebaseMessaging.SendAsync` như FCM token.** Bản đầu dùng `getExpoPushTokenAsync` trên iOS (app đã có `extra.eas.projectId`) và backend adapter gọi Expo Push Service; Android FCM giữ nguyên. Tạo `UserPushToken` với `Provider=Fcm|Expo`, `Platform=Android|Ios`, token unique, user ID, active/invalid timestamps; migration copy token cũ từ `UserFcmToken` thành `Fcm/Android`, giữ bảng cũ cho đến khi xác nhận không còn consumer. Mở rộng `POST/DELETE /api/v1/notifications/token` nhận `provider`/`platform`, giữ body cũ tương thích Android. Worker route đúng provider, xử lý ticket/receipt/error invalid token của Expo và lỗi token của FCM. Không gửi Expo token vào FCM hoặc APNs token vào FCM. Kiểm thử credential, sandbox/production, token rotation, đăng xuất và nhiều thiết bị. Nếu team muốn FCM cho iOS thì đó là thay đổi thiết kế riêng: cần cấu hình native Firebase iOS và xác nhận app thực sự đăng ký **FCM registration token**.
5. Android remote push trong Expo Go SDK 54 không khả dụng; kiểm thử bằng development/release build. Dù không có quyền notification, in-app inbox và API booking vẫn hoạt động. Tham chiếu SDK 54: https://docs.expo.dev/versions/v54.0.0/sdk/notifications/.

## 18. Web Manager: đặc tả màn hình và state

### 18.1. Routing/API layer

- Thêm `/manager/incidents` vào `src/App.jsx`, menu trong `ManagerSidebar.jsx`, title trong `ManagerLayout.jsx`. Trang chỉ cho role Manager của branch hiện tại; nếu Admin được hỗ trợ sau này phải có branch selection/authorization rõ ràng.
- `src/api/manager.incidents.api.js`: `previewIncident`, `createIncident`, `listIncidents`, `getIncident`, `getImpact`, `extendIncident`, `resolveIncident`; tái sử dụng `apiRequest`, chuẩn hóa camelCase và errorCode, không parse message để quyết định logic. Request đang lưu draft ở local React state; không mất lựa chọn lane/ETA khi preview hoặc save lỗi.
- Không dùng mutation sửa `Lane.IsActive` hoặc `TimeSlot.MaxCapacity` cho sự cố. `ManagerLanesPage` có thể liên kết sang trang sự cố và badge “đang bảo trì” lấy từ incident API. `ManagerBookingsPage` đọc impact flags để phân biệt booking còn phục vụ/đang chờ khách/đã chuyển/đã hủy.

### 18.2. Trang danh sách/chi tiết

Danh sách gồm loại sự cố, branch, lanes, startedAt, ETA, thời gian thực tế, `Active/Resolved/Overdue`, số slot ảnh hưởng, số booking cần phản hồi, số manual, số gửi notification thất bại. Filter trạng thái/ngày, refresh. Detail có timeline audit, bảng slot theo ngày (`base/effective/booked/overflow`), bảng case với thông tin khách cần liên hệ và kết quả quyết định. Ẩn PII không cần thiết theo vai trò; số điện thoại chỉ hiện nơi Manager được phép xem.

Form tạo gồm scope WholeBranch/SelectedLanes, lựa chọn lane nhiều giá trị, reason, start mặc định `now`, ETA; validate trên client nhưng backend là nguồn sự thật. Bấm “Xem ảnh hưởng” gọi preview, hiển thị trước-sau, danh sách sẽ được thông báo và lời cảnh báo khi không có destination. Chỉ enable “Xác nhận sự cố” sau preview; gửi `previewVersion`. Nếu `409 IMPACT_PREVIEW_STALE`, thay preview, yêu cầu Manager xem/xác nhận lại. Không tự gửi lại request tạo cũ.

Gia hạn có input ETA mới và reason, hiển thị số slot/booking mới bị ảnh hưởng trước khi confirm. Resolve có hộp xác nhận “buồng đã hoạt động”, hiển thị các case chưa trả lời và policy Keep. Nút disabled trong lúc request; retry giữ form. Sau success invalidate/refetch danh sách, detail, bookings, lanes; không hiện thông báo “khách đã nhận” chỉ dựa trên việc gửi provider thành công.

### 18.3. Trạng thái giao diện

Mỗi màn hình dữ liệu phải có loading, empty, error, content; lỗi refetch giữ dữ liệu cũ và banner retry. Slot hết công suất vì incident hiển thị lý do và ETA; slot bình thường nhưng đầy lịch hiển thị khác với slot đóng. Date picker dùng timezone VN theo contract API, không `new Date(...).toISOString()` rồi vô tình đổi ngày địa phương. Bảng dài phân trang server-side/virtualize theo khả năng hiện có.

## 19. Mobile Expo SDK 54: đặc tả tích hợp

### 19.1. Data layer và route

- Thêm type `IncidentOptions`, `IncidentAlternative`, `IncidentDecisionRequest`, `IncidentDecisionResponse` vào `src/services/api/bookingService.ts` hoặc file type phù hợp; method `getIncidentOptions(bookingId)` và `submitIncidentDecision(...)`. `apiClient.post` hiện không nhận custom header; mở rộng client một cách tương thích để gắn `Idempotency-Key` mà vẫn giữ auth/refresh/error handling. Không tạo client HTTP song song.
- Thêm route như `src/app/booking/[id]/incident.tsx` theo cấu trúc Expo Router hiện tại, đăng ký trong root Stack nếu cần. Notification list (`src/app/notifications/index.tsx`) xử lý `INCIDENT_ACTION_REQUIRED` bằng `referenceId`/payload để mở route này; `OverloadSuggestionContext` đang bắt push chung, cần phân tuyến theo `type` để không đưa incident sang modal quá tải.
- Trong `src/app/(main)/appointments.tsx` và booking detail, hiển thị banner “Cần quyết định do sự cố” nếu API booking trả pending case. App mở từ push khi auth chưa hydrate thì lưu target, sau login điều hướng đúng case; cold start chỉ xử lý một lần, chống navigate trùng.

### 19.2. UX quyết định

Đầu trang: booking ID/biển số, branch, giờ cũ, nguyên nhân, ETA và hạn phản hồi theo VN. Hai card Cancel/Transfer; Transfer liệt kê tên branch, địa chỉ/khoảng cách nếu sẵn, giờ tương đương, còn chỗ (mang tính tạm thời), “không thu thêm”. Nếu alternatives rỗng, chỉ Cancel khả dụng và có hướng dẫn liên hệ. Sau resolved đủ chỗ, hiện Keep như policy mục 10. Thông báo rõ: hoàn tiền về Wallet theo backend hiện tại; voucher 20% không trần, dùng một lần trong 6 tháng cho lượt tiếp theo, không áp dụng lên lịch đang chuyển.

Tải trạng thái mới mỗi lần màn hình focus và sau push/in-app event; không cache alternatives vô thời hạn. Loading lần đầu, empty (không có case), offline/error (giữ dữ liệu cũ nhưng khóa submit khi không chắc còn hiệu lực), content, submit pending và success đều có UI riêng. Khi submit, giữ draft nếu network fail, dùng cùng idempotency key cho retry; nếu không biết request trước đã thành công hay chưa, GET case trước rồi mới retry. Với `409 DESTINATION_CAPACITY_CHANGED`, refetch, bỏ chọn destination hết chỗ, không tự động chuyển sang nơi khác. Với `DECISION_ALREADY_FINAL`, hiển thị kết quả server.

### 19.3. Push đa nền tảng

Tách parser payload incident khỏi parser `OVERLOAD_SUGGESTION`; đăng ký listener đúng một lần, xử lý foreground/background/cold-start. Android giữ FCM hiện tại; iOS dùng đường provider đã thống nhất ở mục 17 và kiểm thử thiết bị thật. Khi quyền push bị từ chối hoặc token chưa đăng ký, app vẫn poll/refresh in-app notification và bookings. Không lưu dữ liệu tài chính/PII trong push body hoặc URL. Không đặt bí mật APNs/FCM/Expo vào `EXPO_PUBLIC_*`.

## 20. Test plan có thể tự động hóa

### 20.1. Backend unit tests

Tạo test project xUnit/NUnit (solution hiện chưa có test project). Test hàm khoảng thời gian half-open: `end == incidentStart` không giao, `start == incidentEnd` không giao, qua nửa đêm, ETA ở ngày kế tiếp, máy UTC/VN. Test hợp lane của nhiều incident, WholeBranch ưu tiên 0, lane inactive, VIP/Business eligibility, missing `LaneSlotCapacity` fail closed. Test phân bổ booking stable với cùng `CreatedAt`, booking weight > 1, status Pending/Confirmed/CheckedIn/Processing. Test state transitions và không cấp voucher cho Keep.

### 20.2. Backend integration tests trên MySQL tương thích production

Không chỉ dùng EF InMemory/SQLite cho race/transaction; dùng MySQL test DB/container tương thích Pomelo. Seed branch có 3 lane (normal/VIP/business), 2 ngày/slot, booking cá nhân/doanh nghiệp, payment Wallet/Pending PayOS và voucher cũ. Test migration up/down trên DB thử nghiệm; constraint unique thực sự hoạt động.

Các ca bắt buộc:

| Ca | Kỳ vọng |
| --- | --- |
| 1/3 lane hỏng, bookedWeight <= effective | Giảm chỗ mới, không case sai. |
| 2/3 lane hỏng hoặc WholeBranch | Tạo đúng số case, API create mới trả không còn chỗ. |
| Hỏng lane business/VIP | Tổng weight còn nhưng lịch cần lane đó vẫn được phát hiện; không gợi ý destination không phù hợp. |
| Sự cố giao slot qua ngày, ETA gia hạn/overdue/resolve sớm | Chỉ slot đúng khoảng bị đóng; ETA quá hạn không tự mở. |
| Hai booking đồng thời chọn một chỗ cuối | Chỉ một Transfer commit, case còn lại nhận 409; booked weight không vượt cap. |
| Hai request cùng idempotency key hoặc callback retry | Một quyết định, một refund, một user voucher, một thay đổi capacity. |
| Cancel Wallet/PayOS pending, điểm và voucher cũ | Hoàn đúng thực thu; pending link vô hiệu; không double refund; counters voucher phục hồi. |
| Transfer giá branch khác | Giữ `BookingId`, giá/điểm/voucher/thanh toán đã chốt; audit nguồn/đích. |
| Push/SignalR/email fail sau commit | Booking/case đã commit; outbox retry, API không báo hủy/chuyển thất bại sai. |
| Manager branch A truy cập incident branch B, customer khác truy cập case | 403/404 đúng, không rò dữ liệu. |
| Reconcile `BookedWeight` lệch | Preview cảnh báo drift; không tự che lỗi bằng clamp 0. |

### 20.3. Frontend tests và device checks

Web: test form validation, preview stale, error/refetch, chống double submit, hiển thị đúng giờ VN và role/branch. Chạy `npm run lint` và `npm run build`. Mobile: typecheck (`npx tsc --noEmit`), lint, test API adapter/notification parser, deep link sau login, no push permission/offline/409/retry; Android/iPhone development hoặc release build để test remote push, in-app inbox và cold start. Chạy thử trên backend local đúng URL LAN/emulator tương ứng; không kết luận tính năng đạt chỉ vì web Expo chạy ở localhost.

## 21. Thứ tự thực hiện cho AI và đầu ra mỗi giai đoạn

1. **Audit và baseline:** liệt kê mọi path gọi capacity, các status/payment/voucher branch và trạng thái git. Chạy build/lint hiện tại. Đầu ra: ma trận điểm chạm + rủi ro cần xử lý trước khi code.
2. **Migration/capacity:** tạo entity/index/backfill + bộ tính công suất + tích hợp mọi điểm chặn; test unit/integration. Đầu ra: không tạo/chuyển được booking vào slot bị khóa, không thay cấu hình recurring.
3. **Impact/Manager API:** preview/create/extend/resolve, reconciliation, audit/version, authorization; test overlap và preview stale. Đầu ra: manager API dùng Swagger được dù chưa có UI.
4. **Customer decision/financials:** options, Cancel/Transfer/Keep, transaction/idempotency, refund/voucher, PayOS pending handling; test race và dữ liệu tài chính. Đầu ra: response contract ổn định để FE tích hợp.
5. **Notifications/jobs:** outbox handler, in-app, push Android/iOS provider, reminder/overdue/manual queue; test retry và provider failure. Đầu ra: không mất case khi push fail.
6. **Web Manager:** trang, API adapter, menu, preview/monitor/extend/resolve; lint/build và test giao diện.
7. **Mobile:** API types, route, inbox, push parser, appointments badge, bốn trạng thái dữ liệu, retry; typecheck/lint/device test.
8. **E2E và rollout:** chạy migration staging, hiệu chỉnh `LaneSlotCapacity`, pilot một branch, đo metrics, xử lý bug, rồi mở feature flag cho các branch khác. Không deploy production/áp migration production chỉ vì test local pass.

Mỗi giai đoạn: ghi file thay đổi, lệnh kiểm thử và kết quả, nêu phần chưa kiểm thử; không đánh dấu “done” khi chỉ có code mà thiếu test/đường lỗi.

## 22. Definition of Done và giới hạn triển khai

- Manager khai báo 1/nhiều/toàn bộ lane, xem chính xác slot hiện tại và ngày kế tiếp, gia hạn/resolve; lịch sử thay đổi có actor và timestamp.
- API xem chỗ trống và API ghi booking đều cùng capacity policy; không có đường cá nhân/doanh nghiệp/walk-in/reschedule/transfer vượt công suất. Lane hỏng không được admission/scheduler chọn.
- Đúng số booking không thể phục vụ nhận case; case tồn tại dù push không đến. Customer chọn Cancel/Transfer và nhận đúng một voucher 20%/6 tháng; Keep khi đã khắc phục không cấp voucher.
- Chuyển chỉ tới branch thực sự còn chỗ/đúng loại dịch vụ trong cùng giờ; giá không đổi. Hủy hoàn tiền/điểm/voucher cũ đúng một lần. Retry, concurrent request và webhook muộn không phá invariant.
- Web Manager và mobile có loading/empty/error/content, thông báo dễ hiểu và đường phục hồi khi 409/offline; push Android/iPhone và in-app fallback được kiểm thử theo phạm vi sản phẩm.
- Không sửa thẳng `TimeSlot.MaxCapacity`, không tái sử dụng voucher 30.000đ/7 ngày hoặc quá tải 10% làm đền bù sự cố, không tự mở lại ở ETA, không gọi FCM/email trong transaction, không ghi đè code người khác.

### Nguồn kỹ thuật để đối chiếu khi triển khai

- Expo SDK 54 Notifications: https://docs.expo.dev/versions/v54.0.0/sdk/notifications/.
- EF Core concurrency: https://learn.microsoft.com/en-us/ef/core/saving/concurrency.
- EF Core transactions: https://learn.microsoft.com/en-us/ef/core/saving/transactions.

## 23. Cấu hình vận hành, quan sát và rollout

- Feature flag theo branch, mặc định **off**: `IncidentManagement:EnabledBranchIds`; cho phép bật một branch pilot sau khi migration/capacity calibration/test pass. Tắt flag chỉ ngừng tạo incident mới, **không** bỏ xử lý case/outbox đang mở và không vô hiệu bộ lọc an toàn của incident Active đã tạo.
- Server config có giá trị mặc định và validation startup cho: `ResponseDeadlineMinutesBeforeSlot=30`, `MaxBackdateHours=24`, `MaxFutureStartMinutes=5`, `MaxEstimatedDurationDays=30`, `ReconciliationIntervalMinutes=5`, `NotificationRetryMaxAttempts=5`, `NotificationRetryBackoff=exponential(30s..15m)`, `ManualEscalationMinutesBeforeSlot=60`. Chỉ server quyết định hạn; FE hiển thị giá trị server trả, không tự tính khác.
- Log có `incidentId`, `caseId`, `bookingId`, `branchId`, `outboxId` và correlation ID, nhưng không log phone/email/token/payload nhạy cảm. Metric: số incident active/overdue, số case AwaitingCustomer/Manual, thời gian phản hồi, số Cancel/Transfer/Keep, số 409 destination full, capacity drift, số push/email failed/dead-letter và chênh lệch refund. Có màn hình/alert cho Manager hoặc ops khi ETA quá hạn hoặc case đến giờ chưa xử lý.
- Reconciliation chạy lại an toàn sau restart, có checkpoint/window và unique constraints; không dựa vào timer chỉ chạy trong một instance. Nếu nhiều API instance, outbox/job phải có cơ chế claim/lease hoặc một worker duy nhất để không phát thông báo/hoàn tiền trùng.
- Rollout: migration staging -> seed `LaneSlotCapacity` và calibration -> chạy read-only preview trên dữ liệu thật đã ẩn PII -> bật flag một branch -> quan sát ít nhất một vòng sự cố giả lập/tình huống test -> mở rộng. Có rollback plan: tắt tạo incident mới, giữ worker xử lý case hiện hữu, không drop bảng hoặc revert migration dữ liệu tài chính. Không chạy thao tác production khi chưa được người dùng/ops phê duyệt.
