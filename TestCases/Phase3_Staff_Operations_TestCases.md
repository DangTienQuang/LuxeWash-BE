# Deep Dive: Staff & Operations - Exhaustive Test Cases

This document provides a highly detailed, exhaustive list of test cases for the Phase 3 API group (`OperationStaffController`, `StaffBookingsController`, `StaffSelfServiceController`, `StaffMaterialUsageController`, `StaffVouchersController`). It focuses strictly on State Machine transitions, Priority Queue algorithms, Inventory deductions, and HR workflow validations.

---

## 1. OperationStaffController (Active Wash Floor Operations)

### 1.1. GET /api/v1/operation-staff/lane-assignment
- **TC01 (Happy Path):** Authenticated staff member retrieves their specific lane assignment for today.
  - *Expect:* HTTP 200, returns active `LaneId` and `LaneName`.
- **TC02 (Happy Path - Floater Staff):** Staff is assigned as a "Floater" (unbound to a specific lane).
  - *Expect:* HTTP 200, returns `LaneId = 0` (All Lanes).
- **TC03 (Logic - No Shift Today):** Staff attempts to retrieve lane assignment on their day off.
  - *Expect:* HTTP 404, "You are not scheduled to work today".
- **TC04 (Security - Banned):** Suspended staff attempts to view lane assignment.
  - *Expect:* HTTP 403 Forbidden.

### 1.2. GET /api/v1/operation-staff/tasks (Priority Wash Queue)
- **TC05 (Algorithm - Priority Sort):** Retrieve the active queue of vehicles currently `CheckedIn` or `Processing`.
  - *Expect:* HTTP 200. The array MUST be strictly sorted by Customer Tier (Diamond > Gold > Silver > Walk-in), then by `CheckedInTime` (FIFO).
- **TC06 (Data Isolation):** Staff at Branch A retrieves the task list.
  - *Expect:* HTTP 200, list ONLY contains vehicles at Branch A. Branch B vehicles must be entirely hidden.
- **TC07 (Logic - Empty Queue):** Retrieve tasks when the branch has no active vehicles.
  - *Expect:* HTTP 200, returns `[]`.

### 1.3. PUT /api/v1/operation-staff/bookings/{bookingId}/status (Wash State Machine)
- **TC08 (State - Happy Path):** Update booking from `CheckedIn` to `Processing`.
  - *Expect:* HTTP 200, `ProcessingStartTime` is stamped in the DB.
- **TC09 (State - Happy Path):** Update booking from `Processing` to `Completed`.
  - *Expect:* HTTP 200, `CompletedTime` is stamped. System fires background event to deduct standard inventory materials (e.g., 50ml Shampoo).
- **TC10 (State - Invalid Transition):** Attempt to update booking from `Pending` directly to `Completed`.
  - *Expect:* HTTP 400, "Invalid status transition. Vehicle must be CheckedIn and Processing first".
- **TC11 (State - Irreversible):** Attempt to update booking from `Completed` back to `Processing`.
  - *Expect:* HTTP 400, "Cannot revert a completed wash".
- **TC12 (Concurrency - Race Condition):** Two staff members simultaneously send the request to transition the same vehicle to `Completed`.
  - *Expect:* Database lock ensures only 1 request succeeds, preventing double inventory deduction.
- **TC13 (Security - RBAC):** Customer attempts to call this API to mark their own car as `Completed`.
  - *Expect:* HTTP 403 Forbidden.

## 2. StaffBookingsController (Advanced Floor Management)

### 2.1. GET /api/v1/staff-bookings/by-license-plate/{licensePlate} (LPR / Manual Search)
- **TC14 (Happy Path - Pre-booked):** Search a plate that has a `Pending` booking for today.
  - *Expect:* HTTP 200, returns the booking details ready for check-in.
- **TC15 (Happy Path - Walk-in Customer):** Search a plate that has no booking today, but exists in the CRM.
  - *Expect:* HTTP 200, returns customer profile (Name, Tier) to greet them properly and quickly create a walk-in order.
- **TC16 (Negative - New Vehicle):** Search a plate never seen before.
  - *Expect:* HTTP 404, "Vehicle not found in database".
