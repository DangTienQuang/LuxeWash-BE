# LuxeWash-
Smart Automated Car Wash Management System with Advanced Booking &amp; Loyalty Program

## ESP32 barrier device

The barrier controller polls the backend over HTTPS. Configure these environment
variables on the deployed API (double underscores map to .NET configuration
sections):

```text
BarrierDevice__DeviceId=luxewash-branch-1
BarrierDevice__DeviceKey=<long-random-secret>
BarrierDevice__BranchId=1
BarrierDevice__OfflineAfterSeconds=20
```

`DeviceId` and `DeviceKey` must match the values in the ESP32 `secrets.h` file.
The device uses `/api/v1/barrier/device/*`; Staff uses authenticated
`/api/v1/barrier/commands` and `/api/v1/barrier/device/status` endpoints.
