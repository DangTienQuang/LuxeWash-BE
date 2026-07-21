# Deep Dive: Manager & Inventory - Exhaustive Test Cases

This document provides a highly detailed, exhaustive list of test cases for the Phase 4 API group (`ManagerController`, `ManagerInventoryController`). It focuses strictly on AI Revenue Stimulus logic, Branch Load Balancing (Proactive Relocation), and Inventory Stock management.

---

## 1. ManagerController (Branch AI & Lane Operations)

### 1.1. POST /api/v1/manager/revenue-stimulus/comprehensive-proposals (AI Revenue Stimulus)
- **TC01 (AI Logic - Revenue Drop):** Branch revenue for the last 7 days dropped by > 15% compared to the baseline.
  - *Expect:* HTTP 200, system automatically generates and returns 2 voucher proposals (e.g., "Off-peak Discount" and "Win-back Campaign") with status `Proposed`.
- **TC02 (AI Logic - Revenue Stable):** Branch revenue is stable or increasing.
  - *Expect:* HTTP 200, system indicates no stimulus is needed; returns `[]` (no proposals created).
- **TC03 (Security - RBAC):** Call API with a regular Customer or Staff token.
  - *Expect:* HTTP 403 Forbidden, "Access denied. Manager role required".
- **TC04 (Concurrency - Idempotency):** Manager frantically clicks "Generate Proposal" 5 times in a row.
  - *Expect:* HTTP 200, but DB strictly ensures only ONE set of active proposals is generated per week to avoid spamming customers.

### 1.2. POST /api/v1/manager/revenue-stimulus/proposals/{voucherId}/approve (Approve AI Voucher)
- **TC05 (Happy Path):** Manager approves a valid `Proposed` voucher.
  - *Expect:* HTTP 200, voucher status changes to `Approved`. The system fires a background background job (Kafka/RabbitMQ) to distribute the voucher to targeted customer wallets.
- **TC06 (State - Invalid Transition):** Attempt to approve a voucher that is already `Approved` or `Rejected`.
  - *Expect:* HTTP 400, "Invalid voucher state for approval".
- **TC07 (State - Missing Data):** Approve a voucher proposal that has an empty targeted user list (0 users).
  - *Expect:* HTTP 400, "Cannot approve a campaign with zero targeted users".

### 1.3. POST /api/v1/manager/branch-overload/scan-and-notify-relocation (Proactive Relocation AI)
- **TC08 (Logic - Overload Detected):** Branch capacity for the next 2 hours exceeds the 80% threshold.
  - *Expect:* HTTP 200, API returns a list of `Pending` bookings that were automatically sent Push Notifications (FCM/APNS) suggesting relocation to a nearby branch.
- **TC09 (Logic - Normal Load):** Branch capacity is at 50% (Normal).
  - *Expect:* HTTP 200, returns an empty list, no notifications sent.
- **TC10 (Security - Rate Limiting):** Run the scan API continuously every minute.
  - *Expect:* HTTP 429 (or logic block), "Relocation scan can only be manually triggered once every 30 minutes" (to prevent spamming users).

### 1.4. POST /api/v1/manager/lanes/assign-staff (Assign Staff to Lane)
- **TC11 (Happy Path):** Assign Staff A to Lane 1 for the current day.
  - *Expect:* HTTP 200, assignment record created.
- **TC12 (Policy - Not Scheduled):** Assign a staff member who is currently on Leave or not scheduled to work today.
  - *Expect:* HTTP 400, "Staff member is not scheduled to work today".
- **TC13 (Policy - Double Booking):** Assign Staff A to Lane 1, and then assign Staff A to Lane 2 concurrently.
  - *Expect:* HTTP 200, system automatically removes Staff A from Lane 1 and assigns them to Lane 2 (Staff can only manage one primary lane at a time).

### 1.5. POST /api/v1/manager/bookings/{bookingId}/checkin-assign (Force Check-in & Assign)
- **TC14 (Happy Path):** Manager forcefully checks in a VIP vehicle and explicitly routes it to Lane 2 bypassing standard queue logic.
  - *Expect:* HTTP 200, booking status becomes `CheckedIn` and `AssignedLaneId` is updated to 2.

