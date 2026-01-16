# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.29] - 2026-01-16
### Fixed
- **Max-Count Strategy:** Implemented a new counting logic that simultaneously checks the Global Badge, Document Title (`(N)`), and internal conversation lists. The app now uses the **HIGHEST** number found among these sources. This ensures that even if one indicator fails (e.g., Badge says "1"), the correct count from another source (e.g., Title says "(4)") will be respected and displayed in the tray.

## [2.0.28] - 2026-01-16
### Fixed
- **Accurate Burst Counting:** The app now reads the "Global Message Badge" (the red number on the Chats icon) as the definitive source of truth. This correctly counts 5 separate messages from 1 person as "5", whereas previous versions counted them as "1 conversation".
- **Badge Reset Reliability:** Opening the app via the Taskbar now forcefully clears the red notification badge on the tray icon, fixing an issue where it would stay stuck until the tray icon was clicked.
- **Improved Counting Fallback:** If the global badge is missing, the app now intelligently sums up all numbers found in the "unread" labels of your conversation list.

## [2.0.27] - 2026-01-16
### Fixed
- **ARIA/Accessibility Integration:** Added a DOM crawler that scans `aria-label` attributes every 2 seconds. This serves as the "source of truth" for unread messages, bypassing visual/audio limitations of the Messenger web interface. 
- **Burst Message Fix:** The app now correctly identifies "burst" messages (multiple messages from the same sender) by reading the true unread count exposed to screen readers.

## [2.0.26] - 2026-01-16
### Fixed
- **Web Audio API Hook:** Added interception for `AudioBufferSourceNode` (Web Audio API) used by modern applications for sound effects. This runs alongside the HTML5 Audio hook to capture ALL methods of sound generation.
- **Debounce:** Added sophisticated 200ms debounce to prevent double counting if multiple audio APIs are triggered simultaneously.

## [2.0.25] - 2026-01-16
### Fixed
- **Audio-trigged Notifications:** Major fix using Audio API interception to count messages. Even if Messenger doesn't update the title (same sender messages), audio "dings" will now trigger notifications and badge updates reliably.
- **Focus Reset:** Message counters now reset immediately when window gains focus.

## [2.0.24] - 2026-01-16
### Fixed
- **Auto-Update Loop:** Fixed app not starting automatically after update installation.
- **Periodic Checks:** Added automatic update check every 1 hour (previously only on startup).

## [2.0.23] - 2026-01-16
### Fixed
- **Aggressive Message Detection:** Replaced slow DOM monitoring with 500ms title polling that detects EVERY content change.
- **JS-side Counter:** Message counter now runs in JavaScript for better accuracy and synchronization.

## [2.0.22] - 2026-01-16
### Fixed
- **Accurate Message Count:** Added internal message counter that increments on each new message, providing accurate badge count regardless of Messenger's title behavior.

## [2.0.21] - 2026-01-16
### Fixed
- **Auto-Update:** Fixed "Value cannot be null (Parameter 'window')" error when checking for updates before MainWindow loads.

## [2.0.20] - 2026-01-16
### Added
- **Real Unread Count:** Added JavaScript DOM monitoring to track actual unread message count, not just conversation count from title.
- **Accurate Badge:** Tray icon badge now shows the higher of DOM-detected count or title-based count for better accuracy.

## [2.0.19] - 2026-01-16
### Fixed
- **Auto-Update:** Switched update server domain from `.center` to `.pl` to fix SSL certificate mismatch errors.
- **Auto-Update:** Fixed issue where checking for updates from Tray menu did nothing (added thread safety and owner window to dialogs).

## [2.0.18] - 2026-01-16
### Added
- **Badge Icon:** Tray icon now shows unread message count as a red badge with number (instead of warning icon).
- **Dynamic Badge:** Badge updates in real-time as unread count changes.

## [2.0.17] - 2026-01-16
### Changed
- **UI:** Renamed "O firmie" to "O aplikacji" in main menu.
- **UI:** Added "Sprawdź aktualizacje" direct menu option in JaRoD-CENTER menu.

## [2.0.16] - 2026-01-16
### Added
- **Auto-Update System:** Application now automatically checks for updates on startup.
- **Update Checker:** Added "Sprawdź aktualizacje" menu item in System Tray.
- **UpdateService:** Implemented mechanism to fetch `version.json`, download installer, and upgrade automatically.

## [2.0.15] - 2026-01-16
### Fixed
- **Complete Notification Logic Rewrite:** Fixed issue where consecutive messages from same sender didn't trigger notifications. Now uses content-based detection instead of relying only on unread count (Messenger doesn't increment count for same conversation).
- **Sender in Notifications:** Notifications now always show sender name when available.
- **Removed:** "Test powiadomienia" option from tray menu.

## [2.0.14] - 2026-01-16
### Fixed
- **Notification Reliability:** Fixed issue where second message from a sender was not triggering notification if the app was opened (but conversation not entered). Now correctly distinguishes between "seen" count and "confirmed" count.
- **Sender in Notifications:** Notifications now display the sender's name when available, instead of just "Nowa wiadomość" (New Message).
- **Faster Notification Response:** Reduced protection window for specific notifications from 5s to 3s.

## [2.0.13] - 2026-01-15
### Fixed
- **Notification Spam:** Fixed a regression where a single message could trigger dozens of notifications. This was caused by the application reacting too quickly to "title flickering" (where Messenger briefly reports 0 unread messages during updates). The "Read Detection" is now buffered by 2 seconds to ignore these glitches.

## [2.0.12] - 2026-01-15
### Fixed
- **Wrong Sender Fix:** Fixed the bug where the notification displayed the name of the *open conversation* (e.g. "Alice") instead of the person who actually wrote (e.g. "Bob"). The app now strictly differentiates between "Active Chat Names" and "New Message Alerts" (which contain colons or action verbs), defaulting to "New Message" if it's not 100% sure.
- **Notification Protection:** If a specific notification (e.g. "Bob: Hello") is displayed, it is now protected for 5 seconds from being overwritten by a generic "New Message" update, ensuring you have time to read who wrote to you.

## [2.0.11] - 2026-01-15
### Fixed
- **Sender Info:** Restored sender display in notifications. It now intelligently extracts "Name: Message" from the window title, so you know who is writing.
- **Missed Notifications:** Completely overhauled the detection logic. Now, *any* change in message content (e.g., from "John" to "Alice") triggers a notification immediately, even if the "Unread Count" technically didn't increase (e.g. fast switching). This eliminates the "Blind Spot" where some messages were silent.

## [2.0.10] - 2026-01-15
### Fixed
- **Notification Reliability:** Fixed an issue where rapid messages might be ignored. The app now uses advanced heuristics to distinguish between system glitches (flickering titles) and actual new messages arriving quickly.
- **Sender Name Fix:** Fixed a bug where the notification sometimes displayed the name of the *open chat* instead of the sender. Notifications are now always titled "Nowa wiadomość" (New Message) for consistency and accuracy.

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
