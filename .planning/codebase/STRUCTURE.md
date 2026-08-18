# Codebase Structure

**Analysis Date:** 2026-08-17

## Directory Layout

```
TheLastAcorn/
├── Assets/                     # All Unity content (scenes, scripts, art, audio)
│   ├── Managers/               # Bootstrap + persistent manager prefabs/scripts
│   │   ├── Prefabs/            # CameraManager, Checkpoint Manager, Score Manager, etc.
│   │   └── Scripts/            # GameManager.cs, DebugSettings.cs
│   ├── Player/                 # Player prefab, art, animations, and player scripts
│   │   ├── Scripts/            # Player*Manager, Collector, camera
│   │   │   └── Movement/       # PlayerMove partial-class files
│   │   ├── Animations/ Graphics/ HUD/ Effects/ ScentTrails/
│   │   └── End Game Player/    # EndGameScript.cs
│   ├── Scripts/                # Core game systems (managers, world, NPC, UI, views)
│   │   ├── Effects/            # ShakeEffect, ShakeMa
│   │   ├── Managers/           # (misc manager scripts)
│   │   ├── NPC/                # Fox, Hawk, Owl, PatrollingEnemy
│   │   ├── Player/             # (player-adjacent scripts)
│   │   ├── Squirrels House/    # Home-scene background switching
│   │   ├── TreeTool/           # Level-design ScriptableObject tool
│   │   │   └── Editor/         # Editor-only windows/inspectors
│   │   ├── Tutorial/           # Tutorial flow + input switching
│   │   ├── UI/                 # AcornArrow, MainMenu, PauseMenu, ButtonHover
│   │   ├── Views/              # ViewManager, ViewBase (UI stack)
│   │   └── World/              # Acorn, Gate, Vine, LilyPad, WindGust, etc.
│   ├── Views/                  # View prefabs + view controller scripts
│   ├── Scenes/                 # Playable + archived .unity scenes
│   │   ├── Tutorial/           # Level 1-3
│   │   └── Miscellaneous/      # V1Scenes, V2 Scenes, Testing Scenes, Puzzle Designs
│   ├── Resources/              # Runtime-loaded assets (perf test JSON only)
│   ├── Settings/               # URP render settings, Build Profiles, scene template
│   ├── Sounds/ Timeline/ Squirrel Home/  # Audio, timelines, home-tree art
│   ├── Feel/                   # THIRD-PARTY: MoreMountains Feel (feedbacks, tools)
│   ├── Plugins/                # THIRD-PARTY: Dreamteck Splines, etc.
│   ├── NaughtyAttributes/      # THIRD-PARTY: Inspector attributes
│   └── TextMesh Pro/           # THIRD-PARTY: TMP
├── Packages/                   # Unity Package Manager manifest + lock
├── ProjectSettings/            # Unity project config (EditorBuildSettings, ProjectVersion)
├── Library/ Logs/ obj/         # Generated (git-ignored)
└── *.csproj / TheLastAcorn.sln # Generated IDE project files
```

## Directory Purposes

**`Assets/Managers/`:**
- Purpose: Game bootstrap and persistent manager wiring
- Contains: `GameManager.cs`, `DebugSettings.cs`, manager prefabs
- Key files: `Assets/Managers/Scripts/GameManager.cs`, `Assets/Managers/Prefabs/*.prefab`

**`Assets/Player/`:**
- Purpose: Everything player-related (art, animation, and gameplay scripts)
- Contains: player scripts, movement partials, HUD, effects, scent trails
- Key files: `Assets/Player/Scripts/Movement/PlayerMove.cs`, `Assets/Player/Scripts/PlayerStateManager.cs`

**`Assets/Scripts/`:**
- Purpose: Core game systems not owned by the player
- Contains: managers, world interactables, NPCs, UI, views, tutorial, tree tool
- Key files: `Assets/Scripts/SaveLoadManager.cs`, `Assets/Scripts/ScoreManager.cs`, `Assets/Scripts/Views/ViewManager.cs`

**`Assets/Scripts/World/`:**
- Purpose: Level interactables and hazards
- Contains: `Acorn.cs`, `Gate.cs`, `VineMaster.cs`, `WindGust.cs`, `LilyPad.cs`, `BouncyBranch.cs`
- Key files: `Assets/Scripts/World/Acorn.cs`

**`Assets/Views/` + `Assets/Scripts/Views/`:**
- Purpose: Stack-based UI. `Scripts/Views` holds the framework; `Assets/Views` holds concrete screens/prefabs
- Key files: `Assets/Scripts/Views/ViewBase.cs`, `Assets/Views/UpgradePlayer/Scripts/UpgradePlayerView.cs`