---

## 2. ManagerInventoryController (Branch Inventory Management)

### 2.1. POST /api/v1/manager/inventory/imports (Import Materials / Receive PO)
- **TC15 (Happy Path):** Manager imports 100 bottles of "Ceramic Coating" (Material ID: 12).
  - *Expect:* HTTP 200, stock level for Material ID 12 increases by 100. A `GoodsReceipt` transaction is logged for auditing.
- **TC16 (Validation - Negative Amount):** Manager inputs -50 for the import quantity.
  - *Expect:* HTTP 400, "Import quantity must be greater than zero".
- **TC17 (Validation - Invalid Material):** Manager attempts to import an item ID that does not exist in the Master Catalog.
  - *Expect:* HTTP 404, "Material not found in global catalog".

### 2.2. POST /api/v1/manager/inventory/batches/{id}/discard (Discard Expired Stock)
- **TC18 (Happy Path):** Manager discards a batch of chemical soap that has expired.
  - *Expect:* HTTP 200, stock level decreases. Transaction logged as `Discarded` with the manager's ID.
- **TC19 (Logic - Exceeds Stock):** Manager attempts to discard 50 units from a batch that only has 10 units left.
  - *Expect:* HTTP 400, "Discard quantity exceeds available stock in this batch".

### 2.3. GET /api/v1/manager/inventory/expiring-soon (Stock Expiration Alerts)
- **TC20 (Logic - Date Filter):** Request list of items expiring soon.
  - *Expect:* HTTP 200, API strictly returns only batches where `ExpirationDate` is within the next 30 days.

### 2.4. GET /api/v1/manager/inventory/reports/profit (Branch Profitability Report)
- **TC21 (Data Integrity - Math Validation):** Generate profit report for the last month.
  - *Expect:* HTTP 200. The `TotalProfit` MUST mathematically equal exactly `Total Revenue (from all Paid Bookings)` MINUS `Total COGS (Cost of Goods Sold based on exact ml/gram material consumption during that month)`.
- **TC22 (Logic - Empty Month):** Generate report for a month when the branch was closed (0 bookings).
  - *Expect:* HTTP 200, Revenue = 0, COGS = 0, Profit = 0.
- **TC23 (Security - Data Scope):** Manager of Branch A attempts to pull the profit report for Branch B.
  - *Expect:* HTTP 403 Forbidden, "You do not have permission to view financial data for this branch".

### 2.5. POST /api/v1/manager/inventory/extra-usage-requests/{id}/approve (Approve Extra Usage)
- **TC24 (Happy Path):** Approve a Staff's request to use an extra 50ml of specialized soap for a heavily soiled vehicle.
  - *Expect:* HTTP 200, request status becomes `Approved`. Exactly 50ml is instantly deducted from branch inventory.
- **TC25 (Policy - Stock Out):** Approve the request, but the branch currently has 0ml of that soap left.
  - *Expect:* HTTP 400, "Insufficient stock to approve this request".
- **TC26 (State - Double Approve):** Manager accidentally double-clicks the "Approve" button.
  - *Expect:* HTTP 200 on first click, HTTP 400 "Request already approved" on second click. Inventory is only deducted ONCE.
- **TC27 (State - Reject):** Manager clicks Reject because the staff requested an unreasonable amount (e.g., 5 Liters of soap for 1 car).
  - *Expect:* HTTP 200, request status becomes `Rejected`. No inventory is deducted.
# Deep Dive: Business (B2B) & HR - Exhaustive Test Cases

This document provides a highly detailed, exhaustive list of test cases for the Phase 4 API group (`BusinessController`, `ManagerOvertimeRequestController`, `ManagerShiftSwapRequestController`, `ManagerWorkShiftController`). It focuses strictly on B2B Fleet Account lifecycle, Monthly Invoice generation, and HR Shift Rostering logic.

---

## 1. BusinessController (B2B Fleet Accounts)

