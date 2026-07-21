# Deep Dive: AuthController - Exhaustive Test Cases

This document provides a highly detailed, exhaustive list of test cases for the `AuthController`, aiming for ~90% test coverage. It includes Happy Paths, Negative scenarios, Boundary checks, Business Logic validation, and Security/Penetration testing vectors.

---

## 1. POST /api/v1/auth/register (Register New Account)

- **TC01 (Happy Path):** Submit a completely valid payload (valid phone, valid email, strong password).
  - *Expect:* HTTP 200/201, user profile created in DB, OTP SMS generated.
- **TC02 (Negative):** Submit without the required `phone` field.
  - *Expect:* HTTP 400, validation error "Phone number is required".
- **TC03 (Negative):** Submit without the required `password` field.
  - *Expect:* HTTP 400, validation error "Password is required".
- **TC04 (Boundary):** Submit a password with exactly 6 characters (minimum limit).
  - *Expect:* HTTP 200/201, account created successfully.
- **TC05 (Validation):** Submit a password with 5 characters (below minimum).
  - *Expect:* HTTP 400, validation error "Password must be at least 6 characters".
- **TC06 (Validation):** Submit an invalid phone format (e.g., contains letters or special characters `090abcd123`).
  - *Expect:* HTTP 400, validation error "Invalid phone number format".
- **TC07 (Validation):** Submit an invalid email format (e.g., `user@domain`).
  - *Expect:* HTTP 400, validation error "Invalid email format".
- **TC08 (Conflict):** Submit a phone number that already exists in the system.
  - *Expect:* HTTP 409, "Phone number is already registered".
- **TC09 (Conflict):** Submit an email that already exists in the system.
  - *Expect:* HTTP 409, "Email is already in use".
- **TC10 (Security):** Inject SQL payload into the `email` field (e.g., `' OR 1=1; --`).
  - *Expect:* HTTP 400, validation error, or sanitized safely without executing SQL.

## 2. POST /api/v1/auth/login (User Login)

- **TC11 (Happy Path):** Login with correct registered phone and password.
  - *Expect:* HTTP 200, returns `accessToken` and `refreshToken` payload.
- **TC12 (Negative):** Login with an unregistered phone number.
  - *Expect:* HTTP 404, "Account does not exist".
- **TC13 (Negative):** Login with a registered phone but incorrect password.
  - *Expect:* HTTP 401, "Incorrect password".
- **TC14 (Security - Brute Force):** Attempt login with incorrect password 5 consecutive times.
  - *Expect:* HTTP 403, account temporarily locked for 15 minutes.
- **TC15 (Validation):** Submit payload with empty `phone`.
  - *Expect:* HTTP 400, validation error.
- **TC16 (Validation):** Submit payload with empty `password`.
  - *Expect:* HTTP 400, validation error.
- **TC17 (Logic):** Login to an account that has not verified OTP yet (`IsVerified = false`).
  - *Expect:* HTTP 403, "Account not verified. Please verify your OTP".
- **TC18 (Logic):** Login to a Suspended/Banned account.
  - *Expect:* HTTP 403, "Your account has been suspended. Contact support".
- **TC19 (Logic):** Login to an account marked as Soft Deleted.
  - *Expect:* HTTP 404, "Account does not exist".
- **TC20 (Security):** Inject NoSQL/SQL payload in the `phone` field.
  - *Expect:* HTTP 400, strictly rejected by validation middleware.

## 3. POST /api/v1/auth/verify-otp (Verify OTP)

- **TC21 (Happy Path):** Submit correct OTP and matching phone number.
  - *Expect:* HTTP 200, account `IsVerified` flag set to true.
- **TC22 (Negative):** Submit incorrect OTP.
  - *Expect:* HTTP 400, "Invalid OTP code".
- **TC23 (Logic):** Submit an expired OTP (e.g., generated > 5 minutes ago).
  - *Expect:* HTTP 400, "OTP has expired".
- **TC24 (Logic):** Submit an OTP that has already been used (Double verification).
  - *Expect:* HTTP 400, "OTP has already been consumed".
- **TC25 (Logic):** Attempt to verify an account that is already verified.
  - *Expect:* HTTP 400, "Account is already verified".
- **TC26 (Validation):** Submit payload with empty `otp` field.
  - *Expect:* HTTP 400, validation error.
- **TC27 (Security - Brute Force):** Attempt 10 different incorrect OTPs within 1 minute.
  - *Expect:* HTTP 429 Too Many Requests, IP or Account blocked from verification for a cooldown period.
- **TC28 (Validation):** Submit a non-numeric OTP (e.g., `ABCDEF`).
  - *Expect:* HTTP 400, validation error.
