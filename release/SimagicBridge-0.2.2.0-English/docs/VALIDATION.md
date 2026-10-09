# Validation for 0.2.2

Validated on October 9, 2026, against the locally installed SimHub 9.13.2 SDK:

- English plugin build completed, assembly version `0.2.2.0`.
- Default bridge suite passed **46 assertions**.
- Settings-screen preview rendered and visually reviewed; English labels and instructions fit.
- Release ZIP's DLL bytes match the compiled DLL.
- Source and release packaging exclude installed vendor DLLs, private logs, local settings and recovery journals.
- The fixture contains a placeholder preset identifier and no device identifiers, serial numbers or account credentials.

The SDK tests require write access to SimHub's resolver cache. The test host emits a log4net configuration warning because it has no SimHub application logging configuration; this did not prevent the assertions from passing.

No live wheel test or long-session reliability test was performed for the English release. It retains the native transport behavior of 0.2.1. Preview rendering is a layout check, not a hardware connection test.
