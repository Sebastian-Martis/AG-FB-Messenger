# FB-Messenger (v2.0.9)

A lightweight, native desktop wrapper for Facebook Messenger, built with **.NET 8 WPF** and **WebView2**.

## Features
- 🚀 **Fast Startup:** Native .NET performance.
- 📉 **Low Memory Usage:** Uses system Edge WebView2 instead of bundling Chromium.
- 🔔 **Smart Notifications:**
  - Extracts sender and message content from window title (e.g., "John: Hello").
  - **Taskbar Flashing:** Taskbar button glows orange on new messages.
  - **Tray Integration:** Minimized to tray, flashes on unread.
  - **Anti-Spam:** Intelligent debouncing prevents notification loops.
- 💾 **State Persistence:** Remembers window size and position.
- 📦 **Single-File Installer:** Easy installation via Inno Setup.

## Prerequisites
- **OS:** Windows 10/11 (x64)
- **Runtime:** [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **WebView2:** [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (Usually pre-installed on Windows).

## Development Setup

### Requirements
- Visual Studio 2022 / VS Code
- .NET 8 SDK
- Inno Setup 6 (for building installer)

### Build & Run
```powershell
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run
```

### Create Installer
```powershell
# Publish Release
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish

# Determine Path to ISCC (Inno Setup Compiler)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "installer.iss"
```

## Structure
- `AG-Messenger.csproj`: Main project file.
- `MainWindow.xaml`: Main browser window & notification logic.
- `Services/`: Tray icon and window management.
- `installer.iss`: Inno Setup installer script.
- `Assets/`: Icons and images.

## License
Private / Proprietary.

## Known Issues
- **Notifications:** While significantly improved, notifications may still occasionally behave inconsistently (e.g., repeating or missing sender name) due to the way Facebook Messenger dynamically updates the window title. Work on a perfect heuristic is ongoing.