### 1.1. POST /api/v1/business/register (Register B2B Profile)
- **TC01 (Happy Path):** Submit a valid B2B registration containing a unique Tax Code, Company Name, and Representative details.
  - *Expect:* HTTP 200, profile created with `Pending` status. B2B account cannot use fleet features yet.
- **TC02 (Validation - Conflict):** Submit a registration using a Tax Code that already exists in the system.
  - *Expect:* HTTP 409 Conflict, "Tax code is already registered".
- **TC03 (Security - Injection):** Inject XSS script tags into the `CompanyName` field.
  - *Expect:* HTTP 200, payload safely sanitized before insertion.

### 1.2. POST /api/v1/business/admin/review-application (Approve B2B Account)
- **TC04 (Happy Path - Approve):** Admin/Manager reviews and approves a `Pending` B2B application.
  - *Expect:* HTTP 200, profile status becomes `Approved`. System unlocks fleet features (Bulk Import, Corporate Bookings) for the B2B user.
- **TC05 (State - Invalid Transition):** Attempt to approve a profile that is already `Approved` or `Rejected`.
  - *Expect:* HTTP 400, "Invalid application state for approval".
- **TC06 (Security - RBAC):** Standard Staff attempts to approve a B2B application.
  - *Expect:* HTTP 403 Forbidden.

### 1.3. GET /api/v1/business/statements/monthly (Monthly Corporate Invoice)
- **TC07 (Data Integrity - Math Validation):** B2B Admin requests the billing statement for the previous month (e.g., June).
  - *Expect:* HTTP 200. The `TotalAmountDue` MUST mathematically equal the exact sum of all `FinalAmount` from every `Completed` Wash Log associated with this B2B account during June.
- **TC08 (Logic - Empty Period):** Request statement for a month where the fleet had 0 washes.
  - *Expect:* HTTP 200, returns a statement object with `TotalAmountDue = 0` and an empty wash log array.
- **TC09 (Security - Cross-Tenant Data Leak):** B2B User A attempts to view the monthly statement of B2B User B by manipulating URL parameters.
  - *Expect:* HTTP 403 Forbidden, API strictly isolates data based on the JWT Token's `BusinessProfileId`.

### 1.4. POST /api/v1/business/bookings (Corporate Booking)
- **TC10 (Happy Path - Corporate Billing):** B2B User creates a booking for a fleet vehicle.
  - *Expect:* HTTP 200, booking created. Crucially, the `PaymentMethod` is automatically set to `POSTPAID/CORPORATE` (added to the monthly invoice) instead of requiring immediate wallet deduction or cash.
- **TC11 (Policy - Non-fleet Vehicle):** B2B User attempts to book a wash for a license plate that is NOT registered in their approved fleet list.
  - *Expect:* HTTP 400, "This vehicle is not registered in your corporate fleet".

---

## 2. Manager HR Controllers (Shifts, Overtime & Swaps)

### 2.1. POST /api/v1/manager/work-shifts (Create Shift Template)
- **TC12 (Happy Path):** Create a standard Morning Shift (Start: 08:00, End: 12:00).
  - *Expect:* HTTP 201, shift template created successfully.
- **TC13 (Validation - Time Logic):** Attempt to create a shift where the End Time (e.g., 10:00) is EARLIER than the Start Time (e.g., 14:00).
  - *Expect:* HTTP 400, "End time must be after start time".
- **TC14 (Logic - Overlap):** Create a shift that perfectly overlaps with an existing shift template.
  - *Expect:* HTTP 200 (if overlapping templates are allowed) or HTTP 409 (if system enforces strict unique timeframes).

### 2.2. PUT /api/v1/manager/overtime-requests/{id}/review (Review Overtime)
- **TC15 (Happy Path - Approve):** Manager approves an overtime request (2 hours) submitted by Staff A.
  - *Expect:* HTTP 200, request status becomes `Approved`. The system automatically appends the 2 hours to Staff A's timesheet/roster for payroll calculation.
- **TC16 (Happy Path - Reject):** Manager rejects the request, providing a string reason (e.g., "Not enough traffic to justify OT").
  - *Expect:* HTTP 200, status becomes `Rejected`. Timesheet is NOT modified. Notification pushed to Staff A.
