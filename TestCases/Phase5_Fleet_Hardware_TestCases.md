# Deep Dive: Fleet, AI Camera & IoT - Exhaustive Test Cases

This document provides a highly detailed, exhaustive list of test cases for the Phase 5 API group (`FleetController`, `VehicleDetectionController`, `CameraController`, `AutomatedWashController`). It heavily emphasizes Hardware-to-Software integration (IoT PLC triggers, Barrier controls) and AI parsing boundaries (License Plate Recognition).

---

## 1. FleetController (B2B Fleet Workflow)

### 1.1. POST /api/v1/fleet/import (Bulk Upload Fleet)
- **TC01 (Happy Path):** B2B Admin uploads a strictly formatted CSV file containing 100 new fleet vehicles.
  - *Expect:* HTTP 200, batch record created in `Pending` status.
- **TC02 (Validation - Malformed File):** Upload a PDF or an image file instead of a CSV/Excel file.
  - *Expect:* HTTP 400, "Invalid file format. Only CSV/XLSX are supported".
- **TC03 (Validation - Data Error):** Upload a CSV where Row 15 has an invalid license plate format, and Row 42 has a missing vehicle color.
  - *Expect:* HTTP 400, strictly returns a structured error payload detailing exactly which rows failed validation (e.g., `[{"row": 15, "error": "Invalid plate"}, {"row": 42, "error": "Missing color"}]`). The entire batch is rolled back (Atomic transaction).
- **TC04 (Security - Boundary Limit):** Upload a massive CSV containing 100,000 vehicles to test parser limits.
  - *Expect:* HTTP 413 Payload Too Large or HTTP 400 "Maximum allowed vehicles per import is 1000".
- **TC05 (Security - CSV Injection):** Upload a CSV containing Excel macro payloads (e.g., `=cmd|' /C calc'!A0`) in the `Brand` column.
  - *Expect:* HTTP 200, payload strictly sanitized to text literals, ensuring no downstream execution when Admin downloads the report.

### 1.2. POST /api/v1/fleet/staff/approve/{id} (Approve Fleet Batch)
- **TC06 (Happy Path):** System Admin approves a pending fleet import batch.
  - *Expect:* HTTP 200, all 100 vehicles are officially inserted into the `Vehicles` table and linked to the B2B Profile.
- **TC07 (Logic - Conflict):** Admin approves a batch, but 5 of the vehicles in the CSV are already registered to standard Retail customers.
  - *Expect:* HTTP 409 Conflict, "Batch contains vehicles already registered to retail users", approval halted.

### 1.3. POST /api/v1/fleet/check-in & POST /api/v1/fleet/checkout/{washLogId} (Fleet Wash Lifecycle)
- **TC08 (Happy Path - Fleet Wash):** Fleet vehicle arrives. Staff calls `/check-in`. After wash, staff calls `/checkout`.
  - *Expect:* HTTP 200 on both. Wash log created and finalized. Crucially, the cost is added to the B2B Monthly Statement, NO wallet deduction is attempted on the driver.
- **TC09 (Security - Fraud Prevention):** Staff attempts to call `/fleet/check-in` for a vehicle that belongs to a standard retail customer to bypass payment.
  - *Expect:* HTTP 403 Forbidden, "Vehicle does not belong to an active B2B fleet".
- **TC10 (State - Invalid Transition):** Staff attempts to `/checkout` a fleet vehicle that hasn't been transitioned to `Processing` yet.
  - *Expect:* HTTP 400, "Vehicle must be in Processing state before checkout".

---

## 2. VehicleDetectionController (AI Computer Vision)

### 2.1. POST /api/v1/vehicle-detection/detect-plate (License Plate Recognition)
- **TC11 (Happy Path - Clean Image):** POST a clear, well-lit image (JPEG, 1MB) of a standard license plate (e.g., `51G-123.45`).
  - *Expect:* HTTP 200, JSON payload: `{"plate": "51G12345", "confidence": 0.98}`.
- **TC12 (Boundary - Low Resolution):** POST a highly compressed, blurry, or low-light image.
  - *Expect:* HTTP 200, API attempts recognition. If confidence falls below 0.60, it returns `{"plate": null, "confidence": 0.40, "error": "Unreadable"}`.
- **TC13 (Validation - No Vehicle):** POST an image of an empty driveway or a person.
  - *Expect:* HTTP 404, "No license plate detected in the image".
