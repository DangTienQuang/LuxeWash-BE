# Deep Dive: BookingsController - Exhaustive Test Cases

This document provides a highly detailed, exhaustive list of test cases for the `BookingsController`, which is the core operational engine of the system. It covers strict AI Load Balancing logic (Smart Routing), Concurrency (Overbooking prevention), Cancellation policies, and Payment state transitions.

---

## 1. POST /api/v1/bookings/check-compatibility (Service-Vehicle Compatibility)

- **TC01 (Happy Path):** Submit a Sedan vehicle ID and a "Sedan Standard Wash" service ID.
  - *Expect:* HTTP 200, `isCompatible = true`.
- **TC02 (Negative):** Submit a Sedan vehicle ID and an "SUV Premium Wash" service ID.
  - *Expect:* HTTP 200, `isCompatible = false`, "Service not applicable for your vehicle type".
- **TC03 (Validation):** Submit an empty vehicle ID or service ID.
  - *Expect:* HTTP 400, validation error.
- **TC04 (Logic):** Submit a service ID that is globally deactivated.
  - *Expect:* HTTP 404, "Service not found or inactive".
- **TC05 (Security - SQLi):** Inject SQL payload into the vehicle ID parameter.
  - *Expect:* HTTP 400, strictly rejected by validation middleware.

## 2. POST /api/v1/bookings/available-slots (Standard Slot Checking)

- **TC06 (Happy Path):** Request slots for Branch A on today's date.
  - *Expect:* HTTP 200, returns array of 30-minute intervals with current capacity counters.
- **TC07 (Negative - Past Date):** Request slots for yesterday.
  - *Expect:* HTTP 400, "Cannot check availability for past dates".
- **TC08 (Negative - Far Future):** Request slots for a date > 30 days in the future.
  - *Expect:* HTTP 400, "Cannot book more than 30 days in advance".
- **TC09 (Logic - Invalid Branch):** Request slots for a non-existent Branch ID.
  - *Expect:* HTTP 404, "Branch not found".
- **TC10 (Logic - Operating Hours):** Request slots for a branch that is temporarily closed today (e.g., maintenance).
  - *Expect:* HTTP 200, all slots marked as `isAvailable = false`.

## 3. POST /api/v1/bookings/check-slots-with-suggestions (AI Smart Routing & Load Balancing)

- **TC11 (Happy Path - Under Load):** Branch A current booked capacity is at 40% (< 80% threshold).
  - *Expect:* HTTP 200, `isOverloaded = false`, normal slot list returned. No suggestions.
- **TC12 (Smart Routing - Overloaded):** Branch A capacity is >= 80%. Branch B (5km away) is at 30% capacity.
  - *Expect:* HTTP 200, `isOverloaded = true`, `hasAlternativeSuggestion = true`. Payload includes Branch B details and a 15% incentive voucher code.
- **TC13 (Smart Routing - Gridlock):** Branch A is overloaded (>= 80%). Branch B (nearby) is ALSO overloaded (>= 80%).
  - *Expect:* HTTP 200, `isOverloaded = true`, `hasAlternativeSuggestion = false` (System refuses to suggest another congested branch).
- **TC14 (Logic - Isolation):** Branch C is overloaded, but there are NO other branches within a 15km radius.
  - *Expect:* HTTP 200, `isOverloaded = true`, `hasAlternativeSuggestion = false`.
- **TC15 (Performance):** Perform 50 concurrent checks to trigger the Smart Routing AI simultaneously.
  - *Expect:* HTTP 200, average response time < 500ms (caching geo-spatial calculations).

## 4. POST /api/v1/bookings (Create New Booking)

- **TC16 (Happy Path - Cash):** Submit valid booking (Branch, Services, TimeSlot, LicensePlate) with `PaymentMethod = CASH`.
  - *Expect:* HTTP 200, Booking created with status `Pending`. Branch slot capacity decreases by 1.
- **TC17 (Happy Path - Online):** Submit valid booking with `PaymentMethod = PAYOS`.
  - *Expect:* HTTP 200, Booking created with status `Unpaid`. Returns a `PaymentUrl` for redirection.
- **TC18 (Concurrency - Race Condition):** TimeSlot has exactly 1 slot left. User A and User B submit booking requests at the exact same millisecond.
  - *Expect:* Database Transaction Isolation level allows 1 to succeed (HTTP 200) and 1 to fail (HTTP 409 Conflict, "This time slot just became full").