- **TC17 (State - Double Review):** Manager accidentally clicks "Approve" twice.
  - *Expect:* HTTP 200 on first click, HTTP 400 "Request has already been processed" on second click. Overtime hours are strictly added ONLY ONCE.

### 2.3. PUT /api/v1/manager/shift-swap-requests/{id}/review (Review Shift Swap)
- **TC18 (Happy Path - Final Approval):** Manager approves a swap request between Staff A and Staff B (who have both already consented).
  - *Expect:* HTTP 200, request becomes `Approved`. The system automatically updates the live roster: Staff A takes B's shift, and B takes A's shift.
- **TC19 (Policy - Missing Consent):** Manager attempts to approve a swap request created by Staff A, but Staff B has not yet clicked "Agree" on their end.
  - *Expect:* HTTP 400, "Cannot approve swap. Target staff member has not yet consented to the swap".
- **TC20 (Logic - Conflicting Schedule):** Manager approves the swap, but Staff A has since been assigned another overlapping shift on the target day.
  - *Expect:* HTTP 409 Conflict, "Cannot finalize swap. Staff A now has a scheduling conflict on the target date".

---

## 3. BranchesController (Public Branch API)

### 3.1. GET /api/v1/branches (List Operating Branches)
- **TC21 (Happy Path - Public Access):** Unauthenticated user (guest) loads the mobile app home screen to find nearby branches.
  - *Expect:* HTTP 200, returns array of branches containing public data only (ID, Name, Address, Coordinates, Operating Hours). Sensitive data (like manager ID, total daily revenue) MUST NOT be exposed.
- **TC22 (Logic - Offline Branches):** Admin has temporarily disabled Branch X due to renovation.
  - *Expect:* HTTP 200, Branch X is completely filtered out of the response (or explicitly flagged as `isActive = false`).
# Ultra Deep Dive: Manager & Business - +100 Edge Cases

This document provides an additional 100 exhaustive test cases for the Phase 4 API group (`ManagerController`, `ManagerInventoryController`, `BusinessController`, and HR Controllers), strictly focusing on Financial Auditing, B2B Fleet Billing anomalies, Cross-Tenant Leaks, and Inventory Deadlocks.

---

## 1. ManagerController & Revenue AI

### 1.1. POST /api/v1/manager/revenue-stimulus/comprehensive-proposals
- **TC40 (Data Skew):** Generate proposal when one single corporate booking artificially inflated last week's revenue by 500%. AI should detect outliers and not trigger false "revenue drops" this week.
- **TC41 (Logic):** Branch has been open for only 3 days. Baseline data is insufficient. AI must return HTTP 400 "Insufficient historical data".
- **TC42 (Concurrency):** Manager clicks generate on two different browsers at the exact same millisecond. DB must lock and only generate 1 set of proposals.
- **TC43 (Security):** Manager from Branch A injects `branchId=B` into the hidden payload. System must strictly override payload with JWT claims.

### 1.2. POST /api/v1/manager/revenue-stimulus/proposals/{voucherId}/approve
- **TC44 (State):** Approve a voucher that has a start date in the past.
- **TC45 (Validation):** Approve a voucher where the AI accidentally proposed a 105% discount. System must cap at 100%.
- **TC46 (Audit):** Approval must log the Manager ID and exact timestamp into the `CampaignAudit` table.
- **TC47 (Performance):** Approve a campaign targeting 50,000 users. API must return HTTP 202 Accepted and process SMS/Emails asynchronously via background workers.

### 1.3. POST /api/v1/manager/branch-overload/scan-and-notify-relocation
- **TC48 (Logic):** Scan triggers, but the only nearby branch is currently marked `isActive = false` (under maintenance). No relocation should be offered.
- **TC49 (Logic):** Relocation pushes users to Branch B, causing Branch B to instantly cross the 80% overload threshold. System must prevent cascading overload.
- **TC50 (Rate Limit):** Ensure strict 30-minute cooldown on manual trigger to avoid spamming APNS/FCM.