- **TC17 (Security - SQLi):** Inject SQL into the license plate search string.
  - *Expect:* HTTP 400 or handled safely via parameterized queries.

### 2.2. PUT /api/v1/staff-bookings/{id}/no-show (Mark as No-Show)
- **TC18 (Policy - Happy Path):** Customer is 30 minutes late past their TimeSlot. Staff marks as `NoShow`.
  - *Expect:* HTTP 200, slot capacity is freed. Penalty logic (if any) applied to customer profile.
- **TC19 (Policy - Premature):** Customer is only 5 minutes late. Staff attempts to mark as `NoShow`.
  - *Expect:* HTTP 400, "Cannot mark as No-Show before the grace period (30 mins) expires".
- **TC20 (Policy - Processing):** Attempt to mark a vehicle currently `Processing` as `NoShow`.
  - *Expect:* HTTP 400, "Cannot mark an actively washing vehicle as No-Show".

### 2.3. PUT /api/v1/staff-bookings/{detailId}/report-mismatch (Report Vehicle/Service Mismatch)
- **TC21 (Happy Path):** Customer booked a Sedan wash, but arrived in an SUV. Staff reports mismatch.
  - *Expect:* HTTP 200, booking flagged. System automatically recalculates the `FinalAmount` based on SUV pricing.
- **TC22 (Logic - Completed):** Attempt to report a mismatch on a `Completed` booking.
  - *Expect:* HTTP 400, "Cannot report mismatch on a completed wash".

### 2.4. POST /api/v1/staff-bookings/force-cancel (Force Cancel Booking)
- **TC23 (Happy Path - Equipment Failure):** Wash tunnel breaks down. Staff force-cancels a `CheckedIn` booking.
  - *Expect:* HTTP 200, booking cancelled, 100% refund of points/vouchers issued instantly. Customer receives SMS apology.
- **TC24 (Security - Abuse Limit):** Staff attempts to force-cancel 10 bookings in a row without Manager override.
  - *Expect:* HTTP 403, "Maximum force-cancel limit reached. Manager authorization required".

## 3. StaffSelfServiceController (HR & Rostering)

### 3.1. POST /api/v1/staff-self-service/overtime-requests (Submit Overtime)
- **TC25 (Happy Path):** Submit a request for 2 hours overtime on a valid workday.
  - *Expect:* HTTP 200, request created with `Pending` status.
- **TC26 (Policy - Max Hours):** Submit a request for 8 hours overtime in a single day.
  - *Expect:* HTTP 400, "Overtime request exceeds the maximum legal limit of 4 hours per day".
- **TC27 (Policy - Past Date):** Submit overtime request for a date in the past.
  - *Expect:* HTTP 400, "Cannot request overtime for past dates".
- **TC28 (Concurrency):** Submit the exact same overtime request multiple times.
  - *Expect:* HTTP 409, "You already have a pending overtime request for this date".

### 3.2. POST /api/v1/staff-self-service/shift-swap-requests (Shift Swap)
- **TC29 (Happy Path):** Request to swap a Tuesday morning shift with Colleague B's Wednesday morning shift.
  - *Expect:* HTTP 200, swap request created and pushed to Colleague B for consent.
- **TC30 (Logic - Invalid Target):** Request to swap a shift with a colleague who is already on leave that day.
  - *Expect:* HTTP 400, "Target colleague is not scheduled for a shift on that date".
- **TC31 (Logic - Same Day):** Request to swap a morning shift with an afternoon shift on the SAME day with the same person.
  - *Expect:* HTTP 200, allowed (if policy permits double shifts).

## 4. StaffMaterialUsageController (Inventory Consumption)

### 4.1. POST /api/v1/staff-material-usage/bookings/{bookingId}/extra (Request Extra Materials)
- **TC32 (Happy Path):** Vehicle is extremely dirty. Staff requests +20ml extra chemical soap for Booking ID 123.
  - *Expect:* HTTP 200, request logged as `PendingManagerApproval` (or auto-approved based on threshold).
- **TC33 (Policy - Stock Out):** Request extra material that is currently at 0 inventory.
  - *Expect:* HTTP 400, "Insufficient branch stock for this material".
- **TC34 (Security - Invalid Booking):** Request extra material for a booking belonging to a different branch.
  - *Expect:* HTTP 403, "Booking does not belong to your branch".