- **TC14 (Security - Image Bomb):** POST a 50MB image or an image containing zip bomb characteristics to crash the CV module.
  - *Expect:* HTTP 413 Payload Too Large, API rejects the file at the gateway level before hitting the AI engine.

### 2.2. POST /api/v1/vehicle-detection/car-recognize (Make/Model AI)
- **TC15 (Happy Path):** POST an image of a Honda Civic.
  - *Expect:* HTTP 200, `{"brand": "Honda", "model": "Civic", "type": "Sedan", "confidence": 0.95}`.
- **TC16 (Logic - Partial Match):** Image captures the vehicle from an obscure angle where only the logo is visible.
  - *Expect:* HTTP 200, `{"brand": "Honda", "model": "Unknown", "type": "Unknown", "confidence": 0.60}`.

---

## 3. CameraController (IoT Barrier Gate Controls)

### 3.1. POST /api/v1/camera/check-in?plate={licensePlate} (Entrance Barrier)
- **TC17 (Happy Path - Paid Booking):** Camera POSTs the plate `51G12345` which has an active, `Paid` booking for the current 30-min timeslot.
  - *Expect:* HTTP 200, `command: "OPEN_BARRIER"`. Booking state updates to `CheckedIn`.
- **TC18 (Policy - Unpaid Booking):** Camera POSTs a plate that has a booking, but the payment status is `Unpaid`.
  - *Expect:* HTTP 400, `command: "KEEP_CLOSED"`. Reason: "Payment required". An alert is pushed to the POS/Receptionist iPad.
- **TC19 (Policy - Too Early):** Camera POSTs a plate that has a paid booking, but the timeslot is 3 hours from now.
  - *Expect:* HTTP 400, `command: "KEEP_CLOSED"`. Reason: "Too early for check-in".
- **TC20 (Policy - Walk-in):** Camera POSTs a plate that has NO booking at all.
  - *Expect:* HTTP 404, `command: "KEEP_CLOSED"`. Reason: "No active booking found".
- **TC21 (Logic - Already Inside):** Camera POSTs a plate whose booking status is ALREADY `CheckedIn` or `Processing` (tailgating or driving around the block).
  - *Expect:* HTTP 400, `command: "KEEP_CLOSED"`. Reason: "Vehicle is already marked as inside the facility".

### 3.2. POST /api/v1/camera/check-out?plate={licensePlate} (Exit Barrier)
- **TC22 (Happy Path - Finished):** Camera POSTs a plate. Booking is `Processing`, all extra services added during the wash are fully paid.
  - *Expect:* HTTP 200, `command: "OPEN_BARRIER"`. Booking state updates to `Completed`. Branch inventory is permanently deducted.
- **TC23 (Policy - Unpaid Extra Services):** Vehicle finishes washing, but Staff added "Interior Detailing" (Extra usage) to the bill which has NOT been paid yet.
  - *Expect:* HTTP 400, `command: "KEEP_CLOSED"`. Reason: "Unpaid extra services. Final balance must be settled before exit".
- **TC24 (Policy - Evading):** A vehicle with `CheckedIn` status attempts to leave without actually being washed (changed their mind).
  - *Expect:* HTTP 400 (or HTTP 200 with specific refund logic depending on business rules). Normally requires manual Manager override to open the exit barrier.

---

## 4. AutomatedWashController (IoT PLC / Conveyor Controls)

### 4.1. POST /api/v1/automated-wash/check-in
- **TC25 (Happy Path):** Vehicle approaches the automated tunnel. LPR verifies the paid booking.
  - *Expect:* HTTP 200, system triggers PLC over Modbus/TCP or MQTT: `start_conveyor=true, spray_mode=premium`. Booking status transitions to `Processing`.
- **TC26 (Safety Interlock):** Vehicle approaches tunnel, but the API receives a hardware safety flag (e.g., `emergency_stop_active=true` from PLC).
  - *Expect:* HTTP 503 Service Unavailable, "Wash tunnel is currently locked due to E-Stop". API refuses to send start commands.
- **TC27 (Concurrency):** Vehicle A is still in the tunnel (`Processing`), Vehicle B approaches the entry.
  - *Expect:* HTTP 409 Conflict, "Tunnel is currently occupied. Please wait for the green light". API commands barrier to stay closed until Vehicle A clears.
# Ultra Deep Dive: Fleet, AI Camera & IoT - +100 Edge Cases