## 2. ManagerInventoryController (Branch Inventory Deadlocks)

### 2.1. POST /api/v1/manager/inventory/imports
- **TC51 (Math Precision):** Import 10.33 Liters of Soap. Verify DB handles floating point decimals correctly without rounding errors.
- **TC52 (Logic - Duplicate PO):** Submit the exact same Purchase Order (PO) Number twice. DB must reject duplicate POs to prevent double stocking.
- **TC53 (Security):** Import material into a branch the Manager doesn't own.

### 2.2. POST /api/v1/manager/inventory/batches/{id}/discard
- **TC54 (Concurrency):** Manager A discards 10 units, Manager B discards 10 units from a batch that only has 10 units left, exactly at the same millisecond. DB must throw HTTP 409 Conflict for one of them.
- **TC55 (Logic):** Discard a batch that is NOT expired yet. Require explicit `Reason` string (e.g., "Damaged").

### 2.3. GET /api/v1/manager/inventory/reports/profit
- **TC56 (Timezone):** Generate profit report from `2026-01-01T00:00:00Z` to `2026-01-31T23:59:59Z`. Verify boundary transactions on Jan 1st 00:00 and Jan 31st 23:59 are included.
- **TC57 (Math - Vouchers):** Verify that Vouchers (Discounts) are mathematically subtracted from Gross Revenue to calculate Net Revenue.
- **TC58 (Math - Refunds):** Verify that `Cancelled` and refunded bookings are completely excluded from Revenue.
- **TC59 (Math - Extra Materials):** Verify that Staff `Extra Material Usage` is correctly added to the COGS (Cost of Goods Sold), reducing the final profit.

### 2.4. POST /api/v1/manager/inventory/extra-usage-requests/{id}/approve
- **TC60 (Deadlock):** Manager approves extra usage request for 50ml, but a `Completed` wash just deducted the last 50ml of stock 1 millisecond ago. System must gracefully handle the out-of-stock exception.
- **TC61 (State):** Manager approves a request for a booking that was already Force-Cancelled. Request must automatically auto-reject.

## 3. BusinessController (B2B Corporate Billing)

### 3.1. POST /api/v1/business/register
- **TC62 (Validation):** Submit Tax Code with spaces or special chars (`0123 456-789`). System must strip and normalize.
- **TC63 (Boundary):** Tax code length constraints (e.g., exactly 10 or 13 digits for Vietnam standard).
- **TC64 (Data Leak):** Use an email that already exists as a Retail Customer. Should system merge accounts or require unique B2B emails?

### 3.2. GET /api/v1/business/statements/monthly (Invoice Integrity)
- **TC65 (Math - Partial Refund):** A B2B fleet vehicle had a bad wash and was issued a 50% partial refund. Ensure the Monthly Statement mathematically reflects this deduction.
- **TC66 (Math - No-Show):** A B2B fleet vehicle was marked as `No-Show` with a 20% penalty fee. Ensure this fee is correctly added to the invoice.
- **TC67 (Format):** Export statement as PDF. Verify PDF layout does not break if there are 1,000 wash logs in a single month.
- **TC68 (Logic - Unpaid Prior Month):** Invoice for June must include any carry-over debt (Unpaid balance) from May.

### 3.3. POST /api/v1/business/bookings
- **TC69 (Limit):** B2B Admin attempts to book 50 slots simultaneously. System must enforce max concurrent bookings limit to prevent monopolizing the wash floor.
- **TC70 (Policy - Credit Limit):** B2B account has exceeded its predefined monthly credit limit (e.g., > 50,000,000 VND unpaid). System must reject new bookings until debt is settled.
- **TC71 (Security):** Inject a `paymentMethod="WALLET"` flag into the B2B payload. System must forcefully override it to `POSTPAID/CORPORATE`.

## 4. Manager HR (Shifts, Swaps, Overtime)

### 4.1. POST /api/v1/manager/work-shifts
- **TC72 (Boundary):** Shift that crosses midnight (Start: 22:00, End: 06:00 next day). Verify date calculation logic for payroll.
- **TC73 (Validation):** Create a shift with a 0-minute duration (Start: 08:00, End: 08:00).
- **TC74 (Validation):** Create a shift > 24 hours long.

