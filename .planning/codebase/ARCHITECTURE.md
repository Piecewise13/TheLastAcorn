<!-- refreshed: 2026-08-17 -->
# Architecture

**Analysis Date:** 2026-08-17

## System Overview

"The Last Acorn" is a Unity 6 (`6000.4.9f1`) 2D URP side-scrolling platformer. The game
code is a component-based MonoBehaviour architecture: a persistent bootstrap manager spawns
singleton managers, gameplay logic lives on player/world components, and cross-cutting
communication happens through C# events, Inspector-wired UnityEvents, and the Unity Input
System. Heavy use is made of third-party asset packages (Feel/MoreMountains, Dreamteck
Splines, NaughtyAttributes, TextMesh Pro, UniTask, Lofelt Nice Vibrations).

```text
┌─────────────────────────────────────────────────────────────┐
│                    Bootstrap / Persistence                   │
│  GameManager (DontDestroyOnLoad)   SaveLoadManager (static)  │
│  `Assets/Managers/Scripts/GameManager.cs`                    │
└──────────────────┬───────────────────────┬──────────────────┘
         spawns    │                        │ PlayerPrefs
                   ▼                        ▼
┌───────────────────────────────┐  ┌───────────────────────────┐
│      Singleton Managers        │  │       Persistence         │
│  ScoreManager, ViewManager,    │  │  SaveLoadManager,         │
│  CheckpointManager, TimeManager│  │  ScoreManager (score),    │
│  DebugSettings, CameraRig ...  │  │  DebugSettings bypass     │
│  `Assets/Scripts/*Manager.cs`  │  │  `SaveLoadManager.cs`     │
└────────┬──────────────┬────────┘  └───────────────────────────┘
         │              │
         ▼              ▼
┌───────────────────────────────┐  ┌───────────────────────────┐
│   Player (composed managers)   │  │   World / Interactables   │
│  PlayerMove (partial) + Life/  │  │  Acorn, Gate, Vine, Owl,  │
│  Ability/Effects/Camera/Upgrade│  │  Fox, Hawk, Bat, Mushroom │
│  PlayerStateManager (FSM)      │  │  `Assets/Scripts/World/`  │
│  `Assets/Player/Scripts/`      │  │  `Assets/Scripts/NPC/`    │
└────────┬──────────────────────-┘  └───────────────────────────┘
         │ events (OnStateChanged, OnScoreChanged...)
         ▼
┌─────────────────────────────────────────────────────────────┐
│        Views (stacked UI, UniTask async)                     │
│  ViewManager → ViewBase (HUD, PauseMenu, AbilityUnlock...)   │
│  `Assets/Scripts/Views/`  `Assets/Views/`                    │
└─────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| GameManager | Bootstrap: spawn persistent + scene-dependent managers, `DontDestroyOnLoad` | `Assets/Managers/Scripts/GameManager.cs` |
| DebugSettings | Global debug flags (persistence bypass, spawn point, logs) | `Assets/Managers/Scripts/DebugSettings.cs` |
| SaveLoadManager | Static persistence over `PlayerPrefs` (level, score, acorns) | `Assets/Scripts/SaveLoadManager.cs` |
| ScoreManager | Global cumulative score + `OnScoreChanged` event | `Assets/Scripts/ScoreManager.cs` |
| LevelScoreManager | Per-level score used by audio/progression | `Assets/Scripts/LevelScoreManager.cs` |
| CheckpointManager | Respawn points + `OnPlayerRespawn` event | `Assets/Scripts/CheckpointManager.cs` |
| SceneInitializer | Per-scene setup (spawn point, owl, scene state) | `Assets/Scripts/SceneInitializer.cs` |
| SceneLoader | Static scene transition helpers | `Assets/Scripts/SceneLoader.cs` |
| PlayerStateManager | Player finite-state machine + `OnStateChanged` event | `Assets/Player/Scripts/PlayerStateManager.cs` |
| PlayerMove | Player movement/input (partial class) | `Assets/Player/Scripts/Movement/PlayerMove*.cs` |
| ViewManager | Stack-based UI navigation (UniTask async) | `Assets/Scripts/Views/ViewManager.cs` |
| Common.EventHandler | Inspector-wired named UnityEvent dispatcher | `Assets/Scripts/EventHandler.cs` |
| Collector / Acorn | Collectible pickup pipeline via `ICollectible` | `Assets/Player/Scripts/Collector.cs`, `Assets/Scripts/World/Acorn.cs` |

## Pattern Overview

**Overall:** Component-based (Unity MonoBehaviour) architecture with Singleton managers and
event-driven communication. No DI container or formal service layer — managers expose a
`static Instance` and are discovered globally.

**Key Characteristics:**
- Singleton managers via `public static T Instance { get; private set; }` set in `Awake()`,
  destroying duplicates. ~20 game-code singletons (see `Grep` for `public static ... Instance`).
- Two-stage bootstrap: persistent managers instantiated under `GameManager`, scene-dependent
  managers spawned per scene only if not already hand-placed.
- Player behaviour is composed from many single-responsibility managers on the player object
  rather than one monolith; `PlayerMove` itself is a `partial class` split by ability.
- Communication is a mix of typed C# `event Action<...>`, Inspector `UnityEvent`
  (`Common.EventHandler`), and the generated Unity Input System action map.
- Persistence is a static utility over `PlayerPrefs`, with a debug bypass toggle.

## Layers

**Bootstrap / Managers Layer:**
- Purpose: Establish persistent game systems and per-scene managers
- Location: `Assets/Managers/Scripts/`, plus `*Manager.cs` in `Assets/Scripts/`
- Contains: `GameManager`, `DebugSettings`, singleton managers (Score, Checkpoint, Time, Camera, View)
- Depends on: Unity `SceneManagement`, `PlayerPrefs`
- Used by: Everything (accessed via `Instance`)

**Player Layer:**
- Purpose: Player input, movement, state, abilities, camera, effects, life, upgrades
- Location: `Assets/Player/Scripts/` (movement in `Assets/Player/Scripts/Movement/`)
- Contains: `PlayerMove` (partial), `PlayerStateManager` (FSM), `Player*Manager` components
- Depends on: `PlayerGameControls` (generated input), `PlayerStateManager`, world triggers
- Used by: World interactables (owl, vine, gust) call into `PlayerMove` public methods

**World / Interactables Layer:**
- Purpose: Level objects, hazards, enemies, collectibles
- Location: `Assets/Scripts/World/`, `Assets/Scripts/NPC/`
- Contains: `Acorn`, `Gate`, `VineMaster`/`VineSegment`, `Owl`, `Hawk`, `Fox`, `Bat`, `Mushroom`, `WindGust`
- Depends on: `ICollectible`/`IDamageable` interfaces, player state, managers
- Used by: Player collisions/triggers

**View / UI Layer:**
- Purpose: Stacked UI screens (HUD, pause, ability unlock, upgrade)
- Location: `Assets/Scripts/Views/`, `Assets/Views/`
- Contains: `ViewManager` (stack), `ViewBase` (abstract), concrete view controllers
- Depends on: UniTask (`Cysharp.Threading.Tasks`), `ViewID` enum
- Used by: Menus and gameplay to push/pop screens

**Persistence Layer:**
- Purpose: Save/load progress and collected acorns
- Location: `Assets/Scripts/SaveLoadManager.cs`
- Contains: static `PlayerPrefs`-backed API
- Depends on: `DebugSettings` (bypass), Unity `PlayerPrefs`
- Used by: `Acorn`, `ScoreManager`, `SceneInitializer`, `SceneLoader`

## Data Flow

### Primary Request Path (game bootstrap → play)

1. `GameManager.Awake()` sets `Instance`, `DontDestroyOnLoad`, marks `IsReady`, fires `OnReady` (`Assets/Managers/Scripts/GameManager.cs:16`)
2. `InstantiatePersistent()` spawns persistent manager prefabs under the initializer (`GameManager.cs:45`)
3. `SpawnStage2Objects()` spawns scene-dependent managers unless already in scene (`GameManager.cs:57`)
4. On each load, `SceneManager.sceneLoaded` → `SpawnStage2Objects()` again (`GameManager.cs:40`)
5. `SceneInitializer.Start()` seeds scene state, resolves player/owl/checkpoint, sets spawn (`Assets/Scripts/SceneInitializer.cs:18`)

### Player State / Movement Flow

1. Unity Input System `PlayerGameControls` action map bound in `PlayerMove.Awake()` (`Assets/Player/Scripts/Movement/PlayerMove.cs:139`)
2. `FixedUpdate()` reads state from `PlayerStateManager.Instance.CurrentState` and dispatches per-state logic (`PlayerMove.cs:206`)
3. Transitions call `PlayerStateManager.ChangeState(...)`, firing `OnStateChanged` (`Assets/Player/Scripts/PlayerStateManager.cs:42`)
4. `PlayerMove.HandleStateChanged(...)` updates the `Animator` on each transition (`PlayerMove.cs:195`)

### Collectible Flow

1. `Collector.OnTriggerEnter2D` detects `ICollectible` on contact (`Assets/Player/Scripts/Collector.cs:18`)
2. Notifies `LevelScoreManager` and `PlayerUpgradeManager`, calls `Acorn.OnCollected()` (`Collector.cs:26`)
3. `Acorn.OnCollected()` sets a checkpoint, marks collected in `SaveLoadManager`, adds to `ScoreManager` (`Assets/Scripts/World/Acorn.cs:51`)
4. `ScoreManager.AddScore` persists via `SaveLoadManager.SaveTotalScore` and fires `OnScoreChanged` (`Assets/Scripts/ScoreManager.cs:37`)

**State Management:**
- Runtime state: singleton managers hold live state (score, player FSM, checkpoints).
- Persistent state: `SaveLoadManager` (static) over `PlayerPrefs`, plus in-memory acorn session sets.
- UI state: `ViewManager` maintains a `Stack<ViewBase>`.

## Key Abstractions

**ICollectible / Acorn:**
- Purpose: Uniform pickup contract (`int Value`)
- Examples: `Assets/Scripts/ICollectible.cs`, `Assets/Scripts/World/Acorn.cs`
- Pattern: Interface + `Collector` trigger detection

**IDamageable:**
- Purpose: Damage/kill contract for hittable entities
- Examples: `Assets/Scripts/Interfaces.cs`
- Pattern: Interface implemented by damageable components

**PlayerStateManager (FSM):**
- Purpose: Central player state (`Grounded`, `Climb`, `Glide`, `Fall`, `RidingOwl`, `VineSwinging`, `STUNNED`)
- Examples: `Assets/Player/Scripts/PlayerStateManager.cs`
- Pattern: Enum state + `event Action<PlayerState, PlayerState> OnStateChanged`

**ViewBase / ViewManager:**
- Purpose: Async, stack-based UI screens
- Examples: `Assets/Scripts/Views/ViewBase.cs`, `Assets/Scripts/Views/ViewManager.cs`
- Pattern: Abstract base with UniTask `Setup/Show/Hide`, pushed/popped on a stack

**Common.EventHandler (NamedEvent):**
- Purpose: Designer-wired events triggered by string name from the Inspector
- Examples: `Assets/Scripts/EventHandler.cs`
- Pattern: `List<NamedEvent>` mapping `eventName` → `UnityEvent`

**TreePieceLibrary (ScriptableObject):**
- Purpose: Data asset for the in-editor tree-building tool
- Examples: `Assets/Scripts/TreeTool/TreePieceLibrary.cs` (`[CreateAssetMenu(... "The Last Acorn/Tree Piece Library")]`)
- Pattern: The only game-authored ScriptableObject; all other SOs belong to third-party packages

## Entry Points

**GameManager (runtime bootstrap):**
- Location: `Assets/Managers/Scripts/GameManager.cs`
- Triggers: Placed in the first-loaded scene; `Awake()` runs on startup
- Responsibilities: Spawn persistent + scene-dependent managers, survive scene loads

**SceneInitializer (per-scene):**
- Location: `Assets/Scripts/SceneInitializer.cs`
- Triggers: `Start()` when a gameplay scene loads
- Responsibilities: Initialize scene acorn state, resolve player/owl/checkpoint, apply spawn point

**MonoBehaviour lifecycle:**
- `Awake()`/`Start()` on gameplay components; `FixedUpdate()` for physics-based movement.

**Editor tools:**
- `Assets/Scripts/TreeTool/Editor/TreeBuilderWindow.cs` (custom `EditorWindow`), `DebugEditorMenu.cs`.

## Architectural Constraints

- **Threading:** Single-threaded Unity main loop. Async UI uses UniTask (`Cysharp.Threading.Tasks`) on the player loop, not OS threads; `ViewManager` uses `CancellationTokenSource` for transitions.
- **Global state:** Extensive module-level singletons (`static Instance`) and a fully static `SaveLoadManager` holding in-memory acorn sets. Order-of-initialization matters (e.g., `ScoreManager.Awake` reads `SaveLoadManager.LoadTotalScore`).
- **Global discovery:** Components locate collaborators via `Instance`, `FindAnyObjectByType`, and tag lookups (`GameObject.FindGameObjectWithTag("Player")` in `SceneInitializer`), coupling logic to scene contents.
- **Namespaces:** Almost all game scripts live in the global namespace; only `Common` (EventHandler) and `ScriptEffects` (ShakeEffect) are namespaced.

## Anti-Patterns

### Global singleton coupling

**What happens:** Gameplay code reaches into `ScoreManager.Instance`, `PlayerStateManager.Instance`, `CheckpointManager.Instance`, etc. directly.
**Why it's wrong:** Hidden dependencies and fragile initialization order; hard to test in isolation and null-prone across scene transitions.
**Do this instead:** Continue using existing singletons for consistency, but guard every access with null checks (as `SceneInitializer`/`SceneLoader` already do) and prefer injecting references via `[SerializeField]` where a scene-local reference exists.

### Scene lookup by tag/type at Start

**What happens:** `SceneInitializer.Start()` calls `FindGameObjectWithTag`/`FindAnyObjectByType` to wire up player, owl, and checkpoint (`Assets/Scripts/SceneInitializer.cs:48`).
**Why it's wrong:** Silent `NullReferenceException` risk if a scene omits the expected object (note `player.transform.root` with no null check).
**Do this instead:** Serialize references on the initializer or validate lookups before use.

## Error Handling

**Strategy:** Defensive null checks around manager singletons; `Debug.LogWarning`/`Debug.LogError` for missing wiring rather than exceptions.

**Patterns:**
- `if (Manager.Instance != null) { ... }` guards before manager calls (`SceneLoader.RestartLevel`, `Collector`).
- `Debug.LogWarning` for unknown events (`Common.EventHandler.TriggerEvent`) and duplicate singletons (`PlayerStateManager.Awake`).

## Cross-Cutting Concerns

**Logging:** `Debug.Log`/`print` scattered through gameplay; `DebugSettings.ShowDebugLogs` gates verbose persistence logs.
**Validation:** Ad-hoc null checks; NaughtyAttributes used for Inspector validation/attributes.
**Input:** Unity Input System via generated `PlayerGameControls` action map (`Assets/Player/PlayerGameControls.cs`).
**Feedback/juice:** Feel/MoreMountains `MMF_Player` feedbacks (see `Assets/Scripts/MMFPlayerExtensions.cs`, `Assets/Scripts/Effects/`).

---

*Architecture analysis: 2026-08-17*
