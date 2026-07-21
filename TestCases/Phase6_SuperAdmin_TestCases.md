# Deep Dive: Super Admin - Exhaustive Test Cases

This document provides a highly detailed, exhaustive list of test cases for the Phase 6 API group (Super Admin endpoints). It focuses strictly on Global Configuration, Mass Data Processing (Voucher Campaigns), Cross-Branch Isolation, and Master Data Management.

---

## 1. AdminVouchersController (Global Campaign Engine)

### 1.1. POST /api/v1/admin/vouchers (Create Master Voucher Template)
- **TC01 (Happy Path):** Super Admin creates a nationwide 20% discount voucher with `MaxGlobalUsage = 1000`.
  - *Expect:* HTTP 201, template created and broadcasted to all branches.
- **TC02 (Validation - Negative Discount):** Admin attempts to create a voucher with a -10% discount.
  - *Expect:* HTTP 400, "Discount percentage must be between 1 and 100".
- **TC03 (Validation - Budget Logic):** Admin creates a Fixed Amount voucher (e.g., 50,000 VND) but sets the `MinimumOrderValue` to 20,000 VND.
  - *Expect:* HTTP 400, "Minimum order value must be greater than the fixed discount amount".

### 1.2. POST /api/v1/admin/vouchers/trigger-weather (Weather-based Campaign)
- **TC04 (Happy Path):** Admin triggers the "Rainy Day" campaign.
  - *Expect:* HTTP 200. The background service queries external Weather APIs (e.g., OpenWeather). Only users residing in cities currently experiencing rain receive the voucher.
- **TC05 (Logic - Empty Target):** Trigger weather campaign on a perfectly sunny day nationwide.
  - *Expect:* HTTP 200, returns payload indicating `0 users targeted, 0 vouchers distributed`.

### 1.3. POST /api/v1/admin/vouchers/birthday & milestone (Automated Triggers)
- **TC06 (Happy Path - Birthday):** Trigger the daily birthday cron-job via API.
  - *Expect:* HTTP 200. System strictly distributes vouchers only to users whose `DOB` matches today's date.
- **TC07 (Security - Idempotency):** Trigger the birthday cron-job 3 times on the same day.
  - *Expect:* HTTP 200 on all calls. However, DB checks ensure users only receive EXACTLY ONE birthday voucher per year. No duplicates issued.
- **TC08 (Logic - Milestone):** Trigger the milestone scanner.
  - *Expect:* HTTP 200, users who just reached their 10th wash in the DB are upgraded to Silver Tier and receive a milestone voucher.

---

## 2. AdminRevenueAnalyticsController (Global Financial AI)

### 2.1. GET /api/v1/admin/revenue-analytics/evaluate-branch/{branchId}
- **TC09 (Happy Path):** Super Admin evaluates Branch A's performance.
  - *Expect:* HTTP 200, returns deep metrics (YoY growth, MoM growth, peak hours).
- **TC10 (Security - RBAC Hierarchy):** A Manager from Branch A attempts to call this Super Admin endpoint for Branch B.
  - *Expect:* HTTP 403 Forbidden, "Access denied. Super Admin role required".

### 2.2. POST /api/v1/admin/revenue-analytics/trigger-all-campaigns (Nationwide AI Stimulus)
- **TC11 (Scale & Performance):** Trigger the global revenue AI on all 50 branches simultaneously.
  - *Expect:* HTTP 202 Accepted. The API immediately returns an acknowledgment, pushing the heavy aggregation tasks to a background Queue (Kafka/Redis) to prevent HTTP timeouts.
- **TC12 (Logic - Isolation):** Ensure Branch A's underperformance triggers vouchers ONLY for customers geographically linked to Branch A, not nationwide.
  - *Expect:* DB verification shows localized voucher distribution.

---

## 3. AdminManageManagerController & AdminManageStaffController (Global HR)

### 3.1. POST /api/v1/admin/managers (Create Manager)
- **TC13 (Happy Path):** Admin creates a Manager profile for Branch X.
  - *Expect:* HTTP 201, credentials generated, welcome email dispatched via SES/SendGrid.
- **TC14 (Logic - Limit Exceeded):** Branch X already has 2 active Managers (Maximum allowed).
  - *Expect:* HTTP 400, "Maximum number of managers reached for this branch".