## 5. StaffVouchersController (Manual QR Scans)

### 5.1. POST /api/v1/staff-vouchers/consume (Consume Physical/QR Voucher)
- **TC35 (Happy Path):** Staff scans a valid, active QR voucher presented by a walk-in customer.
  - *Expect:* HTTP 200, voucher validated, marked as used, discount applied to POS order.
- **TC36 (Negative - Expired):** Scan a QR voucher that expired yesterday.
  - *Expect:* HTTP 400, "This voucher has expired".
- **TC37 (Negative - Already Used):** Scan a QR voucher that was already consumed by another customer.
  - *Expect:* HTTP 400, "This voucher has already been consumed".
- **TC38 (Policy - Branch Mismatch):** Scan a voucher strictly bound to Branch A while working at Branch B.
  - *Expect:* HTTP 400, "This voucher is not applicable at this branch".
- **TC39 (Concurrency - Double Scan):** Staff accidentally scans the same QR code twice rapidly.
  - *Expect:* HTTP 200 on first scan, HTTP 400 "Already consumed" on the second scan (Database transaction ensures atomic consumption).
# Ultra Deep Dive: Staff & Operations - +100 Edge Cases

This document provides an additional 100 exhaustive test cases for the Phase 3 API group (`OperationStaffController`, `StaffBookingsController`, `StaffSelfServiceController`, `StaffMaterialUsageController`, `StaffVouchersController`), strictly focusing on Boundary Limits, SQL Injection, State Reversals, and Extreme Edge Cases.

---

## 1. OperationStaffController (Active Wash Floor Operations)

### 1.1. GET /api/v1/operation-staff/lane-assignment
- **TC40 (Boundary):** Request lane assignment at exactly 23:59:59 (Before shift rolls over).
- **TC41 (Boundary):** Request lane assignment at exactly 00:00:01 (New day shift).
- **TC42 (Security):** Token contains invalid claims for `StaffId`.
- **TC43 (Logic):** Staff assigned to a lane that was just physically deleted by Admin.
- **TC44 (Performance):** Load test: 500 staff members requesting lane assignments simultaneously at 07:59 AM.

### 1.2. GET /api/v1/operation-staff/tasks
- **TC45 (Pagination Limit):** Active tasks exceed 100 vehicles. Ensure response is properly paginated.
- **TC46 (Data Integrity):** Verify `TimeInQueue` metric updates correctly based on server clock, not client clock.
- **TC47 (Sorting):** Two Diamond tier customers check in at the exact same millisecond. Verify secondary sort logic (by booking ID).
- **TC48 (Filter):** Attempt to inject SQL into the status filter query `?status=Processing' OR 1=1--`.
- **TC49 (State):** Verify vehicles marked as `NoShow` do not appear in the active tasks list.

### 1.3. POST /api/v1/operation-staff/lanes/swap
- **TC50 (Logic):** Swap to a lane that is already at maximum staff capacity (e.g., 3 washers).
- **TC51 (Security):** Attempt to swap to a lane ID belonging to a different branch.
- **TC52 (State):** Attempt to swap lanes while actively `Processing` a vehicle. (Should be blocked).
- **TC53 (Idempotency):** Swap to the exact same lane you are already assigned to.
- **TC54 (Validation):** Pass null or empty lane ID payload.

### 1.4. POST /api/v1/operation-staff/bookings/{bookingId}/checkin
- **TC55 (Boundary):** Check-in a vehicle exactly 5 seconds before the TimeSlot ends.
- **TC56 (Boundary):** Check-in a vehicle exactly at the end of the 30-minute grace period.
- **TC57 (Concurrency):** Staff A and Staff B check in the same vehicle simultaneously on two iPads.
- **TC58 (State):** Check-in a booking that is currently marked as `Unpaid` (Cash not collected yet).
- **TC59 (Security):** Inject negative booking ID (e.g., `-1`).

### 1.5. PUT /api/v1/operation-staff/bookings/{bookingId}/status
- **TC60 (Validation):** Pass empty status string in payload.
- **TC61 (Validation):** Pass invalid status string (`"SUPER_WASHING"`).
- **TC62 (Concurrency):** Change status to `Completed` while another staff is adding extra materials to the bill.
- **TC63 (Logic):** Change status to `Processing` when the physical lane sensor detects no vehicle (Hardware lock).
- **TC64 (Security):** Update status for a booking belonging to another branch.