- **TC19 (Logic - Double Booking):** Attempt to book a vehicle that already has an active `Pending` or `Processing` booking today.
  - *Expect:* HTTP 400, "This vehicle is already scheduled for a wash today".
- **TC20 (Voucher Validation):** Apply a valid Voucher ID explicitly bound to Branch A on a booking at Branch B.
  - *Expect:* HTTP 400, "Voucher is not applicable at this branch".
- **TC21 (Points Logic):** Attempt to apply 5000 points when the user balance only has 1000 points.
  - *Expect:* HTTP 400, "Insufficient loyalty points".
- **TC22 (Validation - Service Constraint):** Submit an empty array of Service IDs `[]`.
  - *Expect:* HTTP 400, "At least one service must be selected".
- **TC23 (Security - RBAC):** Unauthenticated user attempts to create a booking without Auth Header.
  - *Expect:* HTTP 401 Unauthorized.
- **TC24 (Logic - Slot Full):** Attempt to book a TimeSlot that is already at 100% capacity.
  - *Expect:* HTTP 400, "The selected time slot is full".
- **TC25 (Security - Injection):** Inject malicious JS script in the `CustomerNotes` parameter.
  - *Expect:* HTTP 200, script safely sanitized or encoded before saving to DB.

## 5. POST /api/v1/bookings/walk-in (Create Walk-in Booking - Staff Only)

- **TC26 (Happy Path):** Staff creates a walk-in booking for a new customer.
  - *Expect:* HTTP 200, Booking created directly with `CheckedIn` status. Slot capacity decreases by 1 instantly.
- **TC27 (Force Override):** Slot is full (100% capacity), but Manager uses `forceOverrideCapacity = true` for a VIP walk-in.
  - *Expect:* HTTP 200, Booking created successfully despite capacity limit (Capacity becomes 101%).
- **TC28 (Security - RBAC):** Customer token attempts to call the walk-in endpoint.
  - *Expect:* HTTP 403 Forbidden.
- **TC29 (Logic - Existing Vehicle):** Walk-in for a license plate that already exists in the system under a different User ID.
  - *Expect:* HTTP 200, creates booking but correctly assigns it to the existing owner's history.

## 6. PUT /api/v1/bookings/{id}/cancel (Cancel Booking)

- **TC30 (Happy Path - Early Cancel):** Customer cancels booking > 2 hours before the scheduled time.
  - *Expect:* HTTP 200, status becomes `Cancelled`. Capacity is freed. Used Vouchers and Points are fully refunded.
- **TC31 (Policy Check - Late Cancel):** Customer cancels booking < 2 hours before the scheduled time.
  - *Expect:* HTTP 200, status becomes `Cancelled`. System applies a penalty (e.g., points not refunded or penalty fee flagged for next visit).
- **TC32 (Policy Check - Processing):** Attempt to cancel a booking whose status is `Processing` (Vehicle is currently being washed).
  - *Expect:* HTTP 400, "Cannot cancel an ongoing wash".
- **TC33 (Policy Check - Completed):** Attempt to cancel a `Completed` booking.
  - *Expect:* HTTP 400, "Cannot cancel a completed booking".
- **TC34 (Security - IDOR):** User A attempts to cancel User B's booking by changing the `{id}` in the URL.
  - *Expect:* HTTP 403 Forbidden or 404 Not Found (Data scoping).
- **TC35 (Logic - Already Cancelled):** Call cancel on an already `Cancelled` booking.
  - *Expect:* HTTP 400, "Booking is already cancelled".

## 7. PUT /api/v1/bookings/{id}/reschedule (Reschedule Booking)

- **TC36 (Happy Path):** Customer reschedules to a new, available time slot on the same day.
  - *Expect:* HTTP 200, old slot capacity is restored, new slot capacity is decremented.
- **TC37 (Negative - Slot Full):** Attempt to reschedule to a slot that is 100% full.
  - *Expect:* HTTP 400, "The target time slot is full".
- **TC38 (Policy Check):** Attempt to reschedule a booking that is currently `Processing`.
  - *Expect:* HTTP 400, "Cannot reschedule an ongoing wash".
- **TC39 (Policy Check - Cancelled):** Attempt to reschedule a `Cancelled` booking.
  - *Expect:* HTTP 400, "Cannot reschedule a cancelled booking".
- **TC40 (Security - IDOR):** Attempt to reschedule someone else's booking.
  - *Expect:* HTTP 403 Forbidden.

## 8. POST /api/v1/bookings/{id}/accept-relocation (Accept Smart Relocation)

