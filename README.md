# File Monitoring Service - Full Overview Demo

Lightweight Windows Service that monitors one or more source directories in the background (no UI), then automatically renames each new file or folder with a GUID and moves it to a destination folder.

## Overview

This project contains a production-style .NET Framework Windows Service built in C#.

Core behavior:

- Monitors configured source folders using `FileSystemWatcher`.
- Reacts to created items.
- Waits until files/folders are stable and unlocked.
- Renames each item using a generated GUID to avoid name collisions.
- Moves items to the configured destination folder.
- Writes operational logs with severity and timestamps.

## Project Structure

### Main Solution and Projects

- `FileMonitoringService Project 1/FileMonitoringService Project 1.sln`
- `FileMonitoringService Project 1/` (Windows Service source code)
- `File Monitoring Service Installer/` (Visual Studio setup project for MSI)
- `Presentation/Presentation.pdf`
- `Presentation/Presentation.pptx`

### Source Code (Requested Files)

The full Visual Studio service project includes:

- `FileMonitoringService Project 1/FileMonitoringService.cs`
  - Service orchestrator (`ServiceBase`) with `OnStart` and `OnStop`.
- `FileMonitoringService Project 1/Program.cs`
  - Application entry point.
  - Supports both interactive mode (console test run) and Windows Service mode.
- `FileMonitoringService Project 1/ProjectInstaller.cs`
  - Installer metadata and service registration details.
  - Service name: `Service1`
  - Display name: `File Monitoring Service Project 1`
  - Start type: `Automatic`
  - Account: `LocalSystem`
- `FileMonitoringService Project 1/App.config`
  - Configuration for source folders, destination folder, and log folder.

Additional core classes:

- `FileMonitoringService Project 1/ConfigService.cs` - loads/validates configuration with safe defaults.
- `FileMonitoringService Project 1/FileMonitorHelper.cs` - watcher setup, stability checks, retry logic, move/rename logic.
- `FileMonitoringService Project 1/AppLogger.cs` - centralized file logging (`[INFO]`, `[WARN]`, `[ERROR]`).

## Configuration

Before running, configure these paths:

- Source directories (custom XML section: `SourceDirectories`)
- Destination folder (`DestinationFolder`)
- Log folder (`LogFolder`)

Configuration locations:

- Development/build-time: `FileMonitoringService Project 1/App.config`
- Runtime/installed output:
  - `FileMonitoringService Project 1/bin/Release/FileMonitoringService Project 1.exe.config`
  - Or installed executable config next to the installed `.exe`

If settings are missing or invalid, the service applies defaults:

- Source: `C:\FileMonitoring\Source`
- Destination: `C:\FileMonitoring\Destination`
- Logs: `C:\FileMonitoring\Logs`

Example configuration pattern:

```xml
<configSections>
  <section name="SourceDirectories" type="System.Configuration.NameValueSectionHandler"/>
</configSections>

<SourceDirectories>
  <add key="Directory1" value="C:\FileMonitoring\SourceA" />
  <add key="Directory2" value="C:\FileMonitoring\SourceB" />
</SourceDirectories>

<appSettings>
  <add key="DestinationFolder" value="C:\FileMonitoring\Destination" />
  <add key="LogFolder" value="C:\FileMonitoring\Logs" />
</appSettings>
```

## Architecture

### System Diagram

![System Architecture](System%20Diagrm.png)

### Runtime Components

- Application Entry Point (`Program.cs`)
  - Initializes configuration and helper classes.
  - Runs interactive monitoring when started from console.
  - Runs as Windows Service when started by SCM.
- File Monitoring Service (`FileMonitoringService.cs`)
  - Starts/stops monitoring lifecycle.
- Configuration Service (`ConfigService.cs`)
  - Reads config and normalizes paths.
  - Applies defaults on invalid or missing values.
- File Monitor Helper (`FileMonitorHelper.cs`)
  - Creates one watcher per source folder.
  - Handles created events.
  - Performs file/directory stability checks.
  - Renames and moves items with retries.
- App Logger (`AppLogger.cs`)
  - Writes timestamped logs to the physical log directory.

### FileSystemWatcher Behavior