- **TC29 (Negative):** Submit OTP for a phone number not found in the database.
  - *Expect:* HTTP 404, "Account not found".
- **TC30 (Boundary):** Submit an OTP with 5 digits (Expected 6).
  - *Expect:* HTTP 400, validation error "OTP must be 6 digits".

## 4. POST /api/v1/auth/resend-otp (Resend OTP)

- **TC31 (Happy Path):** Valid resend request after the cooldown period.
  - *Expect:* HTTP 200, new OTP generated and dispatched.
- **TC32 (Logic - Cooldown):** Request resend immediately after the previous request (< 60s).
  - *Expect:* HTTP 429, "Please wait 60 seconds before requesting a new OTP".
- **TC33 (Negative):** Request resend for an unregistered phone number.
  - *Expect:* HTTP 404, "Account not found".
- **TC34 (Logic):** Request resend for an account that is already verified.
  - *Expect:* HTTP 400, "Account is already verified".
- **TC35 (Security - Rate Limit):** Request resend more than 5 times in a single day.
  - *Expect:* HTTP 429, "Maximum daily OTP requests exceeded".
- **TC36 (Validation):** Submit an empty phone number.
  - *Expect:* HTTP 400, validation error.
- **TC37 (Boundary):** Request resend exactly at the 60th second of the cooldown.
  - *Expect:* HTTP 200, request accepted.
- **TC38 (Security):** Inject script tags in the phone field (XSS attempt).
  - *Expect:* HTTP 400, request rejected by input sanitization.
- **TC39 (Logic):** Request OTP for a Soft-deleted account.
  - *Expect:* HTTP 404, "Account not found".
- **TC40 (Logic):** Request OTP for a Banned account.
  - *Expect:* HTTP 403, "Account is suspended".

## 5. POST /api/v1/auth/refresh-token (Refresh JWT Token)

- **TC41 (Happy Path):** Submit a valid, unexpired Refresh Token (RT).
  - *Expect:* HTTP 200, returns a new `accessToken` and a new `refreshToken`.
- **TC42 (Negative):** Submit an expired RT.
  - *Expect:* HTTP 401, "Token expired. Please login again".
- **TC43 (Security - Tampered):** Submit an RT with an altered signature.
  - *Expect:* HTTP 401, "Invalid token signature".
- **TC44 (Logic - Reuse Detection):** Submit an RT that has already been used (Token Rotation).
  - *Expect:* HTTP 401, security breach detected, ALL active tokens for this user are instantly revoked.
- **TC45 (Validation):** Submit an empty RT.
  - *Expect:* HTTP 400, validation error.
- **TC46 (Security - Revoked):** Submit an RT belonging to a user who just changed their password (Revoked token).
  - *Expect:* HTTP 401, "Token has been revoked".
- **TC47 (Logic):** Submit a string that is not in JWT format.
  - *Expect:* HTTP 401, "Malformed token".
- **TC48 (Security):** Submit an Access Token in the `refreshToken` field.
  - *Expect:* HTTP 401, "Invalid token type".
- **TC49 (Boundary):** Submit an RT that expires in exactly 1 second.
  - *Expect:* HTTP 200, request accepted before expiration.
- **TC50 (Logic):** User was banned while the RT was active; attempt to refresh.
  - *Expect:* HTTP 403, "Account is suspended".

## 6. POST /api/v1/auth/change-password (Change Password)

- **TC51 (Happy Path):** Submit correct current password and valid new password (with Bearer Token).
  - *Expect:* HTTP 200, password changed successfully.
- **TC52 (Negative):** Submit incorrect current password.
  - *Expect:* HTTP 400, "Current password is incorrect".
- **TC53 (Validation):** Submit new password identical to the current password.
  - *Expect:* HTTP 400, "New password must be different from current password".
- **TC54 (Validation):** Submit a new password with 5 characters.
  - *Expect:* HTTP 400, "Password must be at least 6 characters".
- **TC55 (Security):** Call API without an Authorization header.
  - *Expect:* HTTP 401 Unauthorized.
- **TC56 (Security):** Call API with an expired Access Token.
  - *Expect:* HTTP 401 Unauthorized.
- **TC57 (Security):** Call API with a forged Access Token.
  - *Expect:* HTTP 401 Unauthorized.
- **TC58 (Validation):** Missing `newPassword` field in payload.
  - *Expect:* HTTP 400, validation error.
- **TC59 (Security - Injection):** Include HTML/JS tags in the `newPassword` field.
  - *Expect:* HTTP 200 (Hashing algorithms naturally handle special chars, but ensure no execution on display).
- **TC60 (Logic - Session Revocation):** After changing password, attempt to use the OLD Refresh Token on another device.
  - *Expect:* HTTP 401, old refresh token is revoked globally.