- **TC41 (Happy Path):** Customer received a smart routing suggestion and accepts it.
  - *Expect:* HTTP 200, `BranchId` is updated to the new branch, and the incentive voucher is automatically applied to reduce the `FinalAmount`.
- **TC42 (Logic - Expiration):** Customer attempts to accept relocation 3 hours after the suggestion was made (suggestion expired).
  - *Expect:* HTTP 400, "Relocation offer has expired".
- **TC43 (Logic - Destination Full):** Customer accepts relocation, but the destination branch just became full in the last 5 minutes.
  - *Expect:* HTTP 409 Conflict, "The suggested branch is now full, please retain your current booking".
- **TC44 (Negative - Invalid Status):** Accept relocation for an already `Cancelled` or `Completed` booking.
  - *Expect:* HTTP 400, "Invalid booking status for relocation".

## 9. GET /api/v1/bookings/me & GET /api/v1/bookings/{id} (Read Bookings)

- **TC45 (Happy Path - GET me):** Retrieve paginated history of own bookings.
  - *Expect:* HTTP 200, sorted by `CreatedAt` descending.
- **TC46 (Filter - Status):** GET me with query `status=Pending`.
  - *Expect:* HTTP 200, strictly returns only Pending bookings.
- **TC47 (Happy Path - GET id):** Retrieve specific booking details.
  - *Expect:* HTTP 200, payload contains embedded array of `Services`, `Payments`, and applied `Vouchers`.
- **TC48 (Security - IDOR):** Attempt to GET details of another user's booking ID.
  - *Expect:* HTTP 403/404.

## 10. POST /api/v1/bookings/{id}/payment-link & GET /api/v1/bookings/{id}/payment-status

- **TC49 (Happy Path - Gen Link):** Request a new payment link for an `Unpaid` booking.
  - *Expect:* HTTP 200, returns fresh PayOS/VNPay URL.
- **TC50 (Logic - Already Paid):** Request payment link for a `Completed` (paid) booking.
  - *Expect:* HTTP 400, "This booking has already been paid".
- **TC51 (Happy Path - Status Check):** Poll payment status after bank transfer.
  - *Expect:* HTTP 200, returns `status = Completed`.
- **TC52 (Logic - Cancelled):** Request payment link for a `Cancelled` booking.
  - *Expect:* HTTP 400, "Cannot pay for a cancelled booking".
# Deep Dive: Vehicle & Voucher - Exhaustive Test Cases

This document provides a highly detailed, exhaustive list of test cases for the `VehicleController` and `VoucherController`. It focuses on cross-user data isolation (IDOR protection), business logic constraints, AI integration limits, and concurrency in voucher redemptions.

---

## 1. VehicleController (Personal Vehicle Management)

### 1.1. POST /api/v1/vehicle (Add New Vehicle)
- **TC01 (Happy Path):** Submit a valid, previously unregistered license plate (e.g., `51G-123.45`).
  - *Expect:* HTTP 200, vehicle successfully linked to the user's account.
- **TC02 (Logic - Duplicate Plate):** Submit a license plate that is already registered under the SAME user account.
  - *Expect:* HTTP 409 Conflict, "You have already registered this vehicle".
- **TC03 (Security - Cross-User Conflict):** Submit a license plate that is already registered under a DIFFERENT user account.
  - *Expect:* HTTP 409 Conflict, "This license plate is registered to another user. Please contact Admin".
- **TC04 (Validation - Format):** Submit a heavily malformed license plate (e.g., `invalid-plate!@#`).
  - *Expect:* HTTP 400, "Invalid license plate format".
- **TC05 (Logic - Max Limit):** A user attempts to add a 6th vehicle (assuming the system limit is 5 personal vehicles per user).
  - *Expect:* HTTP 400, "You have reached the maximum limit of registered vehicles".
- **TC06 (Security - Injection):** Inject HTML/Script tags into the `Brand` or `Color` fields.
  - *Expect:* HTTP 200, payload safely sanitized before DB insertion.

### 1.2. PUT /api/v1/vehicle/{licensePlate} (Update Vehicle Info)
- **TC07 (Happy Path):** Update the `Color` and `Model` of a vehicle owned by the user.
  - *Expect:* HTTP 200, DB successfully updated.
- **TC08 (Security - IDOR):** Attempt to update a vehicle owned by another user by passing their license plate in the URL.
  - *Expect:* HTTP 403 Forbidden, "You do not have permission to modify this vehicle".
- **TC09 (Validation):** Attempt to update the `ImageUrl` with an invalid URL string.
  - *Expect:* HTTP 400, validation error.