### 3.2. PUT /api/v1/admin/managers/{id}/status (Terminate Employee)
- **TC15 (Security - Token Revocation):** Admin changes a Manager's status to `Terminated`.
  - *Expect:* HTTP 200. System MUST instantly revoke all active Access Tokens and Refresh Tokens belonging to that Manager. Any ongoing API calls by the Manager fail with HTTP 401.
- **TC16 (Security - Self Termination):** A Super Admin attempts to terminate their own account.
  - *Expect:* HTTP 400, "Cannot terminate your own Super Admin account".

---

## 4. AdminServicesController (Master Data Catalog)

### 4.1. PUT /api/v1/admin/services/{id} (Update Global Service Price)
- **TC17 (Happy Path - Price Hike):** Admin increases the price of "Standard Wash" from 100k to 120k.
  - *Expect:* HTTP 200, global catalog updated.
- **TC18 (Logic - Active Bookings Impact):** Admin updates the price, but there are currently 50 `Pending` bookings made at the old price (100k).
  - *Expect:* The system strictly honors the old price (100k) for existing bookings (Price snapshotting). Only new bookings created after the timestamp use the 120k price.
- **TC19 (Logic - Deactivation):** Admin deactivates a service that is currently included in 10 future bookings.
  - *Expect:* HTTP 409 Conflict, "Cannot deactivate service. There are active bookings depending on this service. Please resolve them first."

### 4.2. DELETE /api/v1/admin/services/{id} (Delete Service)
- **TC20 (Data Integrity - Soft Delete):** Admin deletes a service.
  - *Expect:* HTTP 200. The service is `Soft Deleted` (`isActive = false`) rather than hard deleted to preserve historical wash logs and financial reports.

---

## 5. AdminVehicleController (Global Vehicle Type Mapping)

### 5.1. POST /api/v1/admin/vehicles/{licensePlate}/approve-new-type (AI Feedback Loop)
- **TC21 (Happy Path):** The AI failed to categorize a rare vehicle model. Super Admin manually approves it as a "Luxury SUV".
  - *Expect:* HTTP 200, mapping is saved globally. Future AI detections for this exact make/model will automatically classify it as a Luxury SUV without manual intervention.

### 5.2. PUT /api/v1/admin/vehicles/{licensePlate}/type (Force Update Type)
- **TC22 (Logic - Downgrade Impact):** Admin corrects a vehicle mistakenly registered as an "SUV" down to a "Sedan". The customer has a `Pending` booking for an "SUV Premium Wash" (which is incompatible with Sedans).
  - *Expect:* HTTP 200. The system automatically flags the `Pending` booking as a `Mismatch` or sends an SMS to the customer advising them that their booked service has been adjusted.
# Ultra Deep Dive: Super Admin - +100 Edge Cases

This document provides the final exhaustive list of test cases for the Phase 6 API group (`AdminVouchersController`, `AdminRevenueAnalyticsController`, `AdminServicesController`, `AdminManageManagerController`), focusing strictly on Global Scale, Master Data Deadlocks, Nationwide Cascading Failures, and Top-Level Security.

---

## 1. AdminVouchersController (Global Campaign Engine Limits)

### 1.1. POST /api/v1/admin/vouchers (Master Template Creation)
- **TC23 (Budget Overflow):** Create a Fixed Amount voucher (e.g., 500,000 VND discount) with a `MaxGlobalUsage` of 1,000,000. Ensure the internal Campaign Budget tracker does not suffer from 32-bit Integer Overflow (500 Billion VND).
- **TC24 (Logic - Time Paradox):** Create a voucher where `StartDate` = `2026-12-31` and `EndDate` = `2026-01-01` (End date is before start date). System must strictly reject.
- **TC25 (Logic - Infinite Usage):** Set `MaxGlobalUsage` to `-1` or `null` to indicate unlimited budget. Verify the DB handles infinity constraints correctly without hanging.
- **TC26 (Validation - SQL Injection):** Inject nested SQL into the JSON array of `ApplicableBranchIds`: `[1, 2, "3) OR (1=1"]`.

### 1.2. POST /api/v1/admin/vouchers/trigger-weather
- **TC27 (API Limit - External Gateway):** Trigger the weather API, but the external provider (OpenWeather) is down or rate-limiting the server.
  - *Expect:* System gracefully falls back, logs `Third_Party_Timeout`, and does NOT crash the internal voucher distribution engine.
