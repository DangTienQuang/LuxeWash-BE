# SmartWash API: Missing Negative, Edge & Security Test Cases (Extension)

This document contains exhaustive negative, edge, boundary, and security test cases generated to bridge the system's test coverage to 90%.
These cases strictly complement the core business workflow tests documented in the `PhaseX` files.

**Test Case ID Prefix:** `TC-EXT-`

---

## 🚀 1. Domain-Specific Edge Cases

### 1.1 Concurrency & IoT
- **TC-EXT-001 (Concurrency - Double Check-in):** Two staff members simultaneously send a check-in request for the same `bookingId`.
  - *Expect:* HTTP 200 for the first request, HTTP 409 Conflict (or 400 Bad Request) for the second request indicating the booking is already in progress.
- **TC-EXT-002 (IoT - Phantom Barie Event):** The IoT camera sends a `POST /camera/check-out` for a `licensePlate` that has no active booking.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-003 (IoT - Unpaid Barie Event):** The IoT camera sends a `POST /camera/check-out` for a `licensePlate` whose booking has `FinalAmount > 0` but `PaymentStatus` is `Unpaid`.
  - *Expect:* HTTP 400 Bad Request. The barrier must not open.

### 1.2 B2B Fleet & Financial Limits
- **TC-EXT-004 (B2B - Fleet Credit Limit Exceeded):** A Fleet Manager approves an import batch that would cause the fleet's total monthly wash cost to exceed their predefined credit limit.
  - *Expect:* HTTP 400 Bad Request indicating the credit limit would be breached.
- **TC-EXT-005 (B2B - Disabled Fleet Vehicle):** Attempt to book a wash for a Fleet Vehicle that was deactivated by the admin.
  - *Expect:* HTTP 400 Bad Request indicating the vehicle is not active.

### 1.3 Promotions & Vouchers
- **TC-EXT-006 (Voucher - Expired):** Attempt to apply a voucher that expired 1 minute ago to a new booking.
  - *Expect:* HTTP 400 Bad Request indicating the voucher has expired.
- **TC-EXT-007 (Voucher - Stack Limits):** Attempt to apply two 10% discount vouchers to a single booking when the policy only allows one.
  - *Expect:* HTTP 400 Bad Request indicating voucher stacking is not allowed.
- **TC-EXT-008 (Voucher - Condition Not Met):** Attempt to apply a "10% off for 7-seater vehicles" voucher to a 4-seater vehicle.
  - *Expect:* HTTP 400 Bad Request indicating the voucher conditions are not met.

### 1.4 Rescheduling & Time Slots
- **TC-EXT-009 (Reschedule - Overlapping Slots):** Attempt to reschedule a booking to a time slot that does not have enough remaining `DailySlotCapacity` for the vehicle's `CapacityWeight`.
  - *Expect:* HTTP 400 Bad Request indicating the time slot is full.
- **TC-EXT-010 (Reschedule - Past Time):** Attempt to reschedule a booking to a time slot that is in the past.
  - *Expect:* HTTP 400 Bad Request indicating cannot schedule in the past.

### 1.5 Background Jobs (CRON)
- **TC-EXT-011 (CRON - Idempotency):** Trigger the End-of-Day revenue calculation job twice within the same minute.
  - *Expect:* The second execution should detect the job has already run for today and gracefully skip (or return 200 without double-counting).

---

## 🧪 2. Endpoint-Specific Edge & Validation Cases

### POST register (🔐 Auth)
- **TC-EXT-012 (Validation - Empty Payload):** Submit an empty payload.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-013 (Validation - Invalid Email Format):** Submit an email address missing the `@` symbol.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-014 (Validation - Password Strength):** Submit a password shorter than the required minimum length.
  - *Expect:* HTTP 400 Validation Error.

### POST login (🔐 Auth)
- **TC-EXT-015 (Validation - Empty Payload):** Submit an empty payload.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-016 (Validation - Invalid Email Format):** Submit an email address missing the `@` symbol.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-017 (Validation - Password Strength):** Submit a password shorter than the required minimum length.
  - *Expect:* HTTP 400 Validation Error.