- **TC10 (Logic - Not Found):** Attempt to update a license plate that does not exist in the database.
  - *Expect:* HTTP 404, "Vehicle not found".

### 1.3. DELETE /api/v1/vehicle/{licensePlate} (Remove Vehicle)
- **TC11 (Happy Path):** Delete a vehicle that has no active bookings.
  - *Expect:* HTTP 200, vehicle unlinked or soft-deleted.
- **TC12 (Logic - Active Bookings):** Attempt to delete a vehicle that currently has a `Pending` or `Processing` booking.
  - *Expect:* HTTP 400, "Cannot delete a vehicle that is currently scheduled for a wash".
- **TC13 (Logic - Historical Integrity):** Delete a vehicle that has old, `Completed` bookings.
  - *Expect:* HTTP 200, vehicle unlinked from the user profile, but the historical Wash Logs retain the vehicle data for reporting purposes.
- **TC14 (Security - IDOR):** Attempt to delete a vehicle owned by another user.
  - *Expect:* HTTP 403 Forbidden.

### 1.4. GET /api/v1/vehicle (List My Vehicles)
- **TC15 (Happy Path):** Retrieve the user's vehicles.
  - *Expect:* HTTP 200, returns array of vehicles.
- **TC16 (Logic - Empty List):** Newly registered user retrieves vehicles.
  - *Expect:* HTTP 200, returns empty array `[]`.
- **TC17 (Performance):** User with a long history of deleted vehicles calls GET.
  - *Expect:* HTTP 200, explicitly filters out soft-deleted vehicles, returning only active ones.

### 1.5. GET /api/v1/vehicle/recognize/{licensePlate} (AI Old Vehicle Recognition)
- **TC18 (Happy Path):** Input a license plate of a known vehicle (e.g., a customer who walked in before).
  - *Expect:* HTTP 200, AI automatically maps the string to DB and returns `brand`, `model`, and `vehicleType` (e.g., Sedan).
- **TC19 (Logic - Unknown Plate):** Input a completely new license plate never seen by the system.
  - *Expect:* HTTP 404, "Vehicle not recognized", prompting the staff/user to enter details manually.
- **TC20 (Security - SQLi):** Inject SQL into the `{licensePlate}` route parameter.
  - *Expect:* HTTP 404 or 400, safely handled.

---

## 2. VoucherController (Customer Promotion Management)

### 2.1. GET /api/v1/voucher/me (My Voucher Wallet)
- **TC21 (Happy Path):** Retrieve list of active, unexpired vouchers.
  - *Expect:* HTTP 200, returns vouchers with `remainingUses > 0`.
- **TC22 (Logic - Filter Expired):** Verify that vouchers past their `ExpirationDate` are NOT returned in the active list (or are explicitly flagged as expired).
  - *Expect:* HTTP 200, expired vouchers are filtered out.
- **TC23 (Logic - Filter Depleted):** Verify that vouchers with `remainingUses = 0` are filtered out.
  - *Expect:* HTTP 200, fully consumed vouchers are hidden.
- **TC24 (Pagination):** Request page 1, size 5.
  - *Expect:* HTTP 200, standard pagination metadata returned.

### 2.2. POST /api/v1/voucher/redeem (Redeem Points for Voucher)
- **TC25 (Happy Path):** User with 5,000 points redeems a voucher that costs 1,000 points.
  - *Expect:* HTTP 200, User's `TotalPoints` decreases to 4,000. Voucher is injected into the user's wallet. Transaction log is created.
- **TC26 (Negative - Insufficient Points):** User with 500 points attempts to redeem a 1,000-point voucher.
  - *Expect:* HTTP 400, "Insufficient loyalty points to redeem this voucher".
- **TC27 (Logic - Out of Stock):** Attempt to redeem a voucher where the global `MaxGlobalUsage` has been reached (e.g., only 100 vouchers available, and 100 have been redeemed).
  - *Expect:* HTTP 400, "This voucher is out of stock".
- **TC28 (Logic - Expired Campaign):** Attempt to redeem a voucher from a campaign that expired yesterday.
  - *Expect:* HTTP 400, "This voucher campaign has ended".
- **TC29 (Logic - Admin Deactivated):** Attempt to redeem a voucher that an Admin manually deactivated prematurely.
  - *Expect:* HTTP 400, "This voucher is no longer active".