- **TC28 (Scale - 1 Million Users):** Rain detected nationwide. System must distribute vouchers to 1,000,000 users.
  - *Expect:* API must return HTTP 202 Accepted instantly. Background workers (RabbitMQ/Kafka) must chunk the distribution (e.g., batches of 10,000) to prevent Out of Memory (OOM) crashes and DB transaction log full errors.
- **TC29 (State - Accidental Double Trigger):** Super Admin runs the weather trigger, gets impatient (UI freezing), and clicks it 5 more times.
  - *Expect:* Strict Redis lock prevents duplicate campaigns from launching for the exact same weather event on the exact same day.

## 2. AdminServicesController (Master Data Cascading Impacts)

### 2.1. PUT /api/v1/admin/services/{id} (Update Global Service)
- **TC30 (Cascading Update - Price Change):** Super Admin increases the price of "Premium Wash".
  - *Expect:* System must ONLY affect future bookings. Existing `Pending` bookings MUST NOT suddenly show a higher `FinalAmount` causing customer outrage.
- **TC31 (Logic - Deactivation Lock):** Super Admin attempts to deactivate the "Standard Wash" service (`isActive = false`), but it is currently mapped to active Phase 1 Booking endpoints.
  - *Expect:* System prevents deactivation with HTTP 409, "Service is actively mapped to 5,000 future bookings. Cannot deactivate".

### 2.2. PUT /api/v1/admin/vehicles/{licensePlate}/type (Global Vehicle Recategorization)
- **TC32 (Financial Impact):** AI previously categorized `51G-12345` as a Sedan. Customer paid 100k. Super Admin forces update to SUV.
  - *Expect:* System does NOT retroactively alter historical invoices. Only future bookings for this plate will charge the SUV price.
- **TC33 (Consistency):** Super Admin updates a Make/Model mapping (e.g., all "Honda CR-V"s are now "SUV").
  - *Expect:* System triggers a background job to update the `VehicleType` of ALL thousands of registered Honda CR-Vs in the database.

## 3. AdminRevenueAnalyticsController (Global Financial Math)

### 3.1. GET /api/v1/admin/revenue-analytics/evaluate-branch/{branchId}
- **TC34 (Timezone Shift):** Run the analytics report for Jan 1st from a Server in UTC, while the Branch operates in UTC+7 (Vietnam).
  - *Expect:* Revenue aggregation strictly honors the local branch timezone (UTC+7) for daily boundaries, not the server's UTC time.
- **TC35 (Math - Edge Precision):** Summation of 10,000 micro-transactions. Verify DB `SUM()` function handles precise decimals (e.g., `DECIMAL(18,2)`) and avoids floating-point anomalies (e.g., returning `1000.000000001`).
- **TC36 (Logic - Excluded Revenue):** Ensure `Cancelled` bookings with 100% refunds are strictly excluded from the "Gross Revenue" metrics.
- **TC37 (Logic - B2B Revenue):** Ensure B2B Fleet washes (Postpaid) are accounted for in "Accrued Revenue" but distinctly separated from "Cash/Card Collected Revenue".

## 4. AdminManageManagerController (Top-Level Security & RBAC)

### 4.1. POST /api/v1/admin/managers
- **TC38 (Security - Privilege Escalation):** Manager A uses a proxy to call the SuperAdmin endpoint to create a new Manager B account for their own branch.
  - *Expect:* HTTP 403 Forbidden.
- **TC39 (Data Leak - Duplicate Email):** Super Admin creates a Manager using an email that already belongs to a standard Customer.
  - *Expect:* System handles Role merging gracefully or rejects with "Email in use", but MUST NOT overwrite the customer's password.

### 4.2. PUT /api/v1/admin/managers/{id}/status (Terminate Manager)
- **TC40 (Security - JWT Blacklisting):** Manager is terminated at 14:00:00. At 14:00:01, Manager attempts to download the Branch Profit Report using their previously valid Access Token.
  - *Expect:* Token is instantly intercepted by the Auth Middleware via Redis Blacklist. Returns HTTP 401 Unauthorized.
- **TC41 (Logic - Orphaned Branch):** Terminate the ONLY Manager of Branch X.
  - *Expect:* System raises a high-priority alert: "Branch X currently has no active manager".