## 7. POST /api/v1/auth/logout (User Logout)

- **TC61 (Happy Path):** Valid logout request with active Token.
  - *Expect:* HTTP 200, Refresh Token invalidated in the database.
- **TC62 (Security):** Call API with an invalid or forged Token.
  - *Expect:* HTTP 401 Unauthorized.
- **TC63 (Security):** Call API with an already expired Access Token.
  - *Expect:* HTTP 401 Unauthorized.
- **TC64 (Validation):** Call API without Auth Header.
  - *Expect:* HTTP 401 Unauthorized.
- **TC65 (Logic):** Call Logout twice consecutively with the same token (Double logout).
  - *Expect:* HTTP 200 on first call, HTTP 401 on second call.
- **TC66 (Security):** Attempt to use the Refresh Token immediately after a successful logout.
  - *Expect:* HTTP 401, Token has been revoked.
- **TC67 (Logic):** Provide the Refresh Token in the Auth Header instead of the Access Token.
  - *Expect:* HTTP 401 Unauthorized.
- **TC68 (Concurrency):** Logout from multiple devices simultaneously for the same user.
  - *Expect:* HTTP 200, both sessions are gracefully destroyed without database locks.
- **TC69 (Boundary):** Logout exactly at the moment the Access Token expires.
  - *Expect:* HTTP 401 (if server clock is slightly ahead) or HTTP 200.
- **TC70 (Security):** Attempt to logout a different user's session by manipulating token payload (Signature fails).
  - *Expect:* HTTP 401 Unauthorized.

## 8. POST /api/v1/auth/forgot-password (Forgot Password)

- **TC71 (Happy Path):** Submit a valid, registered phone number.
  - *Expect:* HTTP 200, system generates a reset OTP and dispatches it via SMS.
- **TC72 (Negative):** Submit an unregistered phone number.
  - *Expect:* HTTP 404, "Account does not exist".
- **TC73 (Validation):** Submit an empty phone field.
  - *Expect:* HTTP 400, validation error.
- **TC74 (Validation):** Submit an invalid phone format (`090abc`).
  - *Expect:* HTTP 400, validation error.
- **TC75 (Security - Injection):** Submit SQL payload in the phone field.
  - *Expect:* HTTP 400, validation error.
- **TC76 (Security - Rate Limiting):** Request forgot password 10 times in 1 minute.
  - *Expect:* HTTP 429, "Too many requests. Try again later".
- **TC77 (Logic):** Request forgot password for a Banned account.
  - *Expect:* HTTP 403, "Account is suspended".
- **TC78 (Logic):** Request forgot password for a Soft-deleted account.
  - *Expect:* HTTP 404, "Account does not exist".
- **TC79 (Boundary):** Submit phone number with international country code (e.g., `+84` vs `0`).
  - *Expect:* HTTP 200, system automatically normalizes the phone number.
- **TC80 (Logic):** Request forgot password for an Unverified account (`IsVerified = false`).
  - *Expect:* HTTP 403, "Account must be verified before resetting password".

## 9. POST /api/v1/auth/reset-password (Reset Password)

- **TC81 (Happy Path):** Submit correct Phone, correct Reset OTP, and a valid new password.
  - *Expect:* HTTP 200, password reset successfully.
- **TC82 (Negative):** Submit incorrect Reset OTP.
  - *Expect:* HTTP 400, "Invalid OTP code".
- **TC83 (Negative):** Submit an expired Reset OTP.
  - *Expect:* HTTP 400, "OTP has expired".
- **TC84 (Validation):** Submit a new password that is too short (< 6 chars).
  - *Expect:* HTTP 400, validation error.
- **TC85 (Validation):** Missing OTP field in the payload.
  - *Expect:* HTTP 400, validation error.
- **TC86 (Security - Brute Force):** Attempt 20 incorrect OTP guesses for password reset.
  - *Expect:* HTTP 429, IP/Account blocked from further reset attempts.
- **TC87 (Logic):** Submit a Reset OTP that was already used in a previous successful reset.
  - *Expect:* HTTP 400, "OTP has already been consumed".
- **TC88 (Security):** Inject script tags in the new password field.
  - *Expect:* HTTP 200 (Safely hashed and stored).
- **TC89 (Negative):** Submit Reset OTP for a phone number not found in DB.
  - *Expect:* HTTP 404, "Account not found".
- **TC90 (Logic):** Reset password to the exact same password as the old one.
  - *Expect:* HTTP 400, "New password must be different from the previous password".
# Deep Dive: User, Wallet & Transaction - Exhaustive Test Cases