- **TC30 (Concurrency - Race Condition):** There is exactly 1 voucher left in stock (`MaxGlobalUsage = 100`, current redemptions = 99). User A and User B submit a redeem request at the exact same millisecond.
  - *Expect:* Database lock ensures only 1 user gets the voucher (HTTP 200), and the other user gets HTTP 400 "Out of stock".
- **TC31 (Concurrency - Points Race Condition):** User has exactly 1,000 points. User submits 5 simultaneous redemption requests for a 1,000-point voucher using multi-threading.
  - *Expect:* DB transaction prevents point manipulation. Only 1 request succeeds, the other 4 return HTTP 400 "Insufficient points".
- **TC32 (Security - IDOR):** Pass another user's ID in the payload to force them to spend points and send the voucher to your wallet.
  - *Expect:* HTTP 200, but the API STRICTLY ignores any injected ID and uses the `UserId` from the JWT Token.
# Phase 2: Customer Booking & Vehicle - Test Cases

This document defines the test cases for the Customer API group: Bookings, Vehicle, and Voucher.

---

## 1. BookingsController (Customer)

### 1.1. POST /api/v1/bookings/check-compatibility (Check Service-Vehicle Compatibility)
- **TC01 (Happy Path):** Select a service suitable for the vehicle type (e.g., Sedan wash for Sedan).
  - *Expect:* HTTP 200, isCompatible = true.
- **TC02 (Negative):** Select a service not applicable to the vehicle (e.g., SUV undercarriage wash for Sedan).
  - *Expect:* HTTP 200, isCompatible = false, with warning "Service not applicable for your vehicle type".

### 1.2. POST /api/v1/bookings/available-slots (Check Available Slots - Legacy)
- **TC01 (Happy Path):** Provide valid branchId and targetDate.
  - *Expect:* HTTP 200, returns a list of time slots for the day along with current capacity.
- **TC02 (Past Date):** Provide a targetDate in the past.
  - *Expect:* HTTP 400, "Cannot check availability for past dates".

### 1.3. POST /api/v1/bookings/check-slots-with-suggestions (AI Smart Slot Checking)
- **TC01 (Happy Path - Under 80% Capacity):** Branch has available slots.
  - *Expect:* HTTP 200, isOverloaded = false, standard list of slots returned.
- **TC02 (Smart Routing - Branch Overloaded):** Selected branch is >= 80% capacity.
  - *Expect:* HTTP 200, isOverloaded = true, hasAlternativeSuggestion = true. Returns suggestedAlternative (nearby branch) and incentiveVoucher (15% discount code).
- **TC03 (Full Capacity):** Branch is completely full and no nearby branches are available.
  - *Expect:* HTTP 200, isOverloaded = true, hasAlternativeSuggestion = false, all slots marked isAvailable = false.

### 1.4. POST /api/v1/bookings (Create New Booking)
- **TC01 (Happy Path):** Provide full branchId, serviceIds, timeSlot, cash payment.
  - *Expect:* HTTP 200, Booking created with Pending status, slot capacity deducted.
- **TC02 (Validation):** The selected TimeSlot was just fully booked by another user (Concurrency).
  - *Expect:* HTTP 400, "This time slot is now full, please choose another time".
- **TC03 (Voucher Check):** Apply a Branch A voucher for a Booking at Branch B.
  - *Expect:* HTTP 400, "This voucher is not applicable to the current branch".
- **TC04 (Points Check):** Attempt to use more loyalty points than currently available.
  - *Expect:* HTTP 400, "Insufficient loyalty points".

### 1.5. POST /api/v1/bookings/walk-in (Create Walk-in Booking)
- **TC01 (Happy Path):** Create a walk-in booking directly at the counter (Staff Role).
  - *Expect:* HTTP 200, booking created successfully with CheckedIn status, immediately occupying the current slot.
- **TC02 (Force Override):** Branch is fully booked but Manager decides to forceOverrideCapacity = true.
  - *Expect:* HTTP 200, booking created successfully despite exceeding max capacity.

### 1.6. GET /api/v1/bookings/me (My Booking History)
- **TC01 (Happy Path):** Call with Customer Token.
  - *Expect:* HTTP 200, returns lists of upcoming and completed bookings for the user.
- **TC02 (Pagination):** Request page 2, 5 items per page.
  - *Expect:* HTTP 200, standard paginated response.

### 1.7. GET /api/v1/bookings/{id} (Booking Details)
- **TC01 (Happy Path):** Retrieve details of own booking.
  - *Expect:* HTTP 200, includes services, pricing, and payment status.
- **TC02 (Security):** Attempt to retrieve booking details of another user.
  - *Expect:* HTTP 403 Forbidden.