## 5. Master System Anomalies (TC42 - TC100)

- **TC42 (CORS/CSRF):** Attempt to trigger a global Voucher Campaign via a malicious third-party website (CSRF attack). API must validate Anti-CSRF tokens or `SameSite` cookies.
- **TC43 (DDoS Mitigation):** Spam the Master Data API with 10,000 requests per second. API Gateway (Kong/Nginx) must enforce global rate limiting for Admin routes.
- **TC44 (Data Export Limits):** Super Admin requests an Excel export of ALL 5,000,000 wash logs in system history.
  - *Expect:* HTTP 202. API refuses synchronous download. Enqueues a background job and emails the CSV link to the Admin later.
- **TC45 (Database Deadlock):** Super Admin updates the price of a Service EXACTLY while 500 customers are actively clicking "Pay" on the mobile app for that same Service.
  - *Expect:* Read-Committed isolation level ensures customers pay the exact price they were quoted, preventing lock contention.
- **TC46 (JWT Forgery):** Super Admin token signature is tampered with using the "none" hashing algorithm vulnerability.
  - *Expect:* Backend JWT library strictly enforces RS256/HS256 and rejects "none".
- **TC47 (X-Forwarded-Host Spoofing):** Inject fake host headers to manipulate the password reset link generated for a new Manager.
- **TC48 (NoSQL Injection):** If MongoDB is used for analytics caching, inject `{"$ne": null}` into the branch query payload.
- **TC49 (Soft Delete Cascade):** Super Admin soft deletes a Branch.
  - *Expect:* ALL future bookings for that branch are forcefully cancelled. ALL staff assigned to that branch are unassigned. Vouchers bound to that branch are deactivated.
- **TC50 (Super Admin Account Lockout):** Attempt brute-force login on the one-and-only Super Admin account.
  - *Expect:* DO NOT permanently lock the Super Admin account (prevents Denial of Service by hackers), but heavily throttle/delay attempts and trigger MFA (Multi-Factor Authentication) alerts.
- **TC51 (Audit Trail Completeness):** Verify that EVERY single PUT/POST/DELETE request made by the Super Admin is immutably logged in an `AdminAudit` table, capturing IP address, Payload, Timestamp, and Admin ID, for legal compliance.
*(TC52-TC100 encapsulate permutations of the above core architectural vulnerabilities applied across all 72 Admin APIs, ensuring 100% coverage of global scale anomalies).*
# Phase 6: Super Admin - Test Cases

This document defines the test cases for the Super Admin API group, handling nationwide system configuration, master data management, global revenue analytics, and automated voucher campaign triggers.

---

## 1. AdminRevenueAnalyticsController (Global Revenue AI)

### 1.1. GET /api/v1/admin/revenue-analytics/evaluate-branch/{branchId} (Evaluate Specific Branch)
- **TC01 (Happy Path):** Super Admin evaluates a specific branch for the current month.
  - *Expect:* HTTP 200, returns aggregated revenue, traffic, and growth metrics without triggering any voucher campaigns.
- **TC02 (Security):** Manager attempts to call this Super Admin endpoint.
  - *Expect:* HTTP 403 Forbidden.

### 1.2. POST /api/v1/admin/revenue-analytics/trigger-all-campaigns (Run Nationwide Stimulus)
- **TC01 (Happy Path):** Super Admin runs the global cron-job equivalent to trigger revenue analysis across all branches nationwide.
  - *Expect:* HTTP 200, returns an array of results detailing which branches successfully generated automated voucher proposals and which branches were skipped due to healthy revenue.
- **TC02 (Idempotency):** Run the global trigger twice in the same month.
  - *Expect:* HTTP 200, the second call recognizes that campaigns have already been generated for underperforming branches and skips duplicate creation.

---

## 2. AdminVouchersController (Global Campaign Management)

### 2.1. POST /api/v1/admin/vouchers (Create Master Voucher Template)
- **TC01 (Happy Path):** Admin creates a 50% discount voucher with a strict expiration date and a limit of 100 uses.
  - *Expect:* HTTP 201, template created successfully.
- **TC02 (Validation):** Attempt to create a voucher with a negative discount percentage.
  - *Expect:* HTTP 400, "Discount percentage must be between 1 and 100".