---

## 2. StaffBookingsController (Advanced Floor Management)

### 2.1. GET /api/v1/staff-bookings
- **TC65 (Filter):** Filter bookings by `TargetDate` = 365 days in the past.
- **TC66 (Filter):** Filter bookings by `TargetDate` = 365 days in the future.
- **TC67 (Performance):** Request page 1 with size 1000. Ensure maximum page size constraint (e.g., 50) is enforced.
- **TC68 (Security):** Inject NoSQL/SQL payload in the date filter.
- **TC69 (Data Leak):** Ensure customer's encrypted passwords or sensitive payment hashes are not exposed in the booking payload.

### 2.2. PUT /api/v1/staff-bookings/{id}/status (Force Status)
- **TC70 (Logic):** Force status to `Completed` for an `Unpaid` booking. Ensure payment validation is strictly enforced or explicit Manager override is logged.
- **TC71 (Audit):** Force status update must log the specific Staff ID who performed the action in the `AuditLogs` table.
- **TC72 (State):** Force status to `Pending` for a booking that was already `Completed` 5 days ago.

### 2.3. PUT /api/v1/staff-bookings/status-by-license-plate
- **TC73 (Conflict):** License plate currently has 2 active bookings (System anomaly). System must throw HTTP 409 Conflict demanding manual resolution.
- **TC74 (Validation):** Pass plate string with emojis.
- **TC75 (Logic):** Plate exists but booking is for tomorrow. Ensure it is not accidentally updated today.

### 2.4. GET /api/v1/staff-bookings/by-license-plate/{licensePlate}
- **TC76 (Boundary):** Search plate with exact match of 15 characters (Max length).
- **TC77 (Formatting):** Search plate with spaces (`51G 12345`) vs without spaces (`51G12345`). System must normalize input.
- **TC78 (Logic):** Vehicle has been soft-deleted by user. Should staff still see the history if they walk in?

### 2.5. PUT /api/v1/staff-bookings/{id}/no-show
- **TC79 (Concurrency):** Customer cancels via app at the exact same millisecond staff marks as No-Show.
- **TC80 (Audit):** No-show must release the slot capacity immediately in Redis/DB.
- **TC81 (Logic):** Mark as No-show for a corporate fleet booking. Ensure billing logic handles penalties correctly.

### 2.6. PUT /api/v1/staff-bookings/{detailId}/report-mismatch
- **TC82 (Validation):** Report mismatch with empty reason string.
- **TC83 (Validation):** Report mismatch with reason > 1000 characters.
- **TC84 (Logic):** Mismatch results in a cheaper service. Ensure difference is credited to user's wallet.
- **TC85 (Logic):** Mismatch results in a more expensive service. Ensure booking status changes to `Unpaid_Differential`.

### 2.7. POST /api/v1/staff-bookings/force-cancel
- **TC86 (Notification):** Force cancel triggers push notification and SMS to customer instantly.
- **TC87 (Logic):** Force cancel a booking that was paid via PayOS. System must initiate a background refund API call to the payment gateway.
- **TC88 (Logic):** Refund gateway fails. System must retry 3 times or flag for manual refund.

---

## 3. StaffSelfServiceController (HR)

### 3.1. GET /api/v1/staff-self-service/shifts
- **TC89 (Boundary):** Request shifts for a leap year (Feb 29).
- **TC90 (Empty):** Request shifts for a month where staff is on unpaid leave (Returns `[]`).

### 3.2. POST /api/v1/staff-self-service/overtime-requests
- **TC91 (Validation):** Start time > End time.
- **TC92 (Validation):** Overlap with standard shift hours.
- **TC93 (Boundary):** Request exactly 1 minute of overtime.

### 3.3. GET & POST /api/v1/staff-self-service/shift-swap-requests
- **TC94 (Validation):** Target staff ID does not exist.
- **TC95 (Logic):** Target staff already has an approved swap on that exact day.
- **TC96 (State):** Cancel a swap request before target staff accepts it.
- **TC97 (State):** Cancel a swap request AFTER target staff accepts it but before Manager approves.