### 4.2. PUT /api/v1/manager/overtime-requests/{id}/review
- **TC75 (Security - Self Approval):** Manager submits an OT request for themselves and attempts to approve their own request. System must block (Requires Super Admin or Peer Manager approval).
- **TC76 (Math):** Staff has 1 hour OT approved on Monday, 2 hours OT on Tuesday. Attempt to approve 3 hours OT on Friday (Assuming weekly max OT is 5 hours). System must block the Friday request.
- **TC77 (State):** Approve OT for a staff member who was terminated yesterday.

### 4.3. PUT /api/v1/manager/shift-swap-requests/{id}/review
- **TC78 (Logic - Cross Branch):** Staff A (Branch 1) attempts to swap with Staff B (Branch 2). System must reject unless cross-branch floaters are supported.
- **TC79 (State - Post Facto):** Manager attempts to approve a shift swap for a date that has already passed.
- **TC80 (Policy):** Approve a swap that results in Staff A working 16 consecutive hours without a mandated break (Labor law compliance).

## 5. Security & Mass Assignment (Phase 4 Global)

### 5.1. Global Put/Post Overrides
- **TC81 (Mass Assignment):** Manager attempts to update their own profile `PUT /api/v1/manager/me` and injects `{"BranchId": "ALL", "Role": "SuperAdmin"}`.
- **TC82 (IDOR):** Manager uses `PUT /api/v1/manager/lanes/assign-staff` but inputs a Staff ID that belongs to a different branch.
- **TC83 (Rate Limit):** Brute-force guessing B2B Tax Codes on the registration endpoint to scrape existing companies.
- **TC84 (SQL Injection):** Inject `' OR 1=1; DROP TABLE Users;--` into the Search HR Staff query parameter.
- **TC85 (XSS):** Manager inputs `<script>alert('XSS')</script>` as the rejection reason for an Overtime request. Ensure it does not execute when the Staff views their app.
- **TC86 (Replay Attack):** Intercept the HTTP request for approving a 1,000,000 VND Purchase Order. Replay the exact same packet 10 times. DB must reject based on transaction nonce/idempotency key.
- **TC87 (Data Leak):** `GET /api/v1/manager/inventory/reports/profit` exposes raw DB IDs or internal server stack traces on failure.
- **TC88 (Concurrency):** Two managers of the same branch both click "Approve" on a B2B Application simultaneously.
- **TC89 (Token Expiry):** Manager's JWT token expires mid-upload of a 5MB inventory CSV file.
- **TC90 (Format String):** Inject `%s%s%s%s%s` into Manager Note fields to attempt memory leaks (mostly applicable if C/C++ backend, but good practice).

## 6. Extreme Edge Cases (System Limits)

- **TC91 (Year 2038 Problem):** Pass timestamps far into the future to test 32-bit integer limits on UNIX time.
- **TC92 (Leap Second):** Schedule a shift at exactly 23:59:60 (Leap second insertion).
- **TC93 (Unicode):** Create a B2B Company name with 4-byte Emojis ðŸš—âœ¨ to ensure MySQL `utf8mb4` encoding doesn't crash.
- **TC94 (Null Byte Injection):** Inject `\0` in the B2B Tax Code string `010123\0456`.
- **TC95 (Denial of Service):** Request pagination `size=2147483647` (Max Int) on the Monthly Statements endpoint.
- **TC96 (Geo-Fencing Limits):** Manager triggers Relocation AI, but GPS coordinates of the branch are maliciously set to `[0,0]` (Null Island).
- **TC97 (Orphaned Data):** Delete a Staff member who has 5 pending Overtime requests. Ensure cascading delete or nullification handles the orphaned requests.
- **TC98 (Decimal Overflow):** B2B Monthly invoice totals 999,999,999,999 VND. Ensure frontend and backend decimal data types do not lose precision.
- **TC99 (Header Injection):** Inject fake `X-Forwarded-For` headers to bypass IP-based Manager Rate Limits.
- **TC100 (CORS/CSRF):** Attempt to trigger the AI Revenue Generator via a cross-site forged POST request (without anti-CSRF tokens).
# Phase 4: Manager & Business - Test Cases

