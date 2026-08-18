# External Integrations

**Analysis Date:** 2026-08-17

## APIs & External Services

This is a self-contained single-player Unity game. It makes **no runtime calls to external web APIs or backend services**. No HTTP client usage, REST calls, or third-party service SDKs (Firebase, Steamworks, PlayFab, etc.) were detected in `Assets/Scripts/`.

**Unity Gaming Services:**
- All Unity cloud services are **disabled** (`ProjectSettings/UnityConnectSettings.asset`):
  - Unity Analytics: `m_Enabled: 0`
  - Unity Ads: `m_Enabled: 0`
  - Unity Purchasing (IAP): `m_Enabled: 0`
  - Crash Reporting: `m_Enabled: 0`
  - Performance Reporting: `m_Enabled: 0`
- The `com.unity.modules.unityanalytics` module is present but analytics is turned off; no telemetry is collected.

## Data Storage

**Databases:**
- None. No SQL/NoSQL database, no ORM.

**Local Persistence:**
- Unity `PlayerPrefs` (platform-native key/value store: registry on Windows, plist on macOS, XML on Linux).
- Access is centralized in `Assets/Scripts/SaveLoadManager.cs`.
- Keys: `CurrentLevel`, `TotalScore`, `VisitedLevels_<name>`, `Acorn_<level>_<acornId>`.
- No custom serialized save files, no JSON/binary save on disk.

**File Storage:**
- Local filesystem only (Unity project assets). No cloud/object storage.

**Caching:**
- None (beyond Unity's built-in asset/`Library` cache).

## Authentication & Identity

- None. No login, user accounts, or auth providers. Single-player local game.

## Monitoring & Observability

**Error Tracking:**
- None (Unity Crash Reporting disabled). Diagnostics via `UnityEngine.Debug.Log` gated behind a `DebugSettings` singleton (`SaveLoadManager.cs`, `DebugEditorMenu.cs`).

**Logs:**
- Unity Editor/Player logs only (`Logs/` directory in project root). Two Mono crash dumps present at repo root (`mono_crash.0.0.json`, `mono_crash.0.1.json`) — editor crash artifacts, not runtime integrations.

## CI/CD & Deployment

**Hosting / Distribution:**
- [Itch.io](https://lukem13.itch.io/the-last-acorn) — manual build upload (per `README.md`). No automated deploy pipeline detected.

**CI Pipeline:**
- None detected (no `.github/workflows/`, GitLab CI, or Unity Cloud Build config in the repo).

**Version Control:**
- Git (repo present). Unity `com.unity.collab-proxy@2.12.4` (Unity Version Control / PlasticSCM integration) is installed as an editor tool; `ProjectSettings/VersionControlSettings.asset` present.

## Third-Party SDKs, Plugins & Asset Pipelines

**Vendored asset-store packages (imported into `Assets/`, not UPM-managed):**
- **Feel (MoreMountains) v5.9.1** — `Assets/Feel/` — game-feel/juice framework (MMFeedbacks + MMTools). Namespace `MoreMountains.Feedbacks`. Used by `Gate.cs`, `Mushroom.cs`, `OverlayCameraController.cs`, `MMFPlayerExtensions.cs`.
- **NiceVibrations (Lofelt, bundled with Feel)** — `Assets/Feel/NiceVibrations/` — HD haptic feedback for mobile/gamepad. Enabled via `MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED` scripting define. Assembly `Lofelt.NiceVibrations`.
- **Dreamteck Splines** — `Assets/Plugins/Dreamteck/` — spline authoring/runtime (vines, paths). Enabled via `DREAMTECK_SPLINES` scripting define. Assemblies `Dreamteck.Splines`, `Dreamteck.Utilities`.
- **NaughtyAttributes v2.1.5** — `Assets/NaughtyAttributes/` (`com.dbrizov.naughtyattributes`) — editor inspector attributes.
- **UniTask (Cysharp)** — UPM git package `https://github.com/Cysharp/UniTask.git` — async/await utilities.

**UPM git dependency (network fetch at resolve time):**
- UniTask is pulled from GitHub during package resolution (see `Packages/manifest.json`). Requires network access on first import/CI.

**Art / Audio asset pipelines (imported content, no runtime code):**
- Kenney input prompt sprite packs — `Assets/Visuals/Controls/kenney_inputPromptsPixel16×/`.
- Third-party parallax background art (e.g. "Digital Moons" mountains) — `Assets/Visuals/World/End Credit Background/`.
- Custom music/SFX under `Assets/Sounds/`.
- TextMesh Pro assets/shaders — `Assets/TextMesh Pro/`.

## Environment Configuration

**Required env vars:**
- None. No `.env` files present; no environment-based configuration.

**Secrets location:**
- No secrets, API keys, or credentials in the project (nothing to store; no external services).

## Webhooks & Callbacks

**Incoming:**
- None.

**Outgoing:**
- None.

---

*Integration audit: 2026-08-17*