---

## 4. StaffMaterialUsageController (Inventory)

### 4.1. POST /api/v1/staff-material-usage/bookings/{bookingId}/extra
- **TC98 (Boundary):** Request 0.01 ml of extra material.
- **TC99 (Boundary):** Request 9999 liters of extra material.
- **TC100 (Concurrency):** Staff A and Staff B request extra material for the same car simultaneously.
- **TC101 (Logic):** Request extra material for a booking that was already `Completed` yesterday.

---

## 5. StaffVouchersController

### 5.1. POST /api/v1/staff-vouchers/consume
- **TC102 (Security):** Scan QR code containing SQL injection payload (`{"code": "' OR 1=1--"}`).
- **TC103 (Logic):** Consume a voucher that requires Minimum Order Value of 500k, but current POS order is 100k.
- **TC104 (Logic):** Consume a voucher that is restricted to "Diamond Tier" for a "Walk-in" customer.
- **TC105 (Boundary):** Consume voucher at 23:59:59 on its expiration date.
- **TC106 (Boundary):** Consume voucher at 00:00:01 after its expiration date (Should fail).
- **TC107 (State):** Attempt to consume a globally deactivated campaign voucher.
- **TC108 (Idempotency):** Send the identical consume request 10 times in 1 second. System must only apply the discount once to the bill.
- **TC109 (Math):** Apply a 100% discount voucher. Ensure FinalAmount is exactly 0 and no payment gateway URL is generated.
- **TC110 (Math):** Apply a fixed 50k discount voucher on a 30k service. FinalAmount must be 0, not -20k.
- **TC111 (Security):** Tamper with the QR code signature to forge a non-existent campaign.
- **TC112 (Data Leak):** Ensure API response does not expose the internal Campaign Budget remaining to the Staff POS.
- **TC113 (Logic):** User applies a voucher, but then the wash is Force-Cancelled. The voucher MUST be refunded to `remainingUses` count.
- **TC114 (Validation):** Pass empty code string.
- **TC115 (Validation):** Pass extremely long string (10,000 chars) to cause Buffer Overflow or regex DoS.
- **TC116 (Performance):** 100 branches scanning vouchers simultaneously on a holiday. API must respond in < 200ms using Redis caching.
- **TC117 (Policy):** Voucher is restricted to "Morning Shifts" (08:00 - 12:00), but staff scans it at 13:00.
- **TC118 (Policy):** Customer attempts to stack 2 vouchers (System must reject if `IsStackable = false`).
- **TC119 (Policy):** Customer attempts to stack 2 vouchers (System must accept if `IsStackable = true`, but cap discount at 100%).
- **TC120 (Security):** Staff attempts to use a customer's personal loyalty voucher on a Walk-in cash order to pocket the difference. System must verify Voucher Owner ID == Booking Customer ID.
- **TC121 (Logic):** Apply voucher to a Corporate Fleet booking (Should fail, B2B billing does not use retail vouchers).
- **TC122 (Audit):** Consume API must log the Staff ID, Branch ID, POS Terminal ID, and Timestamp for fraud detection.
- **TC123 (State):** Attempt to consume a voucher on an order that is already `Paid`.
- **TC124 (State):** Attempt to consume a voucher on an order that is already `Completed`.
- **TC125 (Integration):** If POS goes offline, test offline-sync mechanics for QR codes (if supported).
- **TC126 (Logic):** Customer uses a "Free Ceramic Coating" voucher. System must automatically append the "Ceramic Coating" Service ID to the booking details, bypassing standard payment.
- **TC127 (Logic):** Consume a voucher that targets a specific vehicle type (e.g., "SUV Only") on a Sedan booking. System must reject.
- **TC128 (Logic):** Consume a voucher that targets a specific branch (Branch A) while scanning at Branch B. System must reject.
- **TC129 (Math - Float Precision):** Apply a 33.33% discount on a 199,999 VND order. Verify rounding logic (e.g., round to nearest 1,000 VND).
- **TC130 (Logic):** Customer presents a screenshot of a dynamic QR code. System must reject if the TOTP (Time-Based One-Time Password) embedded in the QR has expired.
- **TC131 (Logic):** System gracefully handles database disconnect during consumption (Transaction rollback).
- **TC132 (Security):** Brute-force guessing 6-character voucher codes via API. System must trigger Rate Limit after 5 failed attempts.
- **TC133 (Security):** Rate Limit block must expire automatically after 15 minutes.
- **TC134 (Security):** Manager override for a Rate Limited POS terminal.
- **TC135 (Logic):** Consume a "First-time Customer" voucher for a user who already has 1 Completed wash in history.
- **TC136 (Logic):** Consume a "Win-back" voucher for a user who washed their car 2 days ago (Requires > 30 days inactivity).
- **TC137 (Logic):** Consume a voucher where the associated Campaign has run out of its Global Budget (e.g., max 50,000,000 VND total discount).
- **TC138 (Security):** Block concurrent redemption of the same voucher code across two different branches simultaneously.
- **TC139 (Logic):** Verify `ConsumedAt` timestamp accurately reflects server time, completely ignoring client POS time.
# Phase 3: Staff & Operations - Test Cases