This document provides a highly detailed, exhaustive list of test cases for the `UserController`, `WalletController`, and `TransactionController`, completing the Deep Dive for Phase 1. It covers strict Role-Based Access Control (RBAC), Idempotency, Concurrency (Race Conditions), and Mass Assignment vulnerabilities.

---

## 1. UserController (User Profile Management)

### 1.1. GET /api/v1/user/me (Get Profile Info)
- **TC01 (Happy Path):** Call API with a valid Customer Token.
  - *Expect:* HTTP 200, returns full profile data (Name, Phone, Points, Tier).
- **TC02 (Security - No Token):** Call API without an Authorization header.
  - *Expect:* HTTP 401 Unauthorized.
- **TC03 (Security - Expired Token):** Call API with an expired JWT.
  - *Expect:* HTTP 401 Unauthorized, "Token expired".
- **TC04 (Security - Forged Token):** Call API with an invalid JWT signature.
  - *Expect:* HTTP 401 Unauthorized.
- **TC05 (Logic - Banned):** Call API with the token of a suspended/banned user.
  - *Expect:* HTTP 403 Forbidden, "Account is suspended".
- **TC06 (Logic - Soft Deleted):** Call API with the token of an account that was recently deleted.
  - *Expect:* HTTP 404, "Account does not exist".
- **TC07 (Logic - Unverified):** Call API using the token of an unverified account.
  - *Expect:* HTTP 200, returns profile data but indicates `isVerified = false`.
- **TC08 (Data Integrity):** Verify that the payload accurately reflects the `TotalPoints` and `MembershipTier` from the DB.
  - *Expect:* HTTP 200, values match database records exactly.
- **TC09 (Security - RBAC):** Call this Customer-specific API using a Manager/Staff Token.
  - *Expect:* HTTP 403 Forbidden, "Access denied. Customer role required".
- **TC10 (Security - SQLi):** Attempt to inject SQL commands into the JWT claims (if dynamically parsed).
  - *Expect:* HTTP 401/403, JWT validation fails safely.

### 1.2. PUT /api/v1/user/me (Update Profile Info)
- **TC11 (Happy Path):** Update `FullName` and `AvatarUrl` with valid data.
  - *Expect:* HTTP 200, data updated successfully.
- **TC12 (Validation):** Submit an empty `FullName` or a name shorter than 2 characters.
  - *Expect:* HTTP 400, validation error "Name is too short".
- **TC13 (Validation):** Submit a `FullName` containing invalid special characters (e.g., `<script>`).
  - *Expect:* HTTP 400, validation error, or sanitized response.
- **TC14 (Security - Mass Assignment):** Inject `{"Role": "Admin"}` into the JSON payload.
  - *Expect:* HTTP 200, update successful BUT the `Role` remains strictly `Customer` (ignores protected fields).
- **TC15 (Security - Mass Assignment):** Inject `{"TotalPoints": 99999}` into the JSON payload.
  - *Expect:* HTTP 200, update successful BUT `TotalPoints` is ignored and remains unchanged.
- **TC16 (Security - No Token):** Call API without Auth Header.
  - *Expect:* HTTP 401 Unauthorized.
- **TC17 (Security - Expired):** Call API with an expired token.
  - *Expect:* HTTP 401 Unauthorized.
- **TC18 (Logic - Idempotency):** Send the exact same update payload twice consecutively.
  - *Expect:* HTTP 200, no actual DB write necessary if data is identical.
- **TC19 (Validation):** Update `AvatarUrl` with an invalid URL format (e.g., `not-a-url`).
  - *Expect:* HTTP 400, validation error "Invalid URL format".
- **TC20 (Logic - Banned):** Attempt to update profile while the account is suspended.
  - *Expect:* HTTP 403 Forbidden.

### 1.3. DELETE /api/v1/user/me (Delete/Deactivate Account)
- **TC21 (Happy Path):** Valid request to delete own account.
  - *Expect:* HTTP 200, account soft-deleted (`IsDeleted = true`), all PII (Personally Identifiable Information) masked or anonymized.
- **TC22 (Logic - Active Bookings):** Attempt to delete account while having a booking with `Pending` or `Processing` status.
  - *Expect:* HTTP 400, "Cannot delete account while there are active bookings".
- **TC23 (Logic - Remaining Balance):** Attempt to delete account while having a positive Wallet Balance (> 0).
  - *Expect:* HTTP 400, "Please withdraw or consume your remaining wallet balance before deleting".
- **TC24 (Security - No Token):** Call API without Auth Header.
  - *Expect:* HTTP 401 Unauthorized.
- **TC25 (Security - Expired Token):** Call API with an expired Token.
  - *Expect:* HTTP 401 Unauthorized.