### POST verify-otp (🔐 Auth)
- **TC-EXT-018 (Validation - Missing Required Fields):** Omit required fields relevant to the `AuthController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-019 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-020 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST resend-otp (🔐 Auth)
- **TC-EXT-021 (Validation - Missing Required Fields):** Omit required fields relevant to the `AuthController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-022 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-023 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST refresh-token (🔐 Auth)
- **TC-EXT-024 (Validation - Missing Required Fields):** Omit required fields relevant to the `AuthController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-025 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-026 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST change-password (🔐 Auth)
- **TC-EXT-027 (Validation - Missing Required Fields):** Omit required fields relevant to the `AuthController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-028 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-029 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST logout (🔐 Auth)
- **TC-EXT-030 (Validation - Missing Required Fields):** Omit required fields relevant to the `AuthController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-031 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-032 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST forgot-password (🔐 Auth)
- **TC-EXT-033 (Validation - Missing Required Fields):** Omit required fields relevant to the `AuthController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-034 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-035 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST reset-password (🔐 Auth)
- **TC-EXT-036 (Validation - Missing Required Fields):** Omit required fields relevant to the `AuthController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-037 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-038 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET me (👤 User)
- **TC-EXT-039 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-040 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### PUT me (👤 User)
- **TC-EXT-041 (Validation - Missing Required Fields):** Omit required fields relevant to the `UserController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-042 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-043 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE me (👤 User)
- **TC-EXT-044 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-045 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### POST check-compatibility (📅 Bookings (Customer))
- **TC-EXT-046 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-047 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-048 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST available-slots (📅 Bookings (Customer))
- **TC-EXT-049 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-050 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-051 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST check-slots-with-suggestions (📅 Bookings (Customer))
- **TC-EXT-052 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-053 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-054 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST {bookingId}/trigger-email (📅 Bookings (Customer))
- **TC-EXT-055 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-056 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-057 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST *(root)* (📅 Bookings (Customer))
- **TC-EXT-058 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-059 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-060 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST {id}/payment-link (📅 Bookings (Customer))
- **TC-EXT-061 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-062 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-063 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST walk-in (📅 Bookings (Customer))
- **TC-EXT-064 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-065 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-066 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET me (📅 Bookings (Customer))
- **TC-EXT-067 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-068 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET user/{userId} (📅 Bookings (Customer))
- **TC-EXT-069 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-070 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET {id} (📅 Bookings (Customer))
- **TC-EXT-071 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {id}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-072 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {id} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### PUT {id}/cancel (📅 Bookings (Customer))
- **TC-EXT-073 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-074 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-075 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id}/reschedule (📅 Bookings (Customer))
- **TC-EXT-076 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-077 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-078 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id}/condition (📅 Bookings (Customer))
- **TC-EXT-079 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-080 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-081 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET {id}/payment-status (📅 Bookings (Customer))
- **TC-EXT-082 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {id}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-083 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {id} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### POST {id}/accept-relocation (📅 Bookings (Customer))
- **TC-EXT-084 (Validation - Missing Required Fields):** Omit required fields relevant to the `BookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-085 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-086 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🏢 Branches)
- **TC-EXT-087 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-088 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST register (🏪 Business)
- **TC-EXT-089 (Validation - Empty Payload):** Submit an empty payload.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-090 (Validation - Invalid Email Format):** Submit an email address missing the `@` symbol.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-091 (Validation - Password Strength):** Submit a password shorter than the required minimum length.
  - *Expect:* HTTP 400 Validation Error.

### GET my-profile (🏪 Business)
- **TC-EXT-092 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-093 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST admin/review-application (🏪 Business)
- **TC-EXT-094 (Validation - Missing Required Fields):** Omit required fields relevant to the `BusinessController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-095 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-096 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET admin/pending-applications (🏪 Business)
- **TC-EXT-097 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-098 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET admin/application/{businessProfileId} (🏪 Business)
- **TC-EXT-099 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-100 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET available-slots (🏪 Business)
- **TC-EXT-101 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-102 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST bookings (🏪 Business)
- **TC-EXT-103 (Validation - Missing License Plate):** Submit a payload without a License Plate.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-104 (Logic - Future Date Too Far):** Submit a booking date more than 30 days in the future.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-105 (Validation - Invalid Service IDs):** Submit a booking with a non-existent Service ID.
  - *Expect:* HTTP 400 Validation Error or HTTP 404 Not Found.