This document defines the test cases for the Staff Operations API group, designed to handle employee workflows, shift management, and vehicle processing on the workshop floor.

---

## 1. OperationStaffController (Active Shift Operations)

### 1.1. GET /api/v1/operation-staff/lane-assignment (Get Staff Lane Assignment)
- **TC01 (Happy Path - Assigned):** Authenticated staff requests their lane assignment for today.
  - *Expect:* HTTP 200, returns the specific LaneId and LaneName assigned to the staff.
- **TC02 (Happy Path - Unassigned / Flexible):** Staff is not bound to a specific lane today.
  - *Expect:* HTTP 200, returns a default object representing "All Lanes" (LaneId = 0) allowing flexible operations across the branch.

### 1.2. GET /api/v1/operation-staff/tasks (Get Active Tasks / Priority Queue)
- **TC01 (Happy Path):** Retrieve the list of active bookings (CheckedIn/Processing) currently in the branch.
  - *Expect:* HTTP 200, returns array of tasks correctly sorted by priority (Diamond -> Gold -> Silver -> Bronze -> Walk-in).
- **TC02 (Empty Queue):** No active vehicles currently in the branch.
  - *Expect:* HTTP 200, returns an empty array [].

### 1.3. POST /api/v1/operation-staff/lanes/swap (Request Lane Swap)
- **TC01 (Happy Path):** Staff requests to temporarily swap to another active lane.
  - *Expect:* HTTP 200, lane assignment updated for the current session.
- **TC02 (Negative):** Attempt to swap to a lane that is currently offline or disabled.
  - *Expect:* HTTP 400, "The requested lane is currently inactive".

### 1.4. POST /api/v1/operation-staff/bookings/{bookingId}/checkin (Staff Check-in Vehicle)
- **TC01 (Happy Path):** Staff manually checks in a pending vehicle entering the facility.
  - *Expect:* HTTP 200, booking status changed from Pending to CheckedIn.
- **TC02 (Negative):** Attempt to check in a booking that is already Cancelled or Completed.
  - *Expect:* HTTP 400, "Cannot check in a vehicle with this booking status".

### 1.5. PUT /api/v1/operation-staff/bookings/{bookingId}/status (Update Vehicle Wash Status)
- **TC01 (Happy Path - Start Processing):** Update status to Processing.
  - *Expect:* HTTP 200, status updated, ProcessingStartTime is recorded.
- **TC02 (Happy Path - Completed):** Any staff updates status to Completed.
  - *Expect:* HTTP 200, status updated, CompletedTime is recorded, materials are deducted, and the API logs the staff member who finished the wash.
- **TC03 (Negative):** Update status to Completed before it was CheckedIn or Processing.
  - *Expect:* HTTP 400, "Invalid status transition".

---

## 2. StaffBookingsController (Detailed Booking Management)

### 2.1. GET /api/v1/staff-bookings (List Branch Bookings)
- **TC01 (Happy Path):** Staff retrieves all bookings for their branch today.
  - *Expect:* HTTP 200, returns paginated list of bookings.
- **TC02 (Security):** Staff attempts to view bookings from a different branch.
  - *Expect:* HTTP 403, "You do not have permission to view bookings for this branch".