- **TC26 (Logic - Banned):** Banned user attempts to delete their account to evade tracking.
  - *Expect:* HTTP 403 Forbidden, "Cannot delete a suspended account. Contact support".
- **TC27 (Security - Token Revocation):** Verify that immediately after successful deletion, the active Access Token and Refresh Token are revoked.
  - *Expect:* Subsequent API calls return HTTP 401.
- **TC28 (Logic - Re-login):** Attempt to login immediately after account deletion.
  - *Expect:* HTTP 404, "Account does not exist".
- **TC29 (Logic - Double Delete):** Call DELETE endpoint twice consecutively.
  - *Expect:* HTTP 200 on first call, HTTP 404 or 401 on second call.
- **TC30 (Security - RBAC):** Manager Token attempts to call this Customer endpoint.
  - *Expect:* HTTP 403 Forbidden.

---

## 2. WalletController (Digital Wallet & Top-ups)

### 2.1. GET /api/v1/wallet/me (Get Wallet Balance)
- **TC31 (Happy Path):** Valid token request.
  - *Expect:* HTTP 200, returns current numerical balance.
- **TC32 (Security - No Token):** Request without Auth Header.
  - *Expect:* HTTP 401 Unauthorized.
- **TC33 (Security - Expired Token):** Request with expired JWT.
  - *Expect:* HTTP 401 Unauthorized.
- **TC34 (Logic - New User):** Request balance for an entirely new user with no transaction history.
  - *Expect:* HTTP 200, Balance = 0.
- **TC35 (Logic - Banned):** Banned user checks balance.
  - *Expect:* HTTP 403 Forbidden.
- **TC36 (Data Formatting):** Verify the response includes currency metadata (e.g., `currency: "VND"`).
  - *Expect:* HTTP 200, metadata present.
- **TC37 (Security - Forged Token):** Request with invalid signature.
  - *Expect:* HTTP 401 Unauthorized.
- **TC38 (Performance):** High traffic read operations (100 reads/sec).
  - *Expect:* HTTP 200, response time < 200ms, effectively cached or optimized.
- **TC39 (Data Integrity):** User with a massive balance (e.g., 999,999,999 VND).
  - *Expect:* HTTP 200, no numeric overflow errors, JSON parses correctly.