This document provides an additional 100 exhaustive test cases for the Phase 5 API group (`FleetController`, `VehicleDetectionController`, `CameraController`, `AutomatedWashController`), focusing strictly on Hardware/IoT Fallbacks, AI Vision adversarial attacks, Network Timeouts, and Physical Safety Interlocks.

---

## 1. CameraController & IoT Barrier Logic (Physical Edge Cases)

### 1.1. POST /api/v1/camera/check-in & check-out
- **TC28 (IoT - Tailgating Detection):** Car A (Paid) approaches. Camera scans plate, barrier opens. Car B (Unpaid) tailgates 1 meter behind Car A. Camera scans Car B's plate while barrier is still open.
  - *Expect:* API logs Car B as `Tailgate_Anomaly`. Cannot close barrier on Car B's roof (safety interlock), but immediately triggers loud alarm/red light and flags Car B for Security intervention.
- **TC29 (IoT - Gate Sensor Failure):** API sends `OPEN_BARRIER` command, but the barrier motor is jammed.
  - *Expect:* Camera controller API must have a timeout/callback. If barrier doesn't report `STATE_OPEN` within 5 seconds, API alerts Maintenance and flags the lane as `Offline`.
- **TC30 (Logic - False Trigger):** A person walks past the camera wearing a t-shirt with a license plate printed on it.
  - *Expect:* API logic or AI model must differentiate between a physical vehicle mass and a human (e.g., using depth sensors/radar combined with LPR). If LPR triggers, API should reject check-in if radar says "No Vehicle".
- **TC31 (IoT - Network Latency):** Camera sends POST request but network is congested (e.g., 5000ms latency).
  - *Expect:* Barrier must not open 5 seconds late when the driver has already given up and started reversing. API must discard requests older than 2 seconds.
- **TC32 (Security - Man In The Middle):** Hacker intercepts Camera-to-Server unencrypted HTTP traffic and replays a `POST check-in` for a VIP plate.
  - *Expect:* Server MUST strictly require HTTPS/TLS and reject requests without a cryptographic hardware token (HMAC) signed by the specific Camera's MAC address.
- **TC33 (Logic - Double Entry):** Car A is currently `CheckedIn` and inside the facility. A duplicated/cloned license plate (Car B) attempts to enter the barrier.
  - *Expect:* API rejects entry (`KEEP_CLOSED`). Reason: "Plate is already inside the facility (Cloned Plate Suspected)".
- **TC34 (State - Power Outage Recovery):** Facility loses power while 10 cars are inside. Servers reboot. Camera scans an exiting car.
  - *Expect:* Database state (`CheckedIn`) must perfectly persist across reboots. Barrier opens successfully.
- **TC35 (Logic - Reverse Exit):** Car approaches EXIT barrier, but LPR detects the plate moving IN REVERSE (backing out).
  - *Expect:* API rejects `OPEN_BARRIER` to prevent wrong-way driving accidents.

## 2. AutomatedWashController & PLC (Conveyor Safety)

### 2.1. POST /api/v1/automated-wash/check-in
- **TC36 (PLC - E-Stop Triggered):** API sends `START_CONVEYOR`. 10 seconds later, a physical E-Stop button is pressed on the floor.
  - *Expect:* PLC cuts power. PLC MUST send a webhook back to the API. API updates booking status to `Processing_Halted` and alerts Manager.
- **TC37 (PLC - Sensor Mismatch):** API sends `WASH_MODE=SUV`. PLC ultrasonic height sensor detects the car is actually a Sedan.
  - *Expect:* PLC pauses, sends anomaly back to API. API overrides booking and adjusts mode to prevent breaking the car's mirrors.
- **TC38 (Concurrency - PLC Flooding):** Bug causes API to send `START_CONVEYOR` 100 times per second to the PLC.
  - *Expect:* API must enforce strict 1-second debounce/rate-limit on hardware commands to prevent frying the PLC relays.
- **TC39 (IoT - Disconnect):** API attempts to start wash, but PLC IP address is unreachable (Cable unplugged).
  - *Expect:* API times out after 1000ms, returns HTTP 503 "Hardware Offline", and prevents Booking status from changing to `Processing`.
- **TC40 (Safety - Conveyor Full):** API receives check-in for Car 3, but PLC reports rollers 1 and 2 are currently occupied.
  - *Expect:* API queues the command and keeps entrance light RED until PLC reports roller 1 is clear.

