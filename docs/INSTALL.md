# Installation & Running Guide

## Prerequisites

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9) (to build from source)
- .NET 9 Runtime (included with the SDK)

---

## Option A — Full install (recommended, blocks for real)

This registers the service as SYSTEM so it survives reboots and never prompts UAC.

**1. Open an elevated PowerShell prompt** (right-click → "Run as administrator")

**2. Clone and build:**
```powershell
git clone https://github.com/upizs/EarnedScreen.git
cd EarnedScreen
```

**3. Install the service:**
```powershell
.\scripts\install-service.ps1
```

This builds the service in Release mode, registers it as `EarnedScreen`, and starts it.
Streaming sites are blocked immediately.

**4. Launch the desktop app** (no admin needed — run this as your normal user):
```powershell
dotnet run --project src\EarnedScreen.App
```
Or open `EarnedScreen.slnx` in Visual Studio and run `EarnedScreen.App`.

**5. Earn your session:**
- The gateway checklist appears. Tick every item honestly.
- Click **"Earn Screen Time"** — the block lifts for one session.
- After the timer, the Guillotine re-blocks and the cool-down lock appears.

---

## Option B — Run the service manually (dev / testing, no admin)

The service runs fine as a console app without admin, using a sandbox hosts file.
The real hosts file is **not touched**.

```powershell
$env:EARNEDSCREEN_HOSTS = "$env:TEMP\earnedscreen-dev-hosts"
$env:EARNEDSCREEN_DATA  = "$env:TEMP\earnedscreen-dev-data"

# Create a dummy hosts file
Set-Content $env:EARNEDSCREEN_HOSTS "127.0.0.1 localhost`r`n"

# Run the service (console mode)
dotnet run --project src\EarnedScreen.Service

# In a separate terminal, run the app:
dotnet run --project src\EarnedScreen.App
```

The app connects over the named pipe exactly as in production.
To test the Guillotine without waiting an hour, edit the sandbox `settings.json`
(auto-created in `$env:TEMP\earnedscreen-dev-data`) and set `"SessionMinutes": 1`.

---

## Option C — Automated smoke test (no admin, no UI)

Exercises the full lifecycle (block → unlock → Guillotine → daily limit) against a
temporary sandbox and prints PASS/FAIL for each step.

```powershell
# Build first
dotnet build

# Run the smoke test
.\scripts\smoke-test.ps1
```

Expected output:
```
[PASS]  Default state blocks the sandbox hosts file
[PASS]  Unlock succeeds
[PASS]  Status is Unlocked (1)
[PASS]  Hosts file is unblocked
[PASS]  Second unlock reports active session
Waiting up to 80s for the Guillotine...
[PASS]  Guillotine re-blocked the hosts file
[PASS]  Daily limit refuses a new session

SMOKE TEST: ALL PASSED
```

---

## Uninstall

```powershell
# Elevated PowerShell
.\scripts\uninstall-service.ps1
```

Stops and removes the service, strips the EarnedScreen block from the real hosts file,
and flushes DNS — streaming is restored immediately.

---

## Opening in Visual Studio

1. Open `EarnedScreen.slnx` in Visual Studio 2022 (v17.9+).
2. Set `EarnedScreen.Service` as the startup project to run/debug the service.
3. Set `EarnedScreen.App` to run/debug the WPF client.
4. To debug both together: right-click the solution → **Set Startup Projects** → Multiple.

> The service needs admin to write the real hosts file. In VS, either run VS elevated or
> set the `EARNEDSCREEN_HOSTS` / `EARNEDSCREEN_DATA` env vars (Project Properties → Debug)
> to use a sandbox path.

---

## Settings (Claude-Code-only)

After first run, settings are created at:
```
C:\ProgramData\EarnedScreen\settings.json
```

Fields:
| Field | Default | Meaning |
| --- | --- | --- |
| `SessionMinutes` | 60 | Length of an earned session |
| `SessionsPerDay` | 1 | Sessions allowed per calendar day |
| `BlockedDomains` | Netflix, YouTube, Hulu, Disney+, Twitch, Prime, Max | Hosts entries |
| `GatewayChecklist` | Push-ups, sit-ups, squats, jumping jacks, chores | Pre-watch toll |
| `CoolDownChecklist` | Push-ups, squats, water, stretch | Post-session recovery |
| `Notion` | disabled | Optional Notion daily-tasks gateway (see below) |
| `DnsFilter` | Family, enabled | Always-on family-safe DNS + network UI lock (see below) |

**These are edited via Claude Code only.** See [CLAUDE.md](../CLAUDE.md).

---

## Family-safe DNS + network lock

While the service runs, your active adapter's DNS is pinned to **CleanBrowsing Family**
(`185.228.168.168` / `185.228.169.168`) and the Windows network-settings UI is hidden/locked so it
can't be changed. There's **no off switch** — it's always on. `uninstall-service.ps1` fully reverts it
(DNS back to automatic, network settings UI restored).

Configure via the `DnsFilter` block in `settings.json`:
```json
"DnsFilter": {
  "Enabled": true,
  "Filter": "Family",
  "Servers": ["185.228.168.168", "185.228.169.168"],
  "ServersV6": ["2a0d:2a00:1::", "2a0d:2a00:2::"],
  "LockNetworkUi": true
}
```
- `Filter`: `Family` (default), `Adult`, `Security`, or `Custom` (then set `Servers` yourself).
- Requires the browser DoH policy (set automatically) so browsers don't bypass the adapter DNS.

> **Note:** locking the network pages also hides the Wi-Fi-connect UI. To connect to a new network,
> run `uninstall-service.ps1`, connect, then reinstall — or set `"LockNetworkUi": false` to keep the
> DNS pinned without hiding the pages.

---

## Notion daily-tasks gateway (optional)

When enabled, the gateway also pulls your **open tasks due today** from the Notion **My Tasks**
database and makes them required (alongside the workouts). Completing them and earning the session
marks them **Done** in Notion. If Notion is unreachable or unconfigured, the static checklist still
works and a "Notion tasklist not found" note appears — you're never locked out by a Notion issue.

**Setup:**
1. Create an internal integration at <https://www.notion.so/profile/integrations> and copy its secret.
2. Open the **My Tasks** database in Notion → **•••** → **Connections** → add your integration.
3. Edit `C:\ProgramData\EarnedScreen\settings.json` (via Claude Code) and fill in the `Notion` block:
   ```json
   "Notion": {
     "Enabled": true,
     "Token": "ntn_xxx_your_secret",
     "TasksDatabaseId": "f87718af-a132-498f-8549-78e5c8fcd826",
     "TitleProperty": "Task name",
     "DueProperty": "Due",
     "StatusProperty": "Status"
   }
   ```
4. Relaunch `EarnedScreen.App`. Today's open tasks appear as required checkboxes.
