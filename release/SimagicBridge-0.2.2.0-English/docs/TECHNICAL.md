# Technical notes

## Native transport

`NativeGtNeoExtensionFilter` attaches only to SimHub's existing GT Neo device type `67A11361-13EE-45AE-A461-3E19E14B8F87`. The extension replaces its LED manager for the plugin lifetime, then restores the previous manager on shutdown. Duplicate SDK initialization does not replace or close the owning manager.

`NativeGtNeoManager` uses the `SimagicBaseWheelDriver.GetDevice` factory from the locally installed `BA63Driver.dll`. Its internal signature is accessed through reflection with a specific parameter list. The bridge does not construct HID reports, change force feedback settings or remap wheel inputs.

The native mapper contains 77 physical lights: 10 buttons, 48 encoder-ring LEDs, 15 RPM LEDs and 4 auxiliary lights. This is the driver's physical mapping, not a request to change a user's SimHub channel counts.

The native manager uses a 5000 ms heartbeat timeout, a 1-second reconnect interval and a 1-second forced-refresh interval. An actual disconnect can take longer to be detected with the increased heartbeat tolerance. Normal LED updates still follow SimHub's existing pipeline.

Private SDK APIs make this version sensitive to SimHub updates. No compatibility promise is made for other SimHub versions or wheelbases.

## Legacy API recovery

`NativeMode: false` in the plugin's common settings selects the older SimPro API transport. It uses the separate `GT Neo via SimPro` device with simplified 15/10/4 groups and is substantially slower than native output.

The legacy client talks only to `http://127.0.0.1:4010/simpro/api/v3/`, using device enumeration, selected-profile reads and light-configuration writes. The controller also runs in native mode for device information and recovery of earlier legacy sessions. Native LED frames are not sent through SimPro configuration writes.

Before changing a legacy light configuration, the session stores its original values in:

```text
%LOCALAPPDATA%\SimagicSimHubBridge\restore-lights.json
```

Recovery is restricted to the saved device and selected SimPro profile. If a restore remains pending, select that original profile in SimPro, reconnect the original wheel and use **Restore lights**. The journal is retained until the API confirms restoration. Do not edit or delete it as a way to dismiss an error.

## Release identity

English version 0.2.2 keeps the namespace, assembly filename, device identifiers, plugin settings fields and restore-journal location from 0.2.1. Only display strings, metadata and standalone packaging change; transport timing and mapping remain the same. Never install the English and German DLLs side by side under different filenames.

## Validation limits

The automated tests exercise native mapping and extension lifecycle against the installed SDK, plus the legacy session against a fake client. They do not establish long-session hardware reliability. Hardware testing should cover LED latency, reconnects, car overrides, restart behavior and a full racing session. Record the base name reported by the API rather than inferring it from the owner's description.