**`Assets/Scenes/`:**
- Purpose: All Unity scenes. Active build scenes plus large `Miscellaneous/` archive (V1/V2/testing)
- Key files: `Assets/Scenes/Main Menu.unity`, `Assets/Scenes/Level1.unity`, `Assets/Scenes/Tutorial/Level 1.unity`

**`Assets/Feel/`, `Assets/Plugins/`, `Assets/NaughtyAttributes/`, `Assets/TextMesh Pro/`:**
- Purpose: Vendored third-party packages. Do NOT modify or treat as project code.
- Generated/imported: Yes (asset store)

## Key File Locations

**Entry Points:**
- `Assets/Managers/Scripts/GameManager.cs`: Runtime bootstrap (spawns managers, `DontDestroyOnLoad`)
- `Assets/Scripts/SceneInitializer.cs`: Per-scene setup

**Configuration:**
- `ProjectSettings/ProjectVersion.txt`: Unity `6000.4.9f1`
- `ProjectSettings/EditorBuildSettings.asset`: Scene build list (Main Menu → Spring/Summer levels)
- `Packages/manifest.json`: Package dependencies
- `Assets/Settings/`: URP render pipeline + Build Profiles

**Core Logic:**
- `Assets/Player/Scripts/Movement/PlayerMove.cs`: Player movement (partial class)
- `Assets/Player/Scripts/PlayerStateManager.cs`: Player FSM
- `Assets/Scripts/SaveLoadManager.cs`: Persistence
- `Assets/Scripts/ScoreManager.cs`, `Assets/Scripts/CheckpointManager.cs`: Progression

**Testing:**
- No unit-test assemblies detected. `Assets/Scenes/Miscellaneous/Testing Scenes/` holds manual play-test scenes.

## Naming Conventions

**Files:**
- One primary class per file; file name matches the class (`ScoreManager.cs` → `ScoreManager`)
- Partial classes split by feature with a dotted suffix: `PlayerMove.cs`, `PlayerMove.Climb.cs`, `PlayerMove.Glide.cs`, `PlayerMove.Leap.cs`
- Managers suffixed `Manager` (`CheckpointManager`, `PlayerAbilityManager`); UI views suffixed `View`/`ViewBase`

**Classes & members:**
- Classes/methods/properties: `PascalCase`
- Private fields: `camelCase`, exposed to Inspector with `[SerializeField]`
- Enums: `PascalCase` type with `PascalCase` members (one outlier: `PlayerState.STUNNED` is upper-case)

**Directories:**
- `PascalCase` or space-separated Title Case (`Squirrel Home`, `End Game Player`, `V2 Scenes`)
- Editor-only code isolated under an `Editor/` subfolder (`Assets/Scripts/TreeTool/Editor/`)

**Namespaces:**
- Mostly global namespace. Exceptions: `Common` (EventHandler), `ScriptEffects` (ShakeEffect).

## Where to Add New Code

**New gameplay system / manager:**
- Primary code: `Assets/Scripts/` (as a `MonoBehaviour` with `static Instance` if it should be a singleton)
- If persistent across scenes: register its prefab in `GameManager.persistentManagerPrefabs`; if scene-dependent, add to `stage2Objects`

**New player ability/movement:**
- Extend the `PlayerMove` partial class: add `Assets/Player/Scripts/Movement/PlayerMove.<Feature>.cs`
- Add a state to `PlayerStateManager.PlayerState` if it needs its own FSM state

**New world interactable / hazard:**
- Implementation: `Assets/Scripts/World/` (implement `ICollectible` or `IDamageable` where relevant)

**New UI screen:**
- Controller: subclass `ViewBase` under `Assets/Views/<ScreenName>/` or `Assets/Scripts/Views/`
- Add an entry to the `ViewID` enum in `Assets/Scripts/Views/ViewManager.cs`
- Push/pop it through `ViewManager.Instance`

**New editor tool:**
- Place under a `Editor/` subfolder (e.g., `Assets/Scripts/TreeTool/Editor/`)

**New ScriptableObject data:**
- Add `[CreateAssetMenu(menuName = "The Last Acorn/...")]` following `TreePieceLibrary.cs`

## Special Directories

**`Library/`, `obj/`, `Logs/`, `UserSettings/`:**
- Purpose: Unity/IDE generated caches and logs
- Generated: Yes
- Committed: No (git-ignored)

**`Assets/Resources/`:**
- Purpose: Runtime `Resources.Load` assets; currently only performance-test JSON
- Committed: Yes

**`Assets/Scenes/Miscellaneous/`:**
- Purpose: Archived/legacy scene versions (V1Scenes, V2 Scenes) and testing scenes; most are NOT in the build list
- Committed: Yes

**`graphify-out/`:**
- Purpose: Knowledge-graph output (currently only a `cache/`; `graph.json` not yet generated)
- Committed: Partially (see `.gitignore`)

---

*Structure analysis: 2026-08-17*
