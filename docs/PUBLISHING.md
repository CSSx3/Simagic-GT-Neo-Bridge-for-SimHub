# Publish on GitHub

This folder is the standalone repository root. Upload its contents, rather than the surrounding development workspace. The root `.gitignore` excludes build output, local journals and installation backups.

1. Review the README, compatibility limits and MIT license attribution.
2. Build and run the default tests with your installed SimHub 9.13.2. Run `tools\Package.ps1` to create the release ZIP and `SHA256SUMS.txt`.
3. Create a GitHub repository and push this folder's source and documentation.
4. Create an experimental release tagged `v0.2.2`.
5. Attach the release ZIP from this folder's `release` directory. Keep release binaries out of source commits.
6. Use the release notes below and add any new hardware validation you actually performed.

No vendor DLLs, device logs, personal SimHub settings or recovery journals belong in the public repository or release archive.

## Suggested release notes

Simagic Bridge 0.2.2 provides an English interface for GT Neo LED control through an existing wheelbase connection. It uses SimHub's existing GT Neo device and keeps LED profiles and car overrides.

Built and tested against SimHub 9.13.2. Local hardware reports GT Neo firmware 1.4.4 and Alpha EVO Ultra firmware 2.3.6. Alpha U/Ultimate support is not independently confirmed. Long-session reliability and other hardware combinations still need testing.

Close SimHub, copy `User.SimagicBridge.dll` into its installation folder, then enable Simagic Bridge and **Control lights with SimHub**. Use your existing GT Neo under Devices. Do not load two copies of the plugin.
