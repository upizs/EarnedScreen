# EarnedScreen

> A zero-bypass, local environment lock that gates Netflix, YouTube, Hulu & friends behind a workout
> checklist. One earned session a day. Then the Guillotine drops.

Streaming is blocked **24/7 by default**. To watch, you pay the toll — a checklist of push-ups,
squats, and chores. Complete it and you earn a single timed session. When the clock runs out the
network snaps shut mid-episode, and a full-screen cool-down checklist gets you off the couch.

## How it works
- **The Wall** — a Windows Service maps streaming domains to `0.0.0.0` in the hosts file and flushes DNS.
- **The Toll** — the desktop app shows your checklist; finishing it unlocks the network for one session.
- **The Guillotine** — at the end of the session the block snaps back automatically; the active stream
  starves once its buffer drains, and a mandatory cool-down checklist appears.

## Projects
| Project | Role |
| --- | --- |
| `EarnedScreen.Core` | Shared logic: hosts-file manager, settings/state, pipe contract |
| `EarnedScreen.Service` | Windows Service (SYSTEM): enforcement engine, timer, named pipes |
| `EarnedScreen.App` | WPF client: gateway checklist + full-screen cool-down lock |
| `EarnedScreen.Tests` | xUnit tests for Core |

## Quick start
```powershell
dotnet build                       # build everything
dotnet test                        # run unit tests
./scripts/smoke-test.ps1           # full service lifecycle, no admin needed
./scripts/install-service.ps1      # install + start the service (run as admin)
```

Settings (durations, checklist, blocked domains) live privately at
`%ProgramData%\EarnedScreen\settings.json` and are managed via Claude Code — see [CLAUDE.md](CLAUDE.md).