### PUT reschedule/{id} (🏪 Business)
- **TC-EXT-106 (Validation - Missing Required Fields):** Omit required fields relevant to the `BusinessController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-107 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-108 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET vehicles (🏪 Business)
- **TC-EXT-109 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-110 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET vehicles/status/all (🏪 Business)
- **TC-EXT-111 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-112 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET vehicles/status (🏪 Business)
- **TC-EXT-113 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-114 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET *(root)* (🏪 Business)
- **TC-EXT-115 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-116 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET {id} (🏪 Business)
- **TC-EXT-117 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {id}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-118 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {id} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### POST {id}/cancel (🏪 Business)
- **TC-EXT-119 (Validation - Missing Required Fields):** Omit required fields relevant to the `BusinessController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-120 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-121 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET invoice/{bookingId} (🏪 Business)
- **TC-EXT-122 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {bookingId}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-123 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {bookingId} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### GET history (🏪 Business)
- **TC-EXT-124 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-125 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET dashboard (🏪 Business)
- **TC-EXT-126 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-127 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET statements/monthly (🏪 Business)
- **TC-EXT-128 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-129 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST washlogs/{washLogId}/assign-lane (🏪 Business)
- **TC-EXT-130 (Validation - Missing Required Fields):** Omit required fields relevant to the `BusinessController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-131 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-132 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET invoices/{invoiceId}/export (🏪 Business)
- **TC-EXT-133 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-134 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST check-in (📷 Camera)
- **TC-EXT-135 (Validation - Missing Required Fields):** Omit required fields relevant to the `CameraController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-136 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-137 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST check-out (📷 Camera)
- **TC-EXT-138 (Validation - Missing Required Fields):** Omit required fields relevant to the `CameraController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-139 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-140 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🚗 Car Models)
- **TC-EXT-141 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-142 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (🚗 Car Models)
- **TC-EXT-143 (Validation - Missing Required Fields):** Omit required fields relevant to the `CarModelsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-144 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-145 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🚗 Car Models)
- **TC-EXT-146 (Validation - Missing Required Fields):** Omit required fields relevant to the `CarModelsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-147 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-148 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE {id} (🚗 Car Models)
- **TC-EXT-149 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-150 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### POST request (🚗 Car Models)
- **TC-EXT-151 (Validation - Missing Required Fields):** Omit required fields relevant to the `CarModelsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-152 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-153 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET ~/api/v1/admin/carmodels/pending (🚗 Car Models)
- **TC-EXT-154 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-155 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### PUT ~/api/v1/admin/carmodels/{id}/approve (🚗 Car Models)
- **TC-EXT-156 (Validation - Missing Required Fields):** Omit required fields relevant to the `CarModelsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-157 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-158 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT ~/api/v1/admin/carmodels/{id}/reject (🚗 Car Models)
- **TC-EXT-159 (Validation - Missing Required Fields):** Omit required fields relevant to the `CarModelsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-160 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-161 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET template (🚐 Fleet)
- **TC-EXT-162 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-163 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST import (🚐 Fleet)
- **TC-EXT-164 (Validation - Missing Required Fields):** Omit required fields relevant to the `FleetController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-165 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-166 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET pending (🚐 Fleet)
- **TC-EXT-167 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-168 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET staff/pending/all (🚐 Fleet)
- **TC-EXT-169 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-170 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST staff/approve/{id} (🚐 Fleet)
- **TC-EXT-171 (Validation - Missing Required Fields):** Omit required fields relevant to the `FleetController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-172 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-173 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST staff/reject/{id} (🚐 Fleet)
- **TC-EXT-174 (Validation - Missing Required Fields):** Omit required fields relevant to the `FleetController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-175 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-176 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET staff/imports (🚐 Fleet)
- **TC-EXT-177 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-178 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET staff/imports/{batchId} (🚐 Fleet)
- **TC-EXT-179 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-180 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST check-in (🚐 Fleet)
- **TC-EXT-181 (Validation - Missing Required Fields):** Omit required fields relevant to the `FleetController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-182 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-183 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST walk-in (🚐 Fleet)
- **TC-EXT-184 (Validation - Missing Required Fields):** Omit required fields relevant to the `FleetController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-185 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-186 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST walk-out/{washLogId} (🚐 Fleet)
- **TC-EXT-187 (Validation - Missing Required Fields):** Omit required fields relevant to the `FleetController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-188 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-189 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST {washLogId}/start-processing (🚐 Fleet)
- **TC-EXT-190 (Validation - Missing Required Fields):** Omit required fields relevant to the `FleetController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-191 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-192 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET current (🚐 Fleet)
- **TC-EXT-193 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-194 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET queue (🚐 Fleet)
- **TC-EXT-195 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-196 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET history (🚐 Fleet)
- **TC-EXT-197 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-198 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET dashboard (🚐 Fleet)
- **TC-EXT-199 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-200 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST checkout/{washLogId} (🚐 Fleet)
- **TC-EXT-201 (Validation - Missing Required Fields):** Omit required fields relevant to the `FleetController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-202 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-203 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🧴 Materials)
- **TC-EXT-204 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-205 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET units (🧴 Materials)
- **TC-EXT-206 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-207 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET *(root)* (🛠️ Services)
- **TC-EXT-208 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-209 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET {id} (🛠️ Services)
- **TC-EXT-210 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {id}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-211 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {id} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### GET *(root)* (🏅 Tier)
- **TC-EXT-212 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-213 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (🏅 Tier)
- **TC-EXT-214 (Validation - Missing Required Fields):** Omit required fields relevant to the `TierController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-215 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-216 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🏅 Tier)
- **TC-EXT-217 (Validation - Missing Required Fields):** Omit required fields relevant to the `TierController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-218 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-219 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🕐 Time Slots)
- **TC-EXT-220 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-221 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (🕐 Time Slots)
- **TC-EXT-222 (Validation - Missing Required Fields):** Omit required fields relevant to the `TimeSlotsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-223 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-224 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🕐 Time Slots)
- **TC-EXT-225 (Validation - Missing Required Fields):** Omit required fields relevant to the `TimeSlotsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-226 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-227 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE {id} (🕐 Time Slots)
- **TC-EXT-228 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-229 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### GET transactions (💳 Transaction)
- **TC-EXT-230 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-231 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET points/history (💳 Transaction)
- **TC-EXT-232 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-233 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET *(root)* (🚘 Vehicle (Customer))
- **TC-EXT-234 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-235 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (🚘 Vehicle (Customer))
- **TC-EXT-236 (Validation - Missing Required Fields):** Omit required fields relevant to the `VehicleController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-237 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-238 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {licensePlate} (🚘 Vehicle (Customer))
- **TC-EXT-239 (Validation - Missing Required Fields):** Omit required fields relevant to the `VehicleController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-240 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-241 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE {licensePlate} (🚘 Vehicle (Customer))
- **TC-EXT-242 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-243 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### GET recognize/{licensePlate} (🚘 Vehicle (Customer))
- **TC-EXT-244 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-245 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET me (🎟️ Voucher (Customer))
- **TC-EXT-246 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-247 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST redeem (🎟️ Voucher (Customer))
- **TC-EXT-248 (Validation - Missing Required Fields):** Omit required fields relevant to the `VoucherController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-249 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-250 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET me (💰 Wallet)
- **TC-EXT-251 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-252 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST top-up (💰 Wallet)
- **TC-EXT-253 (Validation - Missing Required Fields):** Omit required fields relevant to the `WalletController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-254 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-255 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST top-up/callback (💰 Wallet)
- **TC-EXT-256 (Validation - Missing Required Fields):** Omit required fields relevant to the `WalletController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-257 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-258 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET invoices (🧾 Invoice)
- **TC-EXT-259 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-260 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET invoices/{invoiceId} (🧾 Invoice)
- **TC-EXT-261 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-262 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET invoices/{invoiceId}/pdf (🧾 Invoice)
- **TC-EXT-263 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-264 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST billing/monthly (🧾 Invoice)
- **TC-EXT-265 (Validation - Missing Required Fields):** Omit required fields relevant to the `InvoiceController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-266 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-267 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST payos/webhook (💸 Payment)
- **TC-EXT-268 (Validation - Missing Required Fields):** Omit required fields relevant to the `PaymentController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-269 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-270 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST chat (🤖 AI Chatbot)
- **TC-EXT-271 (Validation - Missing Required Fields):** Omit required fields relevant to the `AIChatBotController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-272 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-273 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET recommendation (🤖 AI Chatbot)
- **TC-EXT-274 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-275 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST detect-plate (🚙 Vehicle Detection)
- **TC-EXT-276 (Validation - Missing Required Fields):** Omit required fields relevant to the `VehicleDetectionController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-277 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-278 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST detect-dual-plate (🚙 Vehicle Detection)
- **TC-EXT-279 (Validation - Missing Required Fields):** Omit required fields relevant to the `VehicleDetectionController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-280 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-281 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST car-recognize (🚙 Vehicle Detection)
- **TC-EXT-282 (Validation - Missing Required Fields):** Omit required fields relevant to the `VehicleDetectionController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-283 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-284 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST check-has-car (🚙 Vehicle Detection)
- **TC-EXT-285 (Validation - Missing Required Fields):** Omit required fields relevant to the `VehicleDetectionController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-286 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-287 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST check-in (🤖 Automated Wash)
- **TC-EXT-288 (Validation - Missing Required Fields):** Omit required fields relevant to the `AutomatedWashController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-289 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-290 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET staff (👔 Manager)
- **TC-EXT-291 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-292 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET lanes (👔 Manager)
- **TC-EXT-293 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-294 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET lanes/{laneId}/staff (👔 Manager)
- **TC-EXT-295 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-296 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET timeslots (👔 Manager)
- **TC-EXT-297 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-298 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST lanes (👔 Manager)
- **TC-EXT-299 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-300 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-301 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST timeslots (👔 Manager)
- **TC-EXT-302 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-303 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-304 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST lanes/assign-staff (👔 Manager)
- **TC-EXT-305 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-306 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-307 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE lanes/{laneId}/staff/{staffId} (👔 Manager)
- **TC-EXT-308 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-309 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### GET bookings (👔 Manager)
- **TC-EXT-310 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-311 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST bookings/{bookingId}/checkin-assign (👔 Manager)
- **TC-EXT-312 (Validation - Missing License Plate):** Submit a payload without a License Plate.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-313 (Logic - Future Date Too Far):** Submit a booking date more than 30 days in the future.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-314 (Validation - Invalid Service IDs):** Submit a booking with a non-existent Service ID.
  - *Expect:* HTTP 400 Validation Error or HTTP 404 Not Found.

### PUT lanes/{laneId} (👔 Manager)
- **TC-EXT-315 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-316 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-317 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE lanes/{laneId} (👔 Manager)
- **TC-EXT-318 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-319 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### PUT timeslots/{slotId} (👔 Manager)
- **TC-EXT-320 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-321 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-322 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE timeslots/{slotId} (👔 Manager)
- **TC-EXT-323 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-324 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### DELETE staff/{userId} (👔 Manager)
- **TC-EXT-325 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-326 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### POST check-revenue-stimulus (👔 Manager)
- **TC-EXT-327 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-328 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-329 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET revenue-stimulus/proposals (👔 Manager)
- **TC-EXT-330 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-331 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### PUT revenue-stimulus/proposals/{voucherId} (👔 Manager)
- **TC-EXT-332 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-333 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-334 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST revenue-stimulus/proposals/{voucherId}/approve (👔 Manager)
- **TC-EXT-335 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-336 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-337 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST revenue-stimulus/proposals/{voucherId}/reject (👔 Manager)
- **TC-EXT-338 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-339 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-340 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST revenue-stimulus/comprehensive-proposals (👔 Manager)
- **TC-EXT-341 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-342 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-343 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST branch-overload/scan-and-notify-relocation (👔 Manager)
- **TC-EXT-344 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-345 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-346 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET stocks (📦 Manager Inventory)
- **TC-EXT-347 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-348 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST imports (📦 Manager Inventory)
- **TC-EXT-349 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerInventoryController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-350 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-351 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET batches (📦 Manager Inventory)
- **TC-EXT-352 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-353 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST batches/{id}/discard (📦 Manager Inventory)
- **TC-EXT-354 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerInventoryController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-355 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-356 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST adjustments (📦 Manager Inventory)
- **TC-EXT-357 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerInventoryController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-358 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-359 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET transactions (📦 Manager Inventory)
- **TC-EXT-360 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-361 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET expiring-soon (📦 Manager Inventory)
- **TC-EXT-362 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-363 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET reports/profit (📦 Manager Inventory)
- **TC-EXT-364 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-365 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET extra-usage-requests (📦 Manager Inventory)
- **TC-EXT-366 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-367 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST extra-usage-requests/{id}/approve (📦 Manager Inventory)
- **TC-EXT-368 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerInventoryController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-369 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-370 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST extra-usage-requests/{id}/reject (📦 Manager Inventory)
- **TC-EXT-371 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerInventoryController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-372 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-373 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (⏰ Manager Overtime)
- **TC-EXT-374 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-375 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### PUT {id}/review (⏰ Manager Overtime)
- **TC-EXT-376 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerOvertimeRequestController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-377 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-378 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (📆 Manager Shift Assignment)
- **TC-EXT-379 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-380 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (📆 Manager Shift Assignment)
- **TC-EXT-381 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerShiftAssignmentController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-382 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-383 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (📆 Manager Shift Assignment)
- **TC-EXT-384 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerShiftAssignmentController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-385 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-386 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE {id} (📆 Manager Shift Assignment)
- **TC-EXT-387 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-388 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### GET *(root)* (🔄 Manager Shift Swap)
- **TC-EXT-389 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-390 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### PUT {id}/review (🔄 Manager Shift Swap)
- **TC-EXT-391 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerShiftSwapRequestController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-392 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-393 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🗓️ Manager Work Shift)
- **TC-EXT-394 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-395 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (🗓️ Manager Work Shift)
- **TC-EXT-396 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerWorkShiftController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-397 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-398 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🗓️ Manager Work Shift)
- **TC-EXT-399 (Validation - Missing Required Fields):** Omit required fields relevant to the `ManagerWorkShiftController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-400 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-401 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE {id} (🗓️ Manager Work Shift)
- **TC-EXT-402 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-403 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### GET lane-assignment (🧑‍🔧 Operation Staff)
- **TC-EXT-404 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-405 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET tasks (🧑‍🔧 Operation Staff)
- **TC-EXT-406 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-407 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET /api/v1/staff/tasks/bookings (🧑‍🔧 Operation Staff)
- **TC-EXT-408 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-409 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST lanes/swap (🧑‍🔧 Operation Staff)
- **TC-EXT-410 (Validation - Missing Required Fields):** Omit required fields relevant to the `OperationStaffController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-411 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-412 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST bookings/{bookingId}/checkin (🧑‍🔧 Operation Staff)
- **TC-EXT-413 (Validation - Missing License Plate):** Submit a payload without a License Plate.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-414 (Logic - Future Date Too Far):** Submit a booking date more than 30 days in the future.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-415 (Validation - Invalid Service IDs):** Submit a booking with a non-existent Service ID.
  - *Expect:* HTTP 400 Validation Error or HTTP 404 Not Found.

### PUT bookings/{bookingId}/status (🧑‍🔧 Operation Staff)
- **TC-EXT-416 (Validation - Missing License Plate):** Submit a payload without a License Plate.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-417 (Logic - Future Date Too Far):** Submit a booking date more than 30 days in the future.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-418 (Validation - Invalid Service IDs):** Submit a booking with a non-existent Service ID.
  - *Expect:* HTTP 400 Validation Error or HTTP 404 Not Found.

### GET *(root)* (📋 Staff Bookings)
- **TC-EXT-419 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-420 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### PUT {id}/status (📋 Staff Bookings)
- **TC-EXT-421 (Validation - Missing Required Fields):** Omit required fields relevant to the `StaffBookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-422 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-423 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT status-by-license-plate (📋 Staff Bookings)
- **TC-EXT-424 (Validation - Missing Required Fields):** Omit required fields relevant to the `StaffBookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-425 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-426 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET by-license-plate/{licensePlate} (📋 Staff Bookings)
- **TC-EXT-427 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-428 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### PUT {id}/no-show (📋 Staff Bookings)
- **TC-EXT-429 (Validation - Missing Required Fields):** Omit required fields relevant to the `StaffBookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-430 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-431 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {detailId}/report-mismatch (📋 Staff Bookings)
- **TC-EXT-432 (Validation - Missing Required Fields):** Omit required fields relevant to the `StaffBookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-433 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-434 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST force-cancel (📋 Staff Bookings)
- **TC-EXT-435 (Validation - Missing Required Fields):** Omit required fields relevant to the `StaffBookingsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-436 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-437 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST bookings/{bookingId}/extra (🧴 Staff Material Usage)
- **TC-EXT-438 (Validation - Missing License Plate):** Submit a payload without a License Plate.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-439 (Logic - Future Date Too Far):** Submit a booking date more than 30 days in the future.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-440 (Validation - Invalid Service IDs):** Submit a booking with a non-existent Service ID.
  - *Expect:* HTTP 400 Validation Error or HTTP 404 Not Found.