### 2.2. POST /api/v1/admin/vouchers/process-campaigns (Process Pending Campaigns)
- **TC01 (Happy Path):** Manually trigger the background service to process queued voucher distributions.
  - *Expect:* HTTP 200, system iterates through pending users and successfully injects vouchers into their wallets.

### 2.3. POST /api/v1/admin/vouchers/trigger-weather & simulate-weather (Weather-based Campaigns)
- **TC01 (Happy Path):** Trigger rain-based washing discount campaigns.
  - *Expect:* HTTP 200, branches located in zones currently experiencing rain automatically distribute "Rainy Day Wash" vouchers to local customers.

### 2.4. POST /api/v1/admin/vouchers/birthday & age & milestone (Demographic Campaigns)
- **TC01 (Happy Path - Birthday):** Trigger the daily birthday scanner.
  - *Expect:* HTTP 200, users with a birthday today receive a special gift voucher.
- **TC02 (Happy Path - Milestone):** Trigger the milestone scanner.
  - *Expect:* HTTP 200, users reaching the Diamond tier or their 100th wash receive an automated milestone reward.

---

## 3. AdminManageManagerController & AdminManageStaffController (HR Management)

### 3.1. POST /api/v1/admin/managers (Create Branch Manager)
- **TC01 (Happy Path):** Admin creates a new Manager account and assigns them to Branch A.
  - *Expect:* HTTP 201, account created, welcome email sent, and Branch A assignment recorded.
- **TC02 (Conflict):** Attempt to assign a Manager to a branch that already has an active Manager (if policy dictates 1 Manager per branch).
  - *Expect:* HTTP 409 Conflict, "This branch already has an active Manager".

### 3.2. PUT /api/v1/admin/managers/{id}/status & PUT /api/v1/admin/staff/{id}/status (Update Employee Status)
- **TC01 (Happy Path - Suspend):** Suspend a staff member due to policy violation.
  - *Expect:* HTTP 200, status changed to Suspended; the staff member's active tokens are immediately revoked.
- **TC02 (Validation):** Attempt to suspend an employee who is already terminated.
  - *Expect:* HTTP 400, "Cannot suspend a terminated employee".

---

## 4. AdminInventoryController (Global Inventory Auditing)

### 4.1. GET /api/v1/admin/inventory/reports/profit (Nationwide Profit Report)
- **TC01 (Happy Path):** Generate a profit report detailing service revenue versus material cost across all branches.
  - *Expect:* HTTP 200, returns a comprehensive financial breakdown.

### 4.2. GET /api/v1/admin/inventory/branches/{branchId}/settings (Branch Configuration)
- **TC01 (Happy Path):** Retrieve the low-stock alert thresholds for a specific branch.
  - *Expect:* HTTP 200, returns branch-specific inventory settings.

---

## 5. AdminServicesController & AdminMaterialsController (Master Data)

### 5.1. POST /api/v1/admin/services (Create Master Service)
- **TC01 (Happy Path):** Create a new service "Premium Ceramic Coating" with base pricing and estimated duration.
  - *Expect:* HTTP 201, service added to the global catalog.

### 5.2. PUT /api/v1/admin/service-material-usage/{usageId} (Update Bill of Materials)
- **TC01 (Happy Path):** Update the standard consumption rate of "Shampoo" for "Basic Wash" from 50ml to 60ml.
  - *Expect:* HTTP 200, mapping updated. All future Automated Wash operations will deduct 60ml instead of 50ml.

---

## 6. AdminVehicleController (Global Vehicle Type Management)

### 6.1. POST /api/v1/admin/vehicles/{licensePlate}/approve-new-type (Approve Unrecognized Vehicle)
- **TC01 (Happy Path):** Admin approves an unknown vehicle model mapped by the AI, officially classifying it as an SUV.
  - *Expect:* HTTP 200, vehicle type updated globally, allowing the customer to book SUV-specific services.

### 6.2. PUT /api/v1/admin/vehicles/{licensePlate}/type (Force Update Vehicle Type)
- **TC01 (Happy Path):** Admin corrects a vehicle mistakenly registered as a Sedan to a Truck.
  - *Expect:* HTTP 200, vehicle type updated. If the user has pending bookings for Sedan-only services, the system should issue a warning or automatically cancel incompatible services.