### 2.2. PUT /api/v1/staff-bookings/{id}/status (Change Booking Status)
- **TC01 (Happy Path):** Manager/Staff forcefully updates a booking status due to an operational edge case.
  - *Expect:* HTTP 200, status successfully updated.

### 2.3. GET /api/v1/staff-bookings/by-license-plate/{licensePlate} (Lookup Booking by Plate)
- **TC01 (Happy Path - Pre-booked):** Scan a license plate that has an active booking today.
  - *Expect:* HTTP 200, returns the booking details.
- **TC02 (Happy Path - Walk-in):** Scan a license plate that has no booking but exists in the customer database.
  - *Expect:* HTTP 200, returns the customer profile to facilitate creating a walk-in order.
- **TC03 (Not Found):** Scan a completely new vehicle.
  - *Expect:* HTTP 404 (or 200 with null payload), indicating a brand new customer.

### 2.4. PUT /api/v1/staff-bookings/{id}/no-show (Mark as No-Show)
- **TC01 (Happy Path):** Customer fails to arrive 30 minutes past the scheduled time; staff marks as No-Show.
  - *Expect:* HTTP 200, booking status changed to NoShow, capacity freed, potential penalty points applied to customer.
- **TC02 (Negative):** Attempt to mark as No-Show before the scheduled time has passed.
  - *Expect:* HTTP 400, "Cannot mark as No-Show before the scheduled time".

### 2.5. PUT /api/v1/staff-bookings/{detailId}/report-mismatch (Report Vehicle Mismatch)
- **TC01 (Happy Path):** Staff reports that the vehicle arrived is an SUV instead of the booked Sedan.
  - *Expect:* HTTP 200, mismatch reported, booking flagged for Manager review or price adjustment.

### 2.6. POST /api/v1/staff-bookings/force-cancel (Force Cancel Booking)
- **TC01 (Happy Path):** Staff forcefully cancels a booking due to facility issues (e.g., equipment breakdown).
  - *Expect:* HTTP 200, booking cancelled, full refund of points/vouchers issued automatically.

---

## 3. StaffSelfServiceController (HR & Shift Management)

### 3.1. GET /api/v1/staff-self-service/shifts (Get My Shifts)
- **TC01 (Happy Path):** Staff views their scheduled shifts for the upcoming week.
  - *Expect:* HTTP 200, returns array of shift objects (date, startTime, endTime).

### 3.2. POST /api/v1/staff-self-service/overtime-requests (Submit Overtime Request)
- **TC01 (Happy Path):** Staff submits a request to work 2 hours overtime on a specific date.
  - *Expect:* HTTP 200, request created with status Pending approval by Manager.
- **TC02 (Negative):** Request overtime exceeding the legal/company maximum daily limit (e.g., > 4 hours).
  - *Expect:* HTTP 400, "Overtime request exceeds the maximum allowed hours".

### 3.3. POST /api/v1/staff-self-service/shift-swap-requests (Submit Shift Swap Request)
- **TC01 (Happy Path):** Staff requests to swap their Tuesday shift with a colleague's Wednesday shift.
  - *Expect:* HTTP 200, request created and sent to the colleague and Manager for approval.

---

## 4. StaffMaterialUsageController (Inventory Operations)

### 4.1. POST /api/v1/staff-material-usage/bookings/{bookingId}/extra (Request Extra Materials)
- **TC01 (Happy Path):** A vehicle is extremely dirty, staff requests extra shampoo beyond the standard allocation.
  - *Expect:* HTTP 200, extra usage logged and forwarded to Manager for inventory approval.
- **TC02 (Negative):** Request an invalid or out-of-stock material.
  - *Expect:* HTTP 400, "Requested material is out of stock".

---

## 5. StaffVouchersController (Manual Voucher Processing)

### 5.1. POST /api/v1/staff-vouchers/consume (Consume Physical/QR Voucher)
- **TC01 (Happy Path):** Staff scans a valid QR voucher code at the counter.
  - *Expect:* HTTP 200, voucher validated, marked as used, and discount applied to the current order.
- **TC02 (Negative):** Scan an expired or already used voucher code.
  - *Expect:* HTTP 400, "Voucher is invalid or has already been consumed".