### GET shifts (🙋 Staff Self Service)
- **TC-EXT-441 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-442 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET overtime-requests (🙋 Staff Self Service)
- **TC-EXT-443 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-444 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST overtime-requests (🙋 Staff Self Service)
- **TC-EXT-445 (Validation - Missing Required Fields):** Omit required fields relevant to the `StaffSelfServiceController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-446 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-447 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET shift-swap-requests (🙋 Staff Self Service)
- **TC-EXT-448 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-449 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST shift-swap-requests (🙋 Staff Self Service)
- **TC-EXT-450 (Validation - Missing Required Fields):** Omit required fields relevant to the `StaffSelfServiceController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-451 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-452 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST consume (🎟️ Staff Vouchers)
- **TC-EXT-453 (Validation - Missing Required Fields):** Omit required fields relevant to the `StaffVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-454 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-455 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🛡️ Admin — Branches)
- **TC-EXT-456 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-457 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET {id} (🛡️ Admin — Branches)
- **TC-EXT-458 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {id}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-459 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {id} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### POST *(root)* (🛡️ Admin — Branches)
- **TC-EXT-460 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminBranchesController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-461 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-462 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🛡️ Admin — Branches)
- **TC-EXT-463 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminBranchesController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-464 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-465 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET {id}/employees (🛡️ Admin — Branches)
- **TC-EXT-466 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {id}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-467 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {id} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### POST *(root)* (🛡️ Admin — Employees)
- **TC-EXT-468 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminEmployeesController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-469 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-470 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id}/transfer (🛡️ Admin — Employees)
- **TC-EXT-471 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminEmployeesController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-472 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-473 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET stocks (🛡️ Admin — Inventory)
- **TC-EXT-474 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-475 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET batches (🛡️ Admin — Inventory)
- **TC-EXT-476 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-477 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET condition-multipliers (🛡️ Admin — Inventory)
- **TC-EXT-478 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-479 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### PUT condition-multipliers/{id} (🛡️ Admin — Inventory)
- **TC-EXT-480 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminInventoryController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-481 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-482 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET reports/profit (🛡️ Admin — Inventory)
- **TC-EXT-483 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-484 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET branches/{branchId}/settings (🛡️ Admin — Inventory)
- **TC-EXT-485 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {branchId}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-486 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {branchId} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### PUT branches/{branchId}/settings (🛡️ Admin — Inventory)
- **TC-EXT-487 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminInventoryController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-488 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-489 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🛡️ Admin — Lanes)
- **TC-EXT-490 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-491 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET {id} (🛡️ Admin — Lanes)
- **TC-EXT-492 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {id}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-493 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {id} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### POST *(root)* (🛡️ Admin — Lanes)
- **TC-EXT-494 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminLanesController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-495 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-496 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST business (🛡️ Admin — Lanes)
- **TC-EXT-497 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminLanesController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-498 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-499 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🛡️ Admin — Lanes)
- **TC-EXT-500 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminLanesController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-501 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-502 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🛡️ Admin — Managers)
- **TC-EXT-503 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-504 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET {id} (🛡️ Admin — Managers)
- **TC-EXT-505 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {id}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-506 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {id} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### POST *(root)* (🛡️ Admin — Managers)
- **TC-EXT-507 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminManageManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-508 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-509 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🛡️ Admin — Managers)
- **TC-EXT-510 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminManageManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-511 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-512 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id}/status (🛡️ Admin — Managers)
- **TC-EXT-513 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminManageManagerController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-514 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-515 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE {id} (🛡️ Admin — Managers)
- **TC-EXT-516 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-517 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### GET *(root)* (🛡️ Admin — Staff)
- **TC-EXT-518 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-519 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET {id} (🛡️ Admin — Staff)
- **TC-EXT-520 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {id}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-521 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {id} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### POST *(root)* (🛡️ Admin — Staff)
- **TC-EXT-522 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminManageStaffController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-523 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-524 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🛡️ Admin — Staff)
- **TC-EXT-525 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminManageStaffController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-526 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-527 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id}/status (🛡️ Admin — Staff)
- **TC-EXT-528 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminManageStaffController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-529 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-530 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE {id} (🛡️ Admin — Staff)
- **TC-EXT-531 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-532 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### GET *(root)* (🛡️ Admin — Materials)
- **TC-EXT-533 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-534 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (🛡️ Admin — Materials)
- **TC-EXT-535 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminMaterialsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-536 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-537 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🛡️ Admin — Materials)
- **TC-EXT-538 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminMaterialsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-539 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-540 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🛡️ Admin — Material Units)
- **TC-EXT-541 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-542 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (🛡️ Admin — Material Units)
- **TC-EXT-543 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminMaterialUnitsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-544 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-545 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🛡️ Admin — Material Units)
- **TC-EXT-546 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminMaterialUnitsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-547 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-548 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET evaluate-branch/{branchId} (🛡️ Admin — Revenue Analytics)
- **TC-EXT-549 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {branchId}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-550 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {branchId} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### POST trigger-campaign/{branchId} (🛡️ Admin — Revenue Analytics)
- **TC-EXT-551 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminRevenueAnalyticsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-552 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-553 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST trigger-all-campaigns (🛡️ Admin — Revenue Analytics)
- **TC-EXT-554 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminRevenueAnalyticsController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-555 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-556 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🛡️ Admin — Service Material Usage)
- **TC-EXT-557 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-558 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (🛡️ Admin — Service Material Usage)
- **TC-EXT-559 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminServiceMaterialUsageController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-560 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-561 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {usageId} (🛡️ Admin — Service Material Usage)
- **TC-EXT-562 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminServiceMaterialUsageController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-563 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-564 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET *(root)* (🛡️ Admin — Services)
- **TC-EXT-565 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-566 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (🛡️ Admin — Services)
- **TC-EXT-567 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminServicesController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-568 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-569 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🛡️ Admin — Services)
- **TC-EXT-570 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminServicesController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-571 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-572 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE {id} (🛡️ Admin — Services)
- **TC-EXT-573 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-574 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### GET *(root)* (🛡️ Admin — Users)
- **TC-EXT-575 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-576 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET {id} (🛡️ Admin — Users)
- **TC-EXT-577 (Logic - Non-existent ID):** Request the resource with a valid-formatted but non-existent {id}.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-578 (Validation - Invalid ID Format):** Request the resource with an improperly formatted {id} (e.g., string instead of UUID/Int).
  - *Expect:* HTTP 400 Bad Request.

### PUT {id}/status (🛡️ Admin — Users)
- **TC-EXT-579 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminUserController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-580 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-581 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST sync-points (🛡️ Admin — Users)
- **TC-EXT-582 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminUserController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-583 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-584 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### GET other-types (🛡️ Admin — Vehicles)
- **TC-EXT-585 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-586 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### PUT {licensePlate}/type (🛡️ Admin — Vehicles)
- **TC-EXT-587 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVehicleController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-588 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-589 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST {licensePlate}/approve-new-type (🛡️ Admin — Vehicles)
- **TC-EXT-590 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVehicleController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-591 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-592 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST {licensePlate}/reject-new-type (🛡️ Admin — Vehicles)
- **TC-EXT-593 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVehicleController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-594 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-595 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST *(root)* (🛡️ Admin — Vehicle Types)
- **TC-EXT-596 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVehicleTypeController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-597 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-598 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🛡️ Admin — Vehicle Types)
- **TC-EXT-599 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVehicleTypeController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-600 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-601 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE {id} (🛡️ Admin — Vehicle Types)
- **TC-EXT-602 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-603 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### GET *(root)* (🛡️ Admin — Vehicle Types)
- **TC-EXT-604 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-605 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### GET *(root)* (🛡️ Admin — Vouchers)
- **TC-EXT-606 (Validation - Invalid Filters):** Pass incompatible or malformed query parameters.
  - *Expect:* HTTP 400 Bad Request.
- **TC-EXT-607 (Edge - Empty Result Set):** Query with filters that match no records.
  - *Expect:* HTTP 200 OK with an empty array `[]`.

### POST *(root)* (🛡️ Admin — Vouchers)
- **TC-EXT-608 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-609 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-610 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### PUT {id} (🛡️ Admin — Vouchers)
- **TC-EXT-611 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-612 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-613 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### DELETE {id} (🛡️ Admin — Vouchers)
- **TC-EXT-614 (Logic - Delete Non-existent):** Attempt to delete a resource that does not exist.
  - *Expect:* HTTP 404 Not Found.
- **TC-EXT-615 (Logic - Delete Dependent Resource):** Attempt to delete a resource that has foreign key dependencies (e.g., deleting a Service used in active Bookings).
  - *Expect:* HTTP 409 Conflict or HTTP 400 Bad Request with a clear message.

### POST {id}/grant (🛡️ Admin — Vouchers)
- **TC-EXT-616 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-617 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-618 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST birthday (🛡️ Admin — Vouchers)
- **TC-EXT-619 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-620 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-621 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST age (🛡️ Admin — Vouchers)
- **TC-EXT-622 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-623 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-624 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST winback (🛡️ Admin — Vouchers)
- **TC-EXT-625 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-626 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-627 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST vip (🛡️ Admin — Vouchers)
- **TC-EXT-628 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-629 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-630 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST milestone (🛡️ Admin — Vouchers)
- **TC-EXT-631 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-632 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-633 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST process-campaigns (🛡️ Admin — Vouchers)
- **TC-EXT-634 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-635 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-636 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST trigger-weather (🛡️ Admin — Vouchers)
- **TC-EXT-637 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-638 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-639 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

### POST simulate-weather (🛡️ Admin — Vouchers)
- **TC-EXT-640 (Validation - Missing Required Fields):** Omit required fields relevant to the `AdminVouchersController` domain in the request body.
  - *Expect:* HTTP 400 Validation Error.
- **TC-EXT-641 (Logic - Concurrent Modifications):** Submit two simultaneous requests to modify the same resource.
  - *Expect:* Proper concurrency handling (e.g., HTTP 409 Conflict).
- **TC-EXT-642 (Boundary - Max Length Exceeded):** Submit string fields exceeding the maximum allowed database length (e.g., > 255 characters).
  - *Expect:* HTTP 400 Validation Error.

## ⚙️ 7. System-Wide API Boundary & Security Matrix

To achieve robust >90% coverage and prevent redundant test case definitions, **the following core security, edge, and boundary scenarios MUST be executed against ALL APIs** across the system.

Automated test suites (e.g., xUnit or Postman) should inject these cases into a global test fixture that iterates through the `endpoints.md` routing table.

### 7.1 Global Authentication & Authorization (Applies to all protected endpoints)
- **TC-EXT-MAT-001 (Security - No Token):** Call endpoint with no `Authorization` header. *Expect:* HTTP 401 Unauthorized.
- **TC-EXT-MAT-002 (Security - Expired Token):** Call endpoint with a JWT expired 1 minute ago. *Expect:* HTTP 401 Unauthorized.
- **TC-EXT-MAT-003 (Security - Invalid Signature):** Call endpoint with a forged JWT signature. *Expect:* HTTP 401 Unauthorized.
- **TC-EXT-MAT-004 (Security - Account Suspended):** Call endpoint using a valid token belonging to an account with `Status = Suspended`. *Expect:* HTTP 403 Forbidden.
- **TC-EXT-MAT-005 (Security - Account Deleted):** Call endpoint using a token belonging to a soft-deleted account. *Expect:* HTTP 404 / 401.

### 7.2 Global Input Validation & Injection (Applies to all POST/PUT/PATCH endpoints)
- **TC-EXT-MAT-006 (Validation - Empty Payload):** Submit `{}` as the JSON body for endpoints expecting DTOs. *Expect:* HTTP 400 Bad Request.
- **TC-EXT-MAT-007 (Boundary - Massive Payload):** Submit a JSON payload > 10MB to test max request body size limits. *Expect:* HTTP 413 Payload Too Large.
- **TC-EXT-MAT-008 (Security - SQL Injection):** Inject `' OR 1=1; DROP TABLE Users; --` into string/text fields and URL parameters. *Expect:* HTTP 400 or securely sanitized (HTTP 200).
- **TC-EXT-MAT-009 (Security - XSS Payload):** Inject `<script>alert(document.cookie)</script>` into all textual input fields. *Expect:* HTTP 400 or securely HTML-encoded output.
- **TC-EXT-MAT-010 (Boundary - String Overflow):** Submit strings > 10,000 characters for standard text inputs. *Expect:* HTTP 400 Validation Error.
- **TC-EXT-MAT-011 (Boundary - Integer Overflow):** Submit `99999999999999999` for standard integer fields. *Expect:* HTTP 400 Validation Error.

