# EarnedScreen — project guide for Claude Code

> "The Earned-Screen Protocol": streaming is blocked 24/7. The user earns **one session per day**
> by completing a physical/chore checklist. When the timer runs out, the Guillotine re-blocks the
> network and a mandatory cool-down checklist appears.

## Settings are Claude-Code-only
The real config lives **outside the repo** at `%ProgramData%\EarnedScreen\settings.json` and is
gitignored. **Only edit it on the user's behalf via Claude Code** — that is the whole point: the user
should not casually lower the workout count or the daily limit. The app auto-creates a default file
on first run (`Settings.CreateDefault()` in `src/EarnedScreen.Core/Settings.cs`).

Settings fields:
- `SessionMinutes` — length of an earned session (default **60**).
- `SessionsPerDay` — sessions allowed per day (default **1**).
- `BlockedDomains` — hosts-file block list.
- `GatewayChecklist` — the pre-watch "toll" (workouts + private chores like dishes/laundry).
- `CoolDownChecklist` — the post-session "anti-potato" list.
- `Notion` — optional Notion integration (see below).

Runtime state (do not hand-edit) lives at `%ProgramData%\EarnedScreen\state.json`.

## Notion daily-tasks gateway (optional)
When enabled, the gateway *also* pulls the user's open tasks for **today** from the Notion **My Tasks**
database and makes them mandatory (added to the same "all checked" gate as the workouts). Ticking them
and earning the session marks them **Done** in Notion (write-back happens on unlock, not per-tick).
If Notion is off/unreachable/empty, the static list still works and a small "Notion tasklist not found"
note shows — a Notion failure never blocks earning.

Code: `src/EarnedScreen.Core/NotionTasksClient.cs` (REST, API version `2022-06-28`; discovers the
Status/title schema at runtime so custom property/option names work). UI wiring in
`src/EarnedScreen.App/MainWindow.xaml.cs` (`LoadNotionTasksAsync`).

`Notion` settings block (Claude-Code-only, sits in `settings.json`):
- `Enabled` — master switch (default **false**).
- `Token` — Notion internal integration secret.
- `TasksDatabaseId` — the My Tasks database id (`f87718af-a132-498f-8549-78e5c8fcd826`).
- `TitleProperty` / `DueProperty` / `StatusProperty` — default to `Task name` / `Due` / `Status`.

**One-time setup:** create an internal integration at <https://www.notion.so/profile/integrations>,
copy the secret, **share the My Tasks database with that integration** (••• → Connections → add it),
then put `Enabled: true` + the token + database id into `settings.json`. The secret lives in
`%ProgramData%` (admin-written, locally readable) — acceptable for a single-user machine.

## Architecture
Two cooperating parts, one shared library:

- **`EarnedScreen.Core`** — shared brain (no UI): `HostsFileManager` (the hosts-file block + DNS
  flush), `BrowserPolicyManager` (DoH off), `Settings`/`SettingsStore`, `SessionState`/`StateStore`,
  the named-pipe `PipeContract`, and `NotionTasksClient` (daily-tasks gateway).
- **`EarnedScreen.Service`** — a Windows Service (runs as **SYSTEM**, so no UAC prompts). It is the
  *only* writer of the hosts file and the source of truth. Owns the session timer / Guillotine and
  exposes two named pipes. See `EnforcementEngine`, `CommandPipeServer`, `EventPipeServer`, `Worker`.
- **`EarnedScreen.App`** — WPF client (runs as the normal user, no elevation). Shows the gateway
  checklist, requests unlock over the Command pipe, and listens on the Events pipe to pop the
  full-screen cool-down lock when the Guillotine drops. See `ServiceClient`, `MainWindow`,
  `CoolDownWindow`.

Why a service (not Task Scheduler): it can't be casually killed, survives reboots, runs as SYSTEM so
hosts edits never prompt, and a service can't draw UI — so the user-session app owns all windows and
the service notifies it over a pipe.

### Pipes
- `EarnedScreen.Command` — request/response: `GetStatus`, `RequestUnlock` → `StatusResponse`.
- `EarnedScreen.Events` — server push: `SessionEnded` (Guillotine dropped).
ACLs (`PipeFactory`) let the user-session app talk to the SYSTEM-owned pipes.

## Build / test / run
```powershell
dotnet build                                   # whole solution (.slnx)
dotnet test                                     # Core unit tests
./scripts/smoke-test.ps1                         # full service lifecycle, no admin needed
```

### Test seam (no admin)
The service normally writes the real hosts file (needs admin). For tests/dev, two env vars redirect
everything to a sandbox so it runs **unelevated**:
- `EARNEDSCREEN_HOSTS` — path to a sandbox hosts file.
- `EARNEDSCREEN_DATA`  — path to a sandbox data dir (settings.json + state.json).
`scripts/smoke-test.ps1` uses these to exercise default-block → unlock → Guillotine → daily-limit.

### Install for real (admin)
```powershell
./scripts/install-service.ps1     # builds Release, registers + starts the SYSTEM service
./scripts/uninstall-service.ps1   # stops/removes the service and strips the hosts block
```
The WPF app (`EarnedScreen.App`) is launched by the user; launch-at-login is a planned follow-up.

## Status
- **Done:** Core, Service (full enforcement + pipes), thin WPF client, unit + smoke tests, install
  scripts. Verified end-to-end against a sandbox.
- **Planned:** polished WPF styling, system-tray host + launch-at-login, hardened un-closeable gateway.