This document outlines the test cases for the Manager and Business (B2B) API groups. These APIs handle branch operations, revenue stimulus logic, staff scheduling, inventory, and B2B fleet accounts.

---

## 1. ManagerController (Branch Operations & Revenue AI)

### 1.1. POST /api/v1/manager/revenue-stimulus/comprehensive-proposals (AI Revenue Stimulus)
- **TC01 (Happy Path - Revenue Drop):** Branch revenue dropped compared to last month.
  - *Expect:* HTTP 200, system automatically generates and returns 2 voucher proposals (Off-peak & Win-back) with status `Proposed`.
- **TC02 (Happy Path - Revenue Stable):** Branch revenue is stable or increasing.
  - *Expect:* HTTP 200, system indicates no stimulus is needed; no proposals are created.
- **TC03 (Security):** Call API with a regular Staff token.
  - *Expect:* HTTP 403 Forbidden.

### 1.2. POST /api/v1/manager/revenue-stimulus/proposals/{voucherId}/approve (Approve AI Voucher)
- **TC01 (Happy Path):** Manager approves a valid `Proposed` voucher.
  - *Expect:* HTTP 200, voucher status changes to `Approved`, system automatically distributes the voucher to targeted customer wallets.
- **TC02 (Negative):** Attempt to approve a voucher that is already `Approved` or `Rejected`.
  - *Expect:* HTTP 400, "Invalid voucher state for approval".

### 1.3. POST /api/v1/manager/branch-overload/scan-and-notify-relocation (Proactive Relocation Scanner)
- **TC01 (Happy Path - Overload Detected):** Branch has too many pending bookings in the next 2 hours.
  - *Expect:* HTTP 200, returns a list of customers who were automatically sent push notifications asking them to relocate.
- **TC02 (Happy Path - Normal Load):** Branch has sufficient capacity.
  - *Expect:* HTTP 200, returns an empty list, no notifications sent.

### 1.4. GET /api/v1/manager/lanes & POST /api/v1/manager/lanes (Lane CRUD)
- **TC01 (Happy Path - GET):** Retrieve list of operating lanes for the branch.
  - *Expect:* HTTP 200, array of lanes with their current active status.
- **TC02 (Happy Path - POST):** Manager adds a new wash lane.
  - *Expect:* HTTP 201, lane created successfully.
- **TC03 (Negative - POST):** Create a lane with a duplicate name within the same branch.
  - *Expect:* HTTP 400, "Lane name already exists in this branch".

### 1.5. POST /api/v1/manager/lanes/assign-staff (Assign Staff to Lane)
- **TC01 (Happy Path):** Assign a staff member to Lane A for today.
  - *Expect:* HTTP 200, assignment record created.
- **TC02 (Validation):** Assign a staff member who is currently on leave today.
  - *Expect:* HTTP 400, "Staff member is not scheduled to work today".

### 1.6. POST /api/v1/manager/bookings/{bookingId}/checkin-assign (Manager Force Check-in)
- **TC01 (Happy Path):** Manager forcefully checks in a vehicle and explicitly assigns it to a specific lane/staff.
  - *Expect:* HTTP 200, booking status becomes `CheckedIn` and `AssignedLaneId` is updated.

---

## 2. ManagerInventoryController (Inventory Management)

### 2.1. GET /api/v1/manager/inventory/stocks (View Stock Levels)
- **TC01 (Happy Path):** Retrieve all inventory items for the branch.
  - *Expect:* HTTP 200, returns list of materials with current quantities and low-stock alerts.

### 2.2. POST /api/v1/manager/inventory/imports (Import Materials)
- **TC01 (Happy Path):** Import 100 bottles of Ceramic Coating.
  - *Expect:* HTTP 200, stock level increases by 100, transaction logged.