### 7.3 Global Routing & Method Restrictions
- **TC-EXT-MAT-012 (Validation - Invalid Method):** Call a `POST` endpoint using `GET` or `PUT`. *Expect:* HTTP 405 Method Not Allowed.
- **TC-EXT-MAT-013 (Security - Rate Limiting):** Fire 100 requests per second to a single endpoint. *Expect:* HTTP 429 Too Many Requests.
- **TC-EXT-MAT-014 (Validation - Trailing Slash):** Call endpoint with a trailing slash (e.g., `/api/v1/users/`). *Expect:* HTTP 200 or 308 Redirect (ensure router normalizes it).

### 7.4 Global Parameter & Pagination Validation (Applies to all GET endpoints)
- **TC-EXT-MAT-015 (Validation - Negative Pagination):** Request `?page=-1&size=-10`. *Expect:* HTTP 400 Bad Request.
- **TC-EXT-MAT-016 (Boundary - Max Pagination):** Request `?page=1&size=1000000` to prevent memory exhaustion. *Expect:* HTTP 400 'Page size exceeds maximum limit'.
- **TC-EXT-MAT-017 (Validation - Invalid Sort Key):** Request `?sort=DropTable`. *Expect:* HTTP 400 Bad Request.

### 7.5 Global Insecure Direct Object Reference (IDOR) Protection
- **TC-EXT-MAT-018 (Security - Cross User Data):** Fetch/Update/Delete an Entity ID belonging to User B using User A's token. *Expect:* HTTP 403 Forbidden or 404 Not Found.
- **TC-EXT-MAT-019 (Security - Cross Branch Data):** Manager of Branch A attempts to modify configurations/inventory of Branch B. *Expect:* HTTP 403 / 404.
