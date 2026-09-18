# Incident API: các điểm backend cần hoàn thiện trước khi mở đầy đủ UI

Đối chiếu ngày 18/09/2026 với `SmartWash-BE` và `Docs/9-18/walkthrough.md`. Đây là ghi chú tích hợp, không phải thay đổi backend.

## API đang có và web đã tích hợp

| Tác vụ | Endpoint hiện tại | Ghi chú |
| --- | --- | --- |
| Xem trước | `POST /api/manager/incidents/preview` | Body `branchId`, `type`, `scope`, `laneIds`, `estimatedEndAtVn`; response `{success,data:{affectedBookingsCount,totalCapacityLoss,affectedBookings}}`. |
| Tạo | `POST /api/manager/incidents` | Thêm `reason`; response `{success,incidentId}`. |
| Gia hạn | `PUT /api/manager/incidents/{id}/extend` | Body `newEstimatedEndAtVn`, `note`. Web chưa mở thao tác vì chưa xử lý booking mới ảnh hưởng. |
| Khắc phục | `POST /api/manager/incidents/{id}/resolve` | Không có body. Web chỉ cho thao tác trên incident vừa tạo trong phiên. |

Lưu ý: route này **không có `/v1`**, khác hầu hết API của dự án. DTO dùng `DateTime` và service so với `TimeHelper.VnNow` (VN wall-clock `DateTime`), nên web gửi `YYYY-MM-DDTHH:mm:ss` không kèm offset. Nên thống nhất lại contract thời gian bằng `DateTimeOffset` hoặc chuyển đổi rõ ở backend.

## Các blocker cần backend xử lý

1. **Không có GET list/detail/impact/case.** `IncidentController` chỉ khai báo bốn action ở trên. Manager không thể xem sự cố đã tạo sau khi tải lại trang, theo dõi khách phản hồi, xem notification fail, hay chọn incident cũ để khắc phục. Cần API phân trang danh sách, chi tiết và case/impact như kế hoạch.
2. **Preview cho `SelectedLanes` chưa mô phỏng lane sắp hỏng.** `IncidentService.PreviewIncidentImpactAsync` gọi `GetEffectiveSlotCapacityAsync` trước khi incident tồn tại; `TotalCapacityLoss` cũng chỉ được tính cho `WholeBranch`. Preview có thể báo 0 trong khi lúc tạo lại phát sinh case. Cần tính cùng thuật toán với create, và trả slot/công suất trước-sau.
3. **Không có preview version/revalidation khi tạo.** DTO không có `previewVersion`; số lượng booking có thể thay đổi giữa preview và create. Cần conflict `409` và preview mới khi dữ liệu thay đổi.
4. **Gia hạn chưa xử lý booking mới.** `IncidentService.ExtendIncidentAsync` chỉ cập nhật ETA và có comment `Handle newly affected bookings if extended... Omitted for brevity`. Nếu mở nút gia hạn hiện tại, lịch mới không có case/thông báo. Cần extend-preview, xử lý thêm case/outbox trong transaction và version check trước khi web có thể bật thao tác.
5. **Thiếu ràng buộc chi nhánh/lane với Manager.** `managerUserId` không được dùng để kiểm tra `request.BranchId`; create không kiểm tra `LaneIds` thuộc chi nhánh; extend/resolve tìm incident theo ID mà không đối chiếu chi nhánh Manager. Web lấy branch ID từ session nhưng không thể thay cho authorization ở server. Cần scope toàn bộ action theo branch của JWT/profile.
6. **Resolve đóng toàn bộ case AwaitingCustomer thành Kept.** `ResolveIncidentAsync` không kiểm tra công suất đã phục hồi hay lịch còn khả thi. Cần re-evaluate từng case và dùng policy Keep/Cancel theo kế hoạch.
7. **Mobile chưa có cách lấy case/alternatives.** `IncidentActionController` chỉ có POST decision bằng `affectedBookingId`, nhưng outbox `INCIDENT_ACTION_REQUIRED` chỉ chứa `BookingId`/`IncidentId`; không có GET case/booking options để lấy `affectedBookingId` hoặc danh sách branch/slot thay thế. Không thể làm mobile flow Cancel/Transfer an toàn chỉ từ payload push hiện có.
8. **Transfer không kiểm tra chỗ trống đích.** `IncidentCustomerService.HandleTransferDecisionAsync` chỉ kiểm tra slot thuộc branch rồi đổi `Booking.BranchId`/`ScheduledTime`; không đặt giữ chỗ hoặc kiểm tra capacity, và có thể tạo overbooking. Cần transaction/lock/revalidation đích và idempotency.

Ngoài ra nên thống nhất route `/api/v1/...`, response wrapper/errorCode và DTO với `implementation_plan2.md` trước khi mở UI quản lý đầy đủ; thông báo đẩy thành công không đồng nghĩa khách đã đọc hoặc quyết định.
