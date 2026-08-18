# Technology Stack

**Analysis Date:** 2026-08-17

## Languages

**Primary:**
- C# (.NET Standard 2.1) - All gameplay scripts under `Assets/Scripts/` (69 files) plus vendored plugin code (Feel, Dreamteck, NaughtyAttributes). Compiled into `Assembly-CSharp` / `Assembly-CSharp-Editor`.

**Secondary:**
- HLSL / ShaderLab - Shaders and shader graphs, e.g. `Assets/Visuals/World/Interactables/Checkpoints/GlowParticleShader.shadergraph`, `Assets/TextMesh Pro/Shaders/*.cginc`.
- YAML - Unity serialized assets (scenes, prefabs, `.asset`, `.controller`, ProjectSettings). Not hand-authored.

## Runtime

**Environment:**
- Unity Engine `6000.4.9f1` (Unity 6.x LTS line) - see `ProjectSettings/ProjectVersion.txt` (`m_EditorVersionWithRevision: 6000.4.9f1 (f7258d6eebbe)`).
- .NET / Mono scripting runtime (`scriptingRuntimeVersion: 1`), API compatibility level `.NET Standard 2.1` (`apiCompatibilityLevel: 6` in `ProjectSettings/ProjectSettings.asset`).

**Scripting Backend:**
- Android: IL2CPP (`scriptingBackend: { Android: 1 }`).
- Other platforms: Mono (default; no override present).
- `allowUnsafeCode: 0`, `useDeterministicCompilation: 1`, `gcIncremental: 1`.

**Package Manager:**
- Unity Package Manager (UPM).
- Manifest: `Packages/manifest.json`
- Lockfile: `Packages/packages-lock.json` (present).

## Frameworks

**Core (Unity packages, from `Packages/manifest.json`):**
- Universal Render Pipeline (URP) `com.unity.render-pipelines.universal@17.4.0` - 2D URP rendering (`m_RenderingPath: 1`). Settings under `Assets/Settings/`.
- 2D Feature set `com.unity.feature.2d@2.0.2` - tilemaps, sprites, 2D physics tooling.
- Input System `com.unity.inputsystem@1.19.0` - player/UI input (used across `PauseMenu.cs`, `Owl.cs`, `VineSegment.cs`, tutorial scripts).
- Cinemachine `com.unity.cinemachine@3.1.6` - camera rigs (`CameraRig.cs`, `OverlayCameraController.cs`).
- Timeline `com.unity.timeline@1.8.12` + Director module - cutscenes (`Assets/Timeline/`, `CutsceneEndHandler.cs`).
- Visual Scripting `com.unity.visualscripting@1.9.11` - referenced in `Assets/Scripts/NPC/FoxScript.cs`.
- Visual Effect Graph `com.unity.visualeffectgraph@17.4.0`.
- uGUI `com.unity.ugui@2.0.0` + TextMesh Pro (`Assets/TextMesh Pro/`) - UI/menus.

**Testing:**
- Unity Test Framework `com.unity.test-framework@1.6.0` - installed. No project-authored test assemblies detected (only vendored `NaughtyAttributes.Test.asmdef`).

**Build/Dev / IDE:**
- Rider editor integration `com.unity.ide.rider@3.0.40`
- Visual Studio editor integration `com.unity.ide.visualstudio@2.0.27`
- Version Control `com.unity.collab-proxy@2.12.4`
- Unity AI Assistant `com.unity.ai.assistant@2.17.0-pre.1`
- Multiplayer Center `com.unity.multiplayer.center@1.0.1` (tooling only; no multiplayer gameplay code detected).

## Key Dependencies

**Critical (third-party asset-store packages, vendored under `Assets/`):**
- Feel (MoreMountains) `v5.9.1` - `Assets/Feel/` (MMFeedbacks, MMTools, NiceVibrations). Game-feel/juice system used heavily via `MoreMountains.Feedbacks` (`Gate.cs`, `Mushroom.cs`, `OverlayCameraController.cs`, `MMFPlayerExtensions.cs`). Assemblies `MoreMountains.Tools`, `Lofelt.NiceVibrations`.
- Dreamteck Splines - `Assets/Plugins/Dreamteck/` - spline system (assemblies `Dreamteck.Splines`, `Dreamteck.Utilities`). Enabled via `DREAMTECK_SPLINES` scripting define.
- NaughtyAttributes `v2.1.5` - `Assets/NaughtyAttributes/` (`com.dbrizov.naughtyattributes`) - inspector attribute extensions (`ShakeEffect.cs`).
- UniTask (Cysharp) - `com.cysharp.unitask` (git dependency: `https://github.com/Cysharp/UniTask.git`) - allocation-free async/await for Unity.

**Infrastructure (Unity built-in modules, `com.unity.modules.*`):**
- Physics2D, Animation, Particle System, Tilemap, Audio, UI/UIElements, Video, JSON Serialize, UnityWebRequest, Terrain, VR/XR (mostly default-enabled modules; VR/XR unused by gameplay).

## Configuration

**Scripting Defines:**
- Standalone: `DREAMTECK_SPLINES;MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED` (`ProjectSettings/ProjectSettings.asset`).

**Project Identity:**
- Product name: `TheLastAcorn`; company: `DefaultCompany`; bundle version: `1.0`.

**Persistence:**
- `PlayerPrefs` only - no custom save files or DB. Central wrapper: `Assets/Scripts/SaveLoadManager.cs` (keys: `CurrentLevel`, `TotalScore`, `VisitedLevels_*`, `Acorn_<level>_<id>`).
- Debug toggle to disable persistence via `DebugSettings` singleton.

**Rendering:**
- URP 2D pipeline assets and renderer settings under `Assets/Settings/`.
- Graphics config: `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/URPProjectSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset`.

**Build:**
- Scene list: `ProjectSettings/EditorBuildSettings.asset`. Scenes under `Assets/Scenes/`.
- Custom editor build tooling: `Assets/Scripts/TreeTool/Editor/TreeBuilderWindow.cs`, `Assets/Scripts/DebugEditorMenu.cs`.

## Platform Requirements

**Development:**
- Unity `6000.4.9f1`. Rider or Visual Studio recommended (both editor integrations installed).
- Solution: `TheLastAcorn.sln` (multiple generated `.csproj` per vendored assembly).

**Production:**
- Distributed via [Itch.io](https://lukem13.itch.io/the-last-acorn) — primary target is desktop Standalone (scripting defines are Standalone-scoped).
- Android build path configured (IL2CPP backend) but not the primary distribution channel.

---

*Stack analysis: 2026-08-17*