- **TC40 (Security - RBAC):** Manager attempts to view their "wallet" (if Managers don't have wallets).
  - *Expect:* HTTP 403 Forbidden.

### 2.2. POST /api/v1/wallet/top-up (Create Top-up Order)
- **TC41 (Happy Path):** Request top-up of 100,000 VND.
  - *Expect:* HTTP 200, returns unique `OrderId` and valid Payment Provider URL.
- **TC42 (Validation):** Request top-up amount of 0.
  - *Expect:* HTTP 400, "Amount must be greater than zero".
- **TC43 (Validation):** Request negative top-up amount (-50000).
  - *Expect:* HTTP 400, "Invalid amount".
- **TC44 (Boundary):** Request amount exceeding maximum daily limit (e.g., > 50,000,000 VND).
  - *Expect:* HTTP 400, "Top-up amount exceeds allowable limit".
- **TC45 (Validation):** Request amount with decimals (e.g., 100.50 VND) if system only supports integers.
  - *Expect:* HTTP 400, validation error.
- **TC46 (Security - No Token):** Request without Auth Header.
  - *Expect:* HTTP 401 Unauthorized.
- **TC47 (Logic - Banned):** Banned user attempts to top up.
  - *Expect:* HTTP 403 Forbidden.
- **TC48 (Integration):** Verify the generated PayOS/VNPay URL contains the correct checksum and callback URL.
  - *Expect:* HTTP 200, URL is valid and opens payment gateway.
- **TC49 (Security - SQLi/XSS):** Inject malicious payload into optional metadata fields (if any).
  - *Expect:* HTTP 400 or payload safely sanitized.
- **TC50 (Security - Rate Limiting):** Spam the top-up endpoint 20 times in 10 seconds.
  - *Expect:* HTTP 429 Too Many Requests, to prevent OrderId exhaustion and provider spam.

### 2.3. POST /api/v1/wallet/top-up/callback (Webhook from Payment Provider)
- **TC51 (Happy Path):** Valid `SUCCESS` webhook received with correct cryptographic signature.
  - *Expect:* HTTP 200. User balance incremented. Transaction logged as `Completed`.
- **TC52 (Security - Missing Signature):** Webhook received without the required signature header.
  - *Expect:* HTTP 401/403, transaction rejected.
- **TC53 (Security - Tampered Signature):** Webhook received with an invalid or forged signature.
  - *Expect:* HTTP 401/403, transaction strictly rejected.
- **TC54 (Logic - Invalid OrderId):** Webhook references an `OrderId` that does not exist in the DB.
  - *Expect:* HTTP 404 or HTTP 200 (to ack provider) but transaction is flagged as `Error/Anomaly`.
- **TC55 (Logic - Cancelled):** Webhook status is `CANCELLED` (User aborted payment).
  - *Expect:* HTTP 200. Order marked as `Cancelled`. Wallet balance NOT credited.
- **TC56 (Logic - Failed):** Webhook status is `FAILED` (Bank declined).
  - *Expect:* HTTP 200. Order marked as `Failed`. Wallet balance NOT credited.
- **TC57 (Concurrency - Idempotency):** The exact same `SUCCESS` webhook is sent twice sequentially.
  - *Expect:* HTTP 200 on both, BUT balance is ONLY credited on the first call.
- **TC58 (Concurrency - Race Condition):** The exact same `SUCCESS` webhook is sent 5 times SIMULTANEOUSLY (Multi-threading).
  - *Expect:* HTTP 200. Database locking/Transactions ensure balance is strictly credited ONLY ONCE.
- **TC59 (Security - Payload Injection):** Provider sends XSS payload in the transaction `memo` description.
  - *Expect:* HTTP 200, memo safely sanitized before saving to DB.
- **TC60 (Security - Amount Tampering):** Webhook reports `SUCCESS` for 10,000 VND, but the original OrderId was created for 100,000 VND.
  - *Expect:* HTTP 400/403, transaction flagged for fraud, balance NOT credited.

---

## 3. TransactionController (Transactions & Points History)

### 3.1. GET /api/v1/transaction/transactions (Wallet History)
- **TC61 (Happy Path):** Retrieve list of transactions with default pagination.
  - *Expect:* HTTP 200, returns array of transactions ordered by date descending.
- **TC62 (Security - No Token):** Call without Auth Header.
  - *Expect:* HTTP 401 Unauthorized.
- **TC63 (Validation - Pagination Size):** Request `page=1, size=10`.
  - *Expect:* HTTP 200, returns maximum 10 records.
- **TC64 (Validation - Invalid Page):** Request `page=-1` or `page=0`.
  - *Expect:* HTTP 400, "Page number must be greater than 0".
- **TC65 (Validation - Max Size):** Request `size=1000` (Exceeding max allowed size).
  - *Expect:* HTTP 400, "Page size exceeds maximum limit of 50".
- **TC66 (Filter - Date Range):** Request with valid `fromDate` and `toDate`.
  - *Expect:* HTTP 200, returns transactions strictly within the time window.
- **TC67 (Validation - Date Logic):** Request with `toDate` earlier than `fromDate`.
  - *Expect:* HTTP 400, "End date cannot be earlier than start date".
- **TC68 (Validation - Date Format):** Request with invalid date format (e.g., `YYYY/MM/DD` instead of ISO 8601).
  - *Expect:* HTTP 400, validation error.
- **TC69 (Logic - Empty Result):** User has never made a transaction.
  - *Expect:* HTTP 200, returns empty array `[]` and `totalRecords = 0`.
- **TC70 (Security - Data Leakage):** Attempt to inject another user's ID into the query parameters to view their transactions.
  - *Expect:* HTTP 200, API ignores the injected ID and strictly filters by the `UserId` extracted from the JWT Token.

### 3.2. GET /api/v1/transaction/points/history (Points History)
- **TC71 (Happy Path):** Retrieve loyalty points history.
  - *Expect:* HTTP 200, returns paginated array of point earnings and deductions.
- **TC72 (Security - No Token):** Call without Auth Header.
  - *Expect:* HTTP 401 Unauthorized.
- **TC73 (Pagination):** Request specific page and size, verify metadata (totalPages, currentPage).
  - *Expect:* HTTP 200, pagination metadata is accurate.
- **TC74 (Data Formatting):** Verify payload distinctly labels the `SourceType` (e.g., `EARNED_FROM_WASH`, `DEDUCTED_FOR_VOUCHER`).
  - *Expect:* HTTP 200, `SourceType` and `PointsDelta` (+/-) are clearly defined.
- **TC75 (Logic - Empty Result):** New user with no points history.
  - *Expect:* HTTP 200, returns empty array `[]`.
- **TC76 (Filter - Date Range):** Filter point history by the last 30 days.
  - *Expect:* HTTP 200, returns correct subset of records.
- **TC77 (Security - Forged Token):** Call with invalid JWT signature.
  - *Expect:* HTTP 401 Unauthorized.
- **TC78 (Logic - Banned):** Banned user attempts to view points.
  - *Expect:* HTTP 403 Forbidden.
- **TC79 (Data Integrity - Math Check):** Summing all positive and negative `PointsDelta` in the history should mathematically equal the user's current `TotalPoints`.
  - *Expect:* DB Architecture ensures strict consistency.
- **TC80 (Security - SQLi):** Inject SQL payload into the `page` or `size` query parameters.
  - *Expect:* HTTP 400, request safely rejected as type mismatch (integer expected).
# Phase 1: Core System & Authentication - Test Cases

This document defines the test cases for the core API group: Auth, User, Wallet, and Transaction.

---

## 1. AuthController

### 1.1. POST /api/v1/auth/register (Register New Account)
- **TC01 (Happy Path):** Provide valid information (phone, email, password).
  - *Expect:* HTTP 200, account created successfully, OTP sent via SMS/Email.
- **TC02 (Negative - Duplicate Phone):** Provide an existing phone number.
  - *Expect:* HTTP 400, "Phone number is already registered".
- **TC03 (Negative - Duplicate Email):** Provide an existing email.
  - *Expect:* HTTP 400, "Email is already in use".
- **TC04 (Validation):** Provide a weak password (< 6 characters).
  - *Expect:* HTTP 400, validation error.
- **TC05 (Validation):** Missing phone number (required field).
  - *Expect:* HTTP 400, validation error.

### 1.2. POST /api/v1/auth/login (User Login)
- **TC01 (Happy Path):** Provide valid phone number and password.
  - *Expect:* HTTP 200, returns accessToken and refreshToken.
- **TC02 (Negative):** Provide a non-existent phone number.
  - *Expect:* HTTP 400/404, "Account does not exist".
- **TC03 (Negative):** Provide an incorrect password.
  - *Expect:* HTTP 401, "Incorrect password".
- **TC04 (Account Lock):** Input incorrect password more than 5 times.
  - *Expect:* HTTP 403, account temporarily locked for 15 minutes.
- **TC05 (Unverified Account):** Login to an account that hasn't verified OTP.
  - *Expect:* HTTP 403, requires OTP verification before login.

### 1.3. POST /api/v1/auth/verify-otp (Verify OTP)
- **TC01 (Happy Path):** Provide correct OTP and phone number.
  - *Expect:* HTTP 200, Account IsVerified = true.
- **TC02 (Negative):** Provide incorrect OTP.
  - *Expect:* HTTP 400, "Incorrect OTP code".
- **TC03 (Negative - Expired):** Provide an expired OTP (> 5 minutes).
  - *Expect:* HTTP 400, "OTP code has expired".
- **TC04 (Negative):** Verify an already verified account.
  - *Expect:* HTTP 400, "Account has already been verified".

### 1.4. POST /api/v1/auth/resend-otp (Resend OTP)
- **TC01 (Happy Path):** Valid resend request.
  - *Expect:* HTTP 200, new OTP generated and sent successfully.
- **TC02 (Rate Limit):** Request OTP continuously (< 60s per request).
  - *Expect:* HTTP 429 Too Many Requests, "Please wait 60s before resending".
- **TC03 (Negative):** Phone number does not exist.
  - *Expect:* HTTP 404, account not found.

### 1.5. POST /api/v1/auth/refresh-token (Refresh JWT Token)
- **TC01 (Happy Path):** Provide a valid and unexpired refreshToken.
  - *Expect:* HTTP 200, returns a new token pair.
- **TC02 (Negative):** Provide an expired refreshToken.
  - *Expect:* HTTP 401 Unauthorized, requires re-login.
- **TC03 (Negative):** Provide an invalid or malformed refreshToken.
  - *Expect:* HTTP 401 Unauthorized.
- **TC04 (Revoked):** Token has been revoked (e.g., user just changed password).
  - *Expect:* HTTP 401 Unauthorized.

### 1.6. POST /api/v1/auth/change-password (Change Password)
- **TC01 (Happy Path):** Provide correct current password and valid new password (with JWT Token).
  - *Expect:* HTTP 200, password changed successfully.
- **TC02 (Negative):** Provide incorrect current password.
  - *Expect:* HTTP 400, "Current password is incorrect".
- **TC03 (Validation):** New password matches the current password.
  - *Expect:* HTTP 400, "New password must be different from the current password".
- **TC04 (Security):** Call API without Bearer Token.
  - *Expect:* HTTP 401 Unauthorized.

### 1.7. POST /api/v1/auth/logout (User Logout)
- **TC01 (Happy Path):** Valid logout request with Token.
  - *Expect:* HTTP 200, Refresh Token invalidated in the database.
- **TC02 (Security):** Call API with an invalid Token.
  - *Expect:* HTTP 401 Unauthorized.

### 1.8. POST /api/v1/auth/forgot-password (Forgot Password)
- **TC01 (Happy Path):** Provide a registered phone number.
  - *Expect:* HTTP 200, system generates a reset OTP and sends it via SMS.
- **TC02 (Negative):** Provide an unregistered phone number.
  - *Expect:* HTTP 404, "Account does not exist".

### 1.9. POST /api/v1/auth/reset-password (Reset Password)
- **TC01 (Happy Path):** Provide correct phone number, correct OTP, and new password.
  - *Expect:* HTTP 200, password reset successfully.
- **TC02 (Negative):** Incorrect OTP.
  - *Expect:* HTTP 400, "Invalid OTP code".
- **TC03 (Negative):** Expired OTP.
  - *Expect:* HTTP 400, "OTP code has expired".

---

## 2. UserController (User Management)

### 2.1. GET /api/v1/user/me (Get Profile Info)
- **TC01 (Happy Path):** Call API with valid Token.
  - *Expect:* HTTP 200, returns User Data (Name, Phone, Points, Tier).
- **TC02 (Security):** Call API with the Token of a banned user.
  - *Expect:* HTTP 403 Forbidden.

### 2.2. PUT /api/v1/user/me (Update Profile Info)
- **TC01 (Happy Path):** Update valid name and address.
  - *Expect:* HTTP 200, data updated in DB.
- **TC02 (Security):** Intentionally inject parameters to alter Role or Accumulated Points.
  - *Expect:* HTTP 200, name updated but IGNORES (does not modify) sensitive fields.
- **TC03 (Validation):** Attempt to update phone number (if not permitted by system logic).
  - *Expect:* HTTP 400, "Phone number cannot be changed".

### 2.3. DELETE /api/v1/user/me (Delete/Deactivate Account)
- **TC01 (Happy Path):** User requests account deletion.
  - *Expect:* HTTP 200, Account IsDeleted = true.
- **TC02 (Logic):** Account has unfinished bookings.
  - *Expect:* HTTP 400, "Cannot delete account while there are active bookings".

---

## 3. WalletController (Digital Wallet)

### 3.1. GET /api/v1/wallet/me (Get Wallet Balance)
- **TC01 (Happy Path):** Call API with valid Token.
  - *Expect:* HTTP 200, returns Balance and recent transaction history.
- **TC02 (Edge Case):** New account with no top-up history.
  - *Expect:* HTTP 200, Balance = 0.

### 3.2. POST /api/v1/wallet/top-up (Create Top-up Order)
- **TC01 (Happy Path):** Request top-up of 100,000 VND via VNPay/PayOS.
  - *Expect:* HTTP 200, generates OrderId and returns PaymentUrl.
- **TC02 (Validation):** Request negative amount (-50000) or 0.
  - *Expect:* HTTP 400, "Invalid top-up amount".
- **TC03 (Boundary):** Request amount exceeding daily limit (e.g., 1 billion VND).
  - *Expect:* HTTP 400, "Top-up amount exceeds daily limit".

### 3.3. POST /api/v1/wallet/top-up/callback (Webhook from Payment Provider)
- **TC01 (Happy Path):** Webhook returns SUCCESS with valid checksum signature.
  - *Expect:* HTTP 200. User wallet credited exactly the top-up amount. Transaction logged.
- **TC02 (Security - Fake Signature):** Attacker calls API with SUCCESS payload but incorrect checksum signature.
  - *Expect:* HTTP 400/403, transaction rejected. Balance unchanged.
- **TC03 (Idempotent / Duplicate):** Provider calls the same Webhook twice for 1 order.
  - *Expect:* HTTP 200, BUT balance is CREDITED ONLY ONCE.
- **TC04 (Negative):** Webhook returns CANCELLED status.
  - *Expect:* HTTP 200, logs failed transaction. Balance unchanged.

---

## 4. TransactionController (Transaction & Points History)

### 4.1. GET /api/v1/transaction/transactions (Wallet History)
- **TC01 (Happy Path):** Retrieve list of credit/debit transactions.
  - *Expect:* HTTP 200, transaction array with Pagination metadata.
- **TC02 (Filter):** Filter transactions by Date Range (e.g., last month).
  - *Expect:* HTTP 200, returns transactions within the specified range.
- **TC03 (Pagination):** Request page=1, size=10.
  - *Expect:* HTTP 200, returns maximum 10 records.

### 4.2. GET /api/v1/transaction/points/history (Points History)
- **TC01 (Happy Path):** Retrieve list of loyalty point increments/decrements.
  - *Expect:* HTTP 200, clearly indicates source (Car Wash) or deduction (Voucher Redemption).
- **TC02 (Empty):** Newly created account with zero points.
  - *Expect:* HTTP 200, empty array [].