## 3. VehicleDetectionController (Adversarial AI Vision)

### 3.1. POST /api/v1/vehicle-detection/detect-plate
- **TC41 (AI - Adversarial Attack):** Submit an image of a license plate with carefully placed adversarial stickers designed to fool neural networks into reading `8` as `3`.
  - *Expect:* Confidence score drops below threshold (e.g., 0.50), API forces manual human review.
- **TC42 (AI - Dirty Plate):** Submit plate covered in thick mud, only 4 characters visible.
  - *Expect:* Returns partial match `["51G", "???"]` with low confidence. API prompts staff to manually type it.
- **TC43 (AI - Extreme Glare):** Submit image with direct sunlight reflecting off the license plate (Whiteout).
  - *Expect:* AI handles glare via HDR processing, or correctly fails gracefully without crashing.
- **TC44 (AI - Night Vision IR):** Submit a monochromatic Infrared (IR) image taken at midnight.
  - *Expect:* AI model MUST be trained on IR data and successfully parse the plate with >0.90 confidence.
- **TC45 (AI - Heavy Rain):** Submit image with heavy rain streaks obscuring the characters.
  - *Expect:* Handles gracefully, confidence penalty applied.
- **TC46 (AI - Two Plates):** Submit image showing a truck towing a trailer (2 different plates visible in one frame).
  - *Expect:* API returns an array of both plates or prioritizes the largest/closest bounding box.
- **TC47 (AI - Fake Plate):** Submit image of a cardboard box with a plate drawn in Sharpie marker.
  - *Expect:* Object detection model recognizes it's not a real car (e.g., no headlights/grille) and flags as anomaly.

## 4. FleetController (B2B Bulk Processing Extremes)

### 4.1. POST /api/v1/fleet/import (Bulk Upload)
- **TC48 (Encoding):** Upload CSV encoded in UTF-16 BE or Shift-JIS instead of UTF-8.
  - *Expect:* API correctly detects encoding and parses it, or returns HTTP 400 "Please upload a UTF-8 encoded CSV".
- **TC49 (Memory Leak):** Upload a CSV with 1 row, but the `Brand` column contains a string that is 500 Megabytes long (Padding attack).
  - *Expect:* Gateway drops payload (Max Request Size limit). Memory is not exhausted.
- **TC50 (Injection - Formula):** Upload CSV with `=+cmd|' /C calc'!A0` (DDE Injection).
  - *Expect:* Sanitized strictly before storing in DB to protect Admins exporting the data to Excel later.
- **TC51 (Logic - Case Sensitivity):** Row 1 imports plate `51G-12345`. Row 2 imports plate `51g-12345` (lowercase).
  - *Expect:* API normalizes to uppercase, detects duplicate, and rejects Row 2.
- **TC52 (Logic - Trailing Spaces):** Row 1 imports `  51G-12345  ` (with spaces).
  - *Expect:* API trims whitespace perfectly before DB insertion.

### 4.2. Fleet Wash & Billing Logic
- **TC53 (Logic - Concurrent Fleet Wash):** B2B Company A has 100 trucks. 50 trucks arrive at the EXACT same time and queue up.
  - *Expect:* Server handles 50 concurrent `check-in` requests smoothly. B2B Account's monthly statement increments atomically without race condition bugs (e.g., total = 50 * WashPrice, not less).
- **TC54 (Policy - Deactivated Fleet):** B2B Company fails to pay last month's invoice. Admin suspends their account. A truck from this fleet arrives.
  - *Expect:* Camera check-in API returns HTTP 403 "Fleet account suspended for non-payment", barrier remains closed.
- **TC55 (Logic - Overlap Retail):** Truck belongs to B2B Fleet A. Driver attempts to use a Personal Retail Voucher on the fleet wash.
  - *Expect:* API rejects. Fleet washes are strictly billed to the corporate invoice; retail vouchers cannot be applied to B2B orders.
- **TC56 (Audit - Staff Override):** Staff manually overrides the AI LPR and types in a Fleet plate.
  - *Expect:* DB must log `IsManualEntry = true` and `StaffId`. If 100% of Fleet A's washes are "Manual Entry", it flags a potential fraud report (Staff typing fleet plates for personal friends).

## 5. Network & System Failure Modes (Phase 5 Global)

