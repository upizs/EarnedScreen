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

Runtime state (do not hand-edit) lives at `%ProgramData%\EarnedScreen\state.json`.

## Architecture
Two cooperating parts, one shared library:

- **`EarnedScreen.Core`** — shared brain (no UI): `HostsFileManager` (the hosts-file block + DNS
  flush), `Settings`/`SettingsStore`, `SessionState`/`StateStore`, and the named-pipe `PipeContract`.
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