- **TC02 (Negative):** Import a negative quantity.
  - *Expect:* HTTP 400, "Import quantity must be greater than zero".

### 2.3. POST /api/v1/manager/inventory/extra-usage-requests/{id}/approve (Approve Extra Material Usage)
- **TC01 (Happy Path):** Approve an extra usage request submitted by Staff.
  - *Expect:* HTTP 200, request status becomes `Approved`, exact amount deducted from branch stock.
- **TC02 (Negative):** Approve a request when current stock is insufficient.
  - *Expect:* HTTP 400, "Insufficient stock to approve this request".

---

## 3. Manager HR Controllers (Shifts, Overtime & Swaps)

### 3.1. PUT /api/v1/manager/overtime-requests/{id}/review (Review Overtime Request)
- **TC01 (Happy Path - Approve):** Manager approves a staff's overtime request.
  - *Expect:* HTTP 200, request status updated to `Approved`, system automatically adds the overtime hours to the staff's timesheet.
- **TC02 (Happy Path - Reject):** Manager rejects the request with a reason.
  - *Expect:* HTTP 200, request status updated to `Rejected`, notification sent to staff.

### 3.2. PUT /api/v1/manager/shift-swap-requests/{id}/review (Review Shift Swap Request)
- **TC01 (Happy Path):** Manager approves a swap between Staff A and Staff B.
  - *Expect:* HTTP 200, system automatically updates the roster, swapping their respective shifts.
- **TC02 (Negative):** Approve a swap request that hasn't been acknowledged by the second staff member.
  - *Expect:* HTTP 400, "Both staff members must agree before Manager approval".

### 3.3. POST /api/v1/manager/work-shifts (Create Work Shift)
- **TC01 (Happy Path):** Create a standard morning shift (08:00 - 12:00).
  - *Expect:* HTTP 201, shift template created.
- **TC02 (Validation):** End time is earlier than start time.
  - *Expect:* HTTP 400, "End time must be after start time".

---

## 4. BusinessController (B2B Fleet Accounts)

### 4.1. POST /api/v1/business/register (Register B2B Account)
- **TC01 (Happy Path):** Submit valid company details, tax code, and representative info.
  - *Expect:* HTTP 200, profile created with `Pending` status.
- **TC02 (Negative):** Submit an already registered tax code.
  - *Expect:* HTTP 400, "Tax code is already registered".

### 4.2. GET /api/v1/business/admin/pending-applications (List Pending B2B Apps - Admin/Manager view)
- **TC01 (Happy Path):** Retrieve list of B2B applications awaiting approval.
  - *Expect:* HTTP 200, returns array of pending profiles.

### 4.3. POST /api/v1/business/admin/review-application (Approve/Reject B2B App)
- **TC01 (Happy Path - Approve):** Admin approves a pending B2B profile.
  - *Expect:* HTTP 200, profile status becomes `Approved`, B2B account gains access to fleet features.

### 4.4. GET /api/v1/business/vehicles (List Corporate Vehicles)
- **TC01 (Happy Path):** Authenticated Business user retrieves their registered fleet.
  - *Expect:* HTTP 200, returns array of fleet vehicles.

### 4.5. GET /api/v1/business/statements/monthly (Monthly Billing Statement)
- **TC01 (Happy Path):** Business user requests the statement for the previous month.
  - *Expect:* HTTP 200, returns aggregated wash logs, total expenses, and the generated monthly invoice details.
- **TC02 (Empty Period):** Request statement for a month with no washes.
  - *Expect:* HTTP 200, returns statement with zero totals.

### 4.6. POST /api/v1/business/washlogs/{washLogId}/assign-lane (Assign Fleet Vehicle to Lane)
- **TC01 (Happy Path):** Manager explicitly routes a B2B fleet vehicle to an express lane.
  - *Expect:* HTTP 200, wash log updated with assigned LaneId.

---

## 5. BranchesController (Public Branch Info)

### 5.1. GET /api/v1/branches
- **TC01 (Happy Path):** Unauthenticated user requests the list of operating branches.
  - *Expect:* HTTP 200, returns public branch details (address, coordinates, operating hours).