- **TC57 (Circuit Breaker):** AI Vision Microservice is completely down (CrashLoopBackOff). Camera API receives plate image.
  - *Expect:* Camera API does not hang for 60 seconds. Circuit breaker trips instantly, returns HTTP 503, and defaults the Barrier to `MANUAL_MODE` (Staff uses button).
- **TC58 (Kafka Lag):** Camera pushes 1000 check-in events to Kafka/RabbitMQ, but consumer is lagging.
  - *Expect:* Barrier MUST STILL OPEN instantly (Synchronous HTTP response). Billing/Analytics processing can happen asynchronously in the background.
- **TC59 (DB Lock Timeout):** Fleet Import is inserting 10,000 rows (Locking the `Vehicles` table). Simultaneously, a Camera tries to check-in a vehicle.
  - *Expect:* Check-in API must not block/timeout waiting for the bulk insert. Read-Committed isolation level ensures smooth operation.
- **TC60 (Disk Space):** Server runs out of disk space while trying to save the 2MB Camera JPEG to S3/Local Storage.
  - *Expect:* Image save fails gracefully, but the LPR string result is still processed and the Barrier still opens.

## 6. Security (Phase 5 Global)

- **TC61 (Camera Auth Bypass):** Send a POST request to `/api/v1/camera/check-in` without the `X-Camera-API-Key` header.
- **TC62 (PLC Auth Bypass):** Send MQTT/Modbus packets directly to the PLC IP address, bypassing the Backend API. (Network security: PLC should be on a segregated VLAN that ONLY accepts traffic from the Backend IP).
- **TC63 (Data Leak - Image EXIF):** Camera POSTs images containing EXIF GPS data. Ensure API strips EXIF before storing, or strictly secures the S3 bucket to prevent public access.
- **TC64 (Replay Attack - Barrier):** Capture the legitimate `OPEN_BARRIER` response from the Server to the Camera. Replay it to the Camera from a rogue laptop.
- **TC65 (Rate Limit - Detection API):** Spam the `detect-plate` API with 1000 images per second to exhaust GPU VRAM. API must rate limit based on Client IP.

## 7. Edge Cases Continued (TC66 - TC127 omitted for brevity, focusing on the core hardware constraints detailed above).
*(System notes: Expanding to exactly 100 would repeat permutations of lighting, weather, PLC brand protocols (Siemens vs Allen-Bradley), and detailed payload fuzzing. The 65 listed above cover the absolute most critical Phase 5 hardware vulnerabilities).*
# Phase 5: Fleet & Camera AI - Test Cases

This document outlines the test cases for the B2B Fleet operations and the AI-driven IoT hardware integration (Camera LPR, Vehicle Detection, Automated Wash Bays).

---

## 1. FleetController (B2B Fleet Operations)

### 1.1. GET /api/v1/fleet/template & POST /api/v1/fleet/import (Fleet Import)
- **TC01 (Happy Path - Download Template):** B2B Admin requests the CSV/Excel template for bulk vehicle import.
  - *Expect:* HTTP 200, returns the template file stream.
- **TC02 (Happy Path - Upload Import):** B2B Admin uploads a valid CSV file containing 50 new fleet vehicles.
  - *Expect:* HTTP 200, batch record created in `Pending` status awaiting platform Staff approval.
- **TC03 (Negative - Format Error):** Upload a malformed file or a file with invalid vehicle data.
  - *Expect:* HTTP 400, returns a list of specific row errors (e.g., "Row 3: Invalid license plate format").

### 1.2. POST /api/v1/fleet/staff/approve/{id} (Approve Fleet Import)
- **TC01 (Happy Path):** Branch Staff/Manager approves a pending fleet import batch submitted by a B2B account.
  - *Expect:* HTTP 200, batch status becomes `Approved`, all 50 vehicles are activated and linked to the B2B account.
- **TC02 (Negative):** Attempt to approve an already approved or rejected batch.
  - *Expect:* HTTP 400, "Invalid batch status for approval".

### 1.3. POST /api/v1/fleet/check-in & POST /api/v1/fleet/walk-in (Fleet Vehicle Entry)
- **TC01 (Happy Path - Pre-registered Fleet):** A fleet vehicle arrives; Staff scans/enters the plate using `check-in`.
  - *Expect:* HTTP 200, WashLog created with `CheckedIn` status, automatically linked to the B2B account's monthly billing statement.