- Trigger event: `Created`
- Scope: top-level only (`IncludeSubdirectories = false`)
- Stability protection:
  - File readiness loop with retry/delay.
  - Directory quiet-window stability checks.
- Transformation:
  - New name = `GUID + original extension`
- Delivery:
  - Move to destination with retry/backoff for `IOException`/`UnauthorizedAccessException`.

## Logging and Monitoring

Windows Services run in the background, so logging is the primary observability mechanism.

Log output characteristics:

- UTC timestamp per entry
- Severity tags: `[INFO]`, `[WARN]`, `[ERROR]`
- Detailed exception context for troubleshooting

Default log file:

- `MonitorServiceLog.log`

Log folder:

- Configured `LogFolder` path
- Falls back to `C:\FileMonitoring\Logs` when invalid/missing

## Build Instructions (Release Mode)

### Option 1: Visual Studio

1. Open `FileMonitoringService Project 1/FileMonitoringService Project 1.sln`.
2. Set configuration to `Release` and platform to `Any CPU`.
3. Build the solution.
4. Output executable:
   - `FileMonitoringService Project 1/bin/Release/FileMonitoringService Project 1.exe`

### Option 2: Command Line (MSBuild)

From the solution folder:

```powershell
msbuild "FileMonitoringService Project 1.sln" /p:Configuration=Release /p:Platform="Any CPU"
```

Optional (if your environment supports building .NET Framework projects with dotnet tooling):

```powershell
dotnet build "FileMonitoringService Project 1.sln" -c Release
```

## Deployment Instructions

You can deploy using either InstallUtil or MSI setup.

### A) InstallUtil Workflow (Requested)

1. Open an elevated Developer Command Prompt or PowerShell.
2. Change directory to the Release output folder.
3. Install the service:

```powershell
InstallUtil.exe "FileMonitoringService Project 1.exe"
```

4. Start the service:

```powershell
net start Service1
```

Alternative start command:

```powershell
sc start Service1
```

5. Stop the service:

```powershell
net stop Service1
```

6. Uninstall the service:

```powershell
InstallUtil.exe /u "FileMonitoringService Project 1.exe"
```

### B) MSI Workflow

1. Build the installer project in Release:
   - `File Monitoring Service Installer/Release/File Monitoring Service Installer.msi`
2. Run the `.msi` and complete the wizard.
3. Control the service with `net start Service1`, `net stop Service1`, or Windows Services UI.

## Test Log

### Test Example Log Image

![Test Example Log](Test%20Example%20Log.png)

### Sample Log (Multiple Files Scenario)

The following sample reflects the provided test log scenario where multiple events occur, including success and handled failure:

```text
2026-04-10 19:02:11.114 [INFO] Application startup initialized.
2026-04-10 19:02:11.235 [INFO] Configuration loaded successfully.
2026-04-10 19:02:11.460 [INFO] Monitoring started for source: C:\FileMonitoring\SourceA
2026-04-10 19:02:11.463 [INFO] Monitoring started for source: C:\FileMonitoring\SourceB
2026-04-10 19:02:14.912 [INFO] File detected: invoice_001.csv
2026-04-10 19:02:15.188 [INFO] File moved successfully: invoice_001.csv -> C:\FileMonitoring\Destination
2026-04-10 19:02:24.587 [ERROR] Failed to move file: temp_notes.txt | UnauthorizedAccessException: Access denied.
2026-04-10 19:02:25.019 [WARN] Continuing service execution after handled error.
2026-04-10 19:02:39.775 [INFO] Interactive monitoring stopped.
```

## Interactive Test Mode

When launched manually (interactive session), the executable runs monitoring in console mode for quick testing:

1. Start `FileMonitoringService Project 1.exe` directly.
2. The app monitors configured sources.
3. Press Enter to stop.

This mode is useful for validating configuration and logging before service installation.

## Notes

- Target framework: `.NET Framework 4.7.2`
- Service dependencies configured in installer: `RpcSs`, `EventLog`, `LanmanWorkstation`, `Tcpip`
- Presentation assets exist in:
  - `Presentation/Presentation.pdf`
  - `Presentation/Presentation.pptx`