### 1.8. PUT /api/v1/bookings/{id}/cancel (Cancel Booking)
- **TC01 (Happy Path):** Cancel at least 2 hours before the scheduled time.
  - *Expect:* HTTP 200, status changed to Cancelled, points/vouchers refunded (if any).
- **TC02 (Policy Check):** Attempt to cancel after the scheduled time or when vehicle is Processing.
  - *Expect:* HTTP 400, "Cannot cancel when the vehicle is already in the facility".

### 1.9. PUT /api/v1/bookings/{id}/reschedule (Reschedule Booking)
- **TC01 (Happy Path):** Reschedule to a new available time slot on the same day.
  - *Expect:* HTTP 200, old slot capacity freed, new slot capacity occupied.
- **TC02 (Negative):** Reschedule to a fully booked time slot.
  - *Expect:* HTTP 400, "The requested time slot is fully booked".

### 1.10. POST /api/v1/bookings/{id}/payment-link (Generate Payment Link)
- **TC01 (Happy Path):** Request payment via VNPay/PayOS.
  - *Expect:* HTTP 200, returns payment gateway URL.
- **TC02 (Logic):** Order is already paid.
  - *Expect:* HTTP 400, "This order has already been paid".

### 1.11. GET /api/v1/bookings/{id}/payment-status (Check Payment Status)
- **TC01 (Happy Path):** Call to verify status after bank transfer.
  - *Expect:* HTTP 200, returns Completed or Unpaid.

### 1.12. POST /api/v1/bookings/{id}/accept-relocation (Accept Smart Relocation)
- **TC01 (Happy Path):** Customer receives overload warning and agrees to switch to the suggested branch.
  - *Expect:* HTTP 200, Booking branchId updated, compensation voucher applied to invoice.
- **TC02 (Negative):** Accept relocation for an already cancelled order.
  - *Expect:* HTTP 400, "Invalid order status for relocation".

---

## 2. VehicleController (Personal Vehicle Management)

### 2.1. GET /api/v1/vehicle (Get My Vehicles)
- **TC01 (Happy Path):** Authenticated user requests vehicle list.
  - *Expect:* HTTP 200, returns array of vehicles (license plate, color, brand, model).

### 2.2. POST /api/v1/vehicle (Add New Vehicle)
- **TC01 (Happy Path):** Input a license plate that does not exist in the system.
  - *Expect:* HTTP 200, vehicle associated with customer profile.
- **TC02 (Conflict):** Input a license plate already registered by someone else.
  - *Expect:* HTTP 409 Conflict, "This license plate is already registered by another account, please contact Admin".

### 2.3. PUT /api/v1/vehicle/{licensePlate} (Update Vehicle Info)
- **TC01 (Happy Path):** Update vehicle color and image.
  - *Expect:* HTTP 200, vehicle details updated.

### 2.4. DELETE /api/v1/vehicle/{licensePlate} (Remove Vehicle)
- **TC01 (Happy Path):** Customer sells vehicle and removes it from account.
  - *Expect:* HTTP 200, vehicle soft-deleted or unlinked from account.
- **TC02 (Negative):** Attempt to remove a vehicle with an active, unfinished booking.
  - *Expect:* HTTP 400, "Cannot remove a vehicle that is currently in service".

### 2.5. GET /api/v1/vehicle/recognize/{licensePlate} (AI Old Vehicle Recognition)
- **TC01 (Happy Path):** Input license plate '51G12345'.
  - *Expect:* HTTP 200, automatically maps string to vehicle type (e.g., Sedan) if it exists in DB.

---

## 3. VoucherController (Customer Promotion Management)

### 3.1. GET /api/v1/voucher/me (My Voucher Wallet)
- **TC01 (Happy Path):** Retrieve list of active vouchers.
  - *Expect:* HTTP 200, displays unexpired vouchers with remaining usage counts.

### 3.2. POST /api/v1/voucher/redeem (Redeem Points for Voucher)
- **TC01 (Happy Path):** User has 5000 points, redeems a voucher costing 1000 points.
  - *Expect:* HTTP 200, deducts 1000 points, new voucher added to user wallet.
- **TC02 (Negative):** User has 500 points, attempts to redeem a 1000-point voucher.
  - *Expect:* HTTP 400, "Insufficient points to redeem this voucher".
- **TC03 (Negative):** Redeem a voucher that has expired or reached redemption limit.
  - *Expect:* HTTP 400, "This voucher is no longer available for redemption".