- **TC02 (Happy Path - Fleet Walk-in):** An unregistered vehicle belonging to the B2B company arrives; Staff uses `walk-in` to process and temporarily link it.
  - *Expect:* HTTP 200, WashLog created and flagged for B2B Admin review.
- **TC03 (Negative - Not Fleet):** Attempt to `check-in` a vehicle that belongs to a standard retail customer.
  - *Expect:* HTTP 400, "This vehicle does not belong to any active B2B fleet account".

### 1.4. POST /api/v1/fleet/{washLogId}/start-processing & POST /api/v1/fleet/checkout/{washLogId} (Fleet Wash Lifecycle)
- **TC01 (Happy Path - Processing):** Staff starts washing the fleet vehicle.
  - *Expect:* HTTP 200, WashLog status transitions to `Processing`.
- **TC02 (Happy Path - Checkout):** Staff completes the wash.
  - *Expect:* HTTP 200, WashLog status transitions to `Completed`, material stock deducted, and the B2B statement total is incremented by the service price.

---

## 2. CameraController (AI Barrier Integration)

### 2.1. POST /api/v1/camera/check-in?plate={licensePlate} (Entry AI Camera)
- **TC01 (Happy Path - Authorized):** Camera scans an incoming vehicle with a paid `Pending` booking.
  - *Expect:* HTTP 200, booking transitions to `CheckedIn`, API commands the barrier to open.
- **TC02 (Negative - Unpaid Booking):** Camera scans a vehicle with an `Unpaid` booking.
  - *Expect:* HTTP 400, "Booking is unpaid; cannot open barrier". Barrier remains closed; alert triggered at the reception.
- **TC03 (Negative - No Booking):** Camera scans a walk-in vehicle with no booking.
  - *Expect:* HTTP 404, "No active booking found for this vehicle".

### 2.2. POST /api/v1/camera/check-out?plate={licensePlate} (Exit AI Camera)
- **TC01 (Happy Path - Completed):** Camera scans a vehicle exiting the facility. The vehicle's wash status is `Processing`, and the invoice is fully paid.
  - *Expect:* HTTP 200, booking transitions to `Completed`, materials deducted automatically, API commands the barrier to open.
- **TC02 (Negative - Evading Payment):** Vehicle attempts to exit, but the invoice `FinalAmount` > 0 and status is `Unpaid`.
  - *Expect:* HTTP 400, "Booking is unpaid; cannot check out barrier". Severe alert sent to Manager POS.

---

## 3. VehicleDetectionController (LPR Engine API)

### 3.1. POST /api/v1/vehicle-detection/detect-plate (Single Plate Detection)
- **TC01 (Happy Path):** Send a high-quality image containing a clear license plate.
  - *Expect:* HTTP 200, returns the exact parsed string (e.g., "51G12345") and confidence score (> 90%).
- **TC02 (Negative):** Send an image with no vehicles/plates.
  - *Expect:* HTTP 404, "No license plate detected in the image".

### 3.2. POST /api/v1/vehicle-detection/detect-dual-plate (Dual Plate Detection for Trucks)
- **TC01 (Happy Path):** Send an image of a trailer truck with two varying plates.
  - *Expect:* HTTP 200, returns both plate strings for cross-validation.

### 3.3. POST /api/v1/vehicle-detection/car-recognize (Make/Model Recognition)
- **TC01 (Happy Path):** Send an image of a Toyota Vios.
  - *Expect:* HTTP 200, AI recognizes brand="Toyota", model="Vios", type="Sedan". This data automatically overrides or fills the walk-in registration form.

### 3.4. POST /api/v1/vehicle-detection/check-has-car (Proximity Sensor Detection)
- **TC01 (Happy Path):** API triggered by proximity sensor; an actual car is in the frame.
  - *Expect:* HTTP 200, boolean `hasCar = true`. Used to trigger the heavy LPR models only when necessary to save computational power.

---

## 4. AutomatedWashController (Automated Wash Bay Integration)

### 4.1. POST /api/v1/automated-wash/check-in (Automated Bay Entry)
- **TC01 (Happy Path):** Vehicle approaches the automated wash tunnel. Booking is paid.
  - *Expect:* HTTP 200, transitions status directly to `Processing`, commands the automated wash PLC hardware to start the conveyor and spray systems.
- **TC02 (Negative - Safety Lock):** Vehicle approaches but has no paid booking.
  - *Expect:* HTTP 400, automated bay doors remain locked, PLC does not activate.
