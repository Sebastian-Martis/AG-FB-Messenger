# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.9] - 2026-01-15
### Fixed
- **Flashing & Reset Fix:** Improved logic to stop the icon from flashing immediately after messages are read.
- **Smart Debouncing:** Implemented a 2-second buffer when checking for "Read" status. This prevents the app from getting confused by Messenger's title toggling (which caused spam), while correctly resetting the counter when you actually open a chat.

## [2.0.8] - 2026-01-15
### Fixed
- **Notification Un-stick:** Fixed a bug where notifications stopped working after the first message. The app now correctly detects when you've opened a chat (reseting the unread counter to 0), enabling subsequent notifications to fire correctly.

## [2.0.7] - 2026-01-15
### Fixed
- **Strict Anti-Spam:** Fixed the "repeated notification" issue by implementing 3-state logic (Read, Unread, Unknown). The app now ignores title changes that don't explicitly increase the unread message count, preventing loops where the title toggles between "Messenger" and sender names.

## [2.0.6] - 2026-01-15
### Improved
- **Smart Notifications:** Notifications now extract sender and message content from the window title (e.g., "John: Hello") instead of just saying "New Message".
- **Anti-Spam:** Fixed an issue where notifications would loop or spam repeatedly. Now alerts only trigger on *new* messages (unread count increase) or distinct new content.

## [2.0.5] - 2026-01-15
### Changed
- Improved Taskbar Flashing: If the application is hidden in the tray, receiving a notification will now automatically minimize it to the taskbar (instead of staying hidden) so the orange flash is visible.

## [2.0.4] - 2026-01-15
### Added
- Added Taskbar Icon Flashing (orange glow) for new notifications. Now both the tray icon and the taskbar button will visually alert the user.

## [2.0.3] - 2026-01-15
### Fixed
- Added robust fallback notification system based on Window Title changes. If Messenger updates the title (e.g., "(1) Messenger"), the app now detects this and triggers the notification and icon flash, regardless of browser permission settings.

## [2.0.2] - 2026-01-15
### Fixed
- Implemented JavaScript notification shim to force-enable notifications from Messenger. This intercepts `window.Notification` calls and relays them to the OS, bypassing potential WebView2 restrictions.

## [2.0.1] - 2026-01-15
### Changed
- Renamed application from "AG Messenger" to "FB-Messenger".
- Renamed installer output to `FB-Messenger-Setup-2.0.1.exe`.

### Fixed
- Fixed issue where single-instance lock prevented reopening the app (now brings window to front).
- Fixed Windows Toast Notifications not appearing (added `NotificationReceived` handler).

## [2.0.0] - 2026-01-15
### Added
- Native .NET 8 (WPF) implementation replacing Electron.
- Windows System Tray support (`TrayIconService`).
- Window state persistence (`WindowStateManager`).
- WebView2 integration for Messenger.
- Single-file installer script (`installer.iss`).

### Removed
- Legacy Electron architecture (Node.js, Electron Forge).
