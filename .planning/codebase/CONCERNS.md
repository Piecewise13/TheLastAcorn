# Codebase Concerns

**Analysis Date:** 2026-08-17

> Scope: `Assets/Scripts/` (69 first-party C# files, ~6,900 LOC). Third-party
> packages under `Assets/Feel/`, `Assets/NaughtyAttributes/`, `Assets/Plugins/`
> are excluded except where the game depends on them.

## Tech Debt

**Editor-only code compiled into runtime assembly (build-breaking):**
- Issue: Several runtime scripts live outside any `Editor/` folder yet `using`
  the `UnityEditor` namespace. `UnityEditor` does not exist in player builds, so
  these produce `CS0234`/`CS0246` and will fail (or already fail) a standalone
  build. They only compile today because the project is run inside the Editor.
- Files:
  - `Assets/Scripts/World/VineSegment.cs` (line 1: `using UnityEditor;`)
  - `Assets/Scripts/World/LilyPad.cs` (line 3: `using UnityEditor.ShaderGraph.Internal;`)
  - `Assets/Scripts/TreeBuilder.cs` (line 2: `using UnityEditor;` — a `MonoBehaviour`, not an editor)
  - `Assets/Scripts/DebugEditorMenu.cs` (a `CustomEditor : Editor` sitting in `Assets/Scripts/`, not in an `Editor/` folder)
- Impact: Player/standalone builds break. Blocks shipping and CI build steps.
- Fix approach: Remove the unused `using UnityEditor*` lines from `VineSegment.cs`,
  `LilyPad.cs`, and `TreeBuilder.cs` (they do not appear to use editor APIs).
  Move `DebugEditorMenu.cs` into an `Editor/` folder (or wrap in `#if UNITY_EDITOR`).
  Consider introducing an `.asmdef` per layer so runtime/editor boundaries are
  enforced at compile time.

**No assembly definitions for first-party code:**
- Issue: All game scripts compile into the default `Assembly-CSharp`. Only
  third-party packages ship `.asmdef` files. This is what lets the editor-only
  `using` above go unnoticed and forces full recompiles on every change.
- Files: entire `Assets/Scripts/` tree (no `.asmdef` present).
- Impact: Slow iteration, no runtime/editor separation, no test assembly, hard
  to enforce dependency direction.
- Fix approach: Add `Game.Runtime`, `Game.Editor`, and `Game.Tests` asmdefs.

**Global mutable static state used for per-instance / per-scene logic:**
- Issue: Static fields are used where instance or scene-scoped state is intended,
  causing cross-object and cross-scene bleed and stale references after reloads.
- Files:
  - `Assets/Scripts/World/VineSegment.cs:28` — `private static bool canAttach`
    is shared across *every* vine in the scene; detaching from one vine blocks
    attaching to all others until the timer elapses, and the flag is never reset
    on scene reload.
  - `Assets/Scripts/World/Gate.cs:10` — `private static PlayerMove playerMove`
    persists across scene loads and can point at a destroyed player.
  - `Assets/Scripts/World/FoxBush.cs:6-24` — `foxSpawned`, `currentFox`,
    `currentFoxScript`, `allBushes` static; not reset on scene reload.
  - `Assets/Scripts/ScoreManager.cs:9` — `private static int score` combined with
    a `DontDestroyOnLoad` instance means score survives even a full manager
    teardown; makes "new game" / test isolation fragile.
- Impact: Hard-to-reproduce bugs on scene restart, checkpoint reload, and level
  transitions.
- Fix approach: Convert to instance fields, or clear statics explicitly in
  `OnEnable`/`Awake`/scene-load callbacks.

**Inconsistent singleton pattern:**
- Issue: Singletons are implemented five different ways and none reset `Instance`
  in `OnDestroy`, leaving dangling static references after scene unload.
- Files: `ScoreManager.cs` (DontDestroyOnLoad), `CameraRig.cs`,
  `ViewManager.cs`, `CheckpointManager.cs`, `TimeManager.cs`,
  `OverlayCameraController.cs`, `Tutorial/TutorialMaster.cs`,
  `Tutorial/InputSwitchManager.cs`, `Squirrels House/BackgroundSwitchManager.cs`,
  `LevelScoreManager.cs`.
- Impact: Stale `Instance` references, duplicate-manager races, inconsistent
  lifetime assumptions between scene-local and persistent managers.
- Fix approach: Extract a single `Singleton<T>` base (guarded `Awake`, null-out
  in `OnDestroy`) and standardize which managers are persistent.

**Debug output left in per-frame / hot paths:**
- Issue: `print(...)`, `Debug.Log`, and `Debug.DrawRay` calls run inside `Update`
  and physics/state-machine methods. `print` is a `Debug.Log` wrapper and
  allocates + logs every call.
- Files: `NPC/FoxScript.cs:289,339,382` (DrawRay + print in `PathFinding`/
  `GroundCheck`/`SearchForTarget`), `World/VineSegment.cs:98,124,149,165,205`,
  `World/Gate.cs:67`, `SceneInitializer.cs:20`.
- Impact: Log spam, GC pressure, and measurable per-frame cost in ship builds.
- Fix approach: Remove or gate behind `DebugSettings.ShowDebugLogs` /
  `[Conditional("UNITY_EDITOR")]`.

**Dead / commented-out code blocks:**
- Issue: Large commented blocks and abandoned logic remain in shipping files.
- Files: `UI/AcornArrow.cs:60-64`, `NPC/FoxScript.cs:33-34,412-424`,
  `NPC/Hawk.cs:215-218`, `UI/AcornCollectionIndicator.cs:92,226`,
  `World/WindGust.cs:48`.
- Impact: Reader confusion, obscures real behavior, rots over time.
- Fix approach: Delete; rely on git history.

**Outstanding TODOs:**
- `World/ReturnSign.cs:49` — `//TODO: Mateo do your thing man` (unfinished feature).
- `World/FoxBush.cs:97` — reachability check unimplemented (no nav mesh).
- `CameraRig.cs:21` — TODO to introduce a cutscene state machine.

## Known Bugs

**`Hawk.PlayerOutOfProximity` throws at runtime:**
- Symptoms: `throw new System.NotImplementedException();`
- Files: `Assets/Scripts/NPC/Hawk.cs:332-335`
- Trigger: `ProximityTrigger.OnTriggerExit2D` calls
  `proximityAlert.PlayerOutOfProximity(...)` (`ProximityTrigger.cs:30`) — any
  Hawk using a `ProximityTrigger` will throw when the player leaves range.
- Workaround: None; other `IProximityAlert` implementers (`FoxScript`,
  `BatScript`, `Owl`) provide empty/valid bodies. Replace with a no-op or real
  behavior.

**`Gate.Update` null-dereference and logic inversion:**
- Symptoms: When `playerMove` is null the guard at `Gate.cs:57`
  (`numChargeCollected == 0 && playerMove != null`) falls through and line 65
  dereferences `playerMove.GetPlayerState()` → `NullReferenceException`.
  Line 67 also calls `ResetCharges.GetInvocationList()` without a null check
  (NRE when no subscribers).
- Files: `Assets/Scripts/World/Gate.cs:55-70`
- Trigger: Scene where `FindAnyObjectByType<PlayerMove>()` returns null, or a
  reset with zero event subscribers.
- Workaround: Add explicit null guards; fix the boolean logic.

**Unchecked `GetComponent` / `FindGameObjectWithTag` results:**
- Symptoms: Components fetched and immediately dereferenced with no null check.
- Files:
  - `World/WaterKillTrigger.cs:27-28`, `World/RespawnTrigger.cs:11-12`,
    `NPC/PatrollingEnemy.cs:60-61`, `World/Mushroom.cs:74-76` — assume the
    colliding root has `PlayerLifeManager` / `Rigidbody2D`.
  - `NPC/Hawk.cs:74`, `NPC/FoxScript.cs:95`, `SceneInitializer.cs:48` —
    `GameObject.FindGameObjectWithTag("Player")` dereferenced with no null check;
    NRE if the Player tag is missing or the object spawns after these `Start`s.
- Impact: NRE crashes tied to scene setup / tag / execution-order assumptions.
- Fix approach: Null-check and log a clear error, or require refs via
  `[SerializeField]`.

## Security Considerations

**Local save data is unprotected (low risk for a single-player game):**
- Risk: All persistence uses `PlayerPrefs` with plaintext string keys
  (`Acorn_<level>_<id>`, `TotalScore`, `CurrentLevel`). Trivially editable by the
  player; no integrity/versioning.
- Files: `Assets/Scripts/SaveLoadManager.cs` (throughout).
- Current mitigation: None.
- Recommendations: Acceptable for a casual platformer. If leaderboards/anti-cheat
  ever matter, move to a checksummed save file. No secrets or credentials were
  found in the codebase.

**Debug utility can wipe all player data:**
- Risk: `DebugEditorMenu` exposes a "Reset PlayerPrefs" button and
  `SaveLoadManager.ClearAllSaveData()` calls `PlayerPrefs.DeleteAll()`.
- Files: `DebugEditorMenu.cs:15-19`, `SaveLoadManager.cs:230-234`.
- Current mitigation: Editor-only (once the class is correctly relocated).
- Recommendations: Ensure this never ships in a runtime code path.

## Performance Bottlenecks

**Per-frame allocations in AI / UI update loops:**
- Problem: New structs/arrays and heavy camera math run every frame per entity.
- Files:
  - `NPC/FoxScript.cs:278` — `new RaycastHit2D[2]` allocated every `PathFinding`
    call (once per frame while stalking/chasing); use a cached buffer +
    `Physics2D.RaycastNonAlloc`.
  - `UI/AcornArrow.cs:57-207` — for *every* acorn and gold acorn, each frame:
    multiple `WorldToViewportPoint` / `ScreenToWorldPoint` conversions plus many
    `new Vector3(...)`. O(acorns) camera round-trips per frame.
- Impact: GC churn and CPU cost that scales with enemy/acorn count.
- Improvement path: Cache buffers, early-out off-screen items, throttle update
  frequency, and reuse vectors.

**`Debug.DrawRay` / `print` in physics loops:** see Tech Debt — active in
`FoxScript.PathFinding` and `VineSegment.FixedUpdate` every physics step.

**Repeated `GetComponent<Image>()` in indicator refresh loops:**
- Problem: `AcornCollectionIndicator` calls `GetComponent<Image>()` inside index
  loops on every refresh instead of caching the `Image` references.
- Files: `UI/AcornCollectionIndicator.cs:308-358`.
- Improvement path: Cache the `Image[]` when indicators are instantiated.

**Empty `Start`/`Update` stubs:**
- Problem: `WaterKillTrigger` (and others generated from the default template)
  keep empty `Update()` bodies, which Unity still invokes each frame.
- Files: `World/WaterKillTrigger.cs:13-22`.
- Improvement path: Delete empty Unity messages.

## Fragile Areas

**Vine attach/detach state machine:**
- Files: `World/VineSegment.cs`
- Why fragile: Static `canAttach` shared across vines; input actions
  enabled/disabled from trigger callbacks (`OnTriggerEnter2D`/`Exit2D`) with a
  `static` gate make attach state order-dependent and easy to desync. Player
  position is hard-set from `Update` (`playerTransform.position = ...`),
  fighting physics.
- Safe modification: Make attach state instance-scoped; centralize input
  enable/disable; drive positioning from `FixedUpdate`.
- Test coverage: None.

**Enemy state machines (Hawk / Fox):**
- Files: `NPC/Hawk.cs` (345 lines), `NPC/FoxScript.cs` (459 lines)
- Why fragile: Large `switch`-based FSMs with many interdependent serialized
  tuning fields, cached player refs from `Start`, and no guards if the player
  ref becomes stale (e.g., respawn). `ResetObject` resets only a subset of state.
- Safe modification: Change one state at a time; re-verify all transitions;
  never assume `player`/`playerLifeManager` are still valid.
- Test coverage: None.

**Save/score coupling across statics + singleton:**
- Files: `SaveLoadManager.cs`, `ScoreManager.cs`, `LevelScoreManager.cs`,
  `CheckpointManager.cs`.
- Why fragile: Score is split between a `static int` and a `DontDestroyOnLoad`
  singleton; acorn state is spread across static `HashSet`s plus `PlayerPrefs`
  with no single source of truth. Scene restart / checkpoint reset logic
  (`ResetCurrentSceneAcorns`, `ResetCurrentSceneScore`) mutates several of these
  in tandem and is easy to get out of sync.
- Safe modification: Route all reads/writes through one owner; add asserts.

## Scaling Limits

**On-screen acorn indicator/arrow systems are O(n) per frame:**
- Current capacity: Fine for a handful of acorns per level.
- Limit: Cost grows linearly with acorn count due to per-acorn camera math each
  frame (`UI/AcornArrow.cs`) and per-slot component lookups
  (`UI/AcornCollectionIndicator.cs`).
- Scaling path: Cull off-screen, cache components, batch updates.

**PlayerPrefs as the save backend:**
- Current capacity: Adequate for level/score/per-acorn booleans.
- Limit: One key per acorn (`Acorn_<level>_<id>`) does not scale to large worlds
  and has no migration/versioning story.
- Scaling path: Serialized save file keyed by level.

## Dependencies at Risk

**Heavy coupling to third-party packages:**
- `Cysharp UniTask` — used for async gate sequences (`World/Gate.cs`). Cancellation
  handled via `destroyCancellationToken` (good), but async flows are untested.
- `Feel / MoreMountains MMFeedbacks` — `MMF_Player` used widely
  (`MMFPlayerExtensions.cs`, `Gate.cs`, `Mushroom.cs`, others). Version upgrades
  can break feedback APIs.
- `Dreamteck Splines`, `NaughtyAttributes`, `Unity.Cinemachine` — editor-coupled;
  upgrades may require rework.
- Risk: These live in-tree (vendored) with no lockfile/version manifest surfaced
  in scripts; upgrades are manual and untested.
- Impact: Silent breakage on Unity/package upgrades.
- Migration plan: Record exact versions in `STACK.md`; wrap third-party calls
  behind thin adapters where practical.

## Missing Critical Features

**No save-data versioning/migration:**
- Problem: `SaveLoadManager` writes raw `PlayerPrefs` keys with no schema version.
- Blocks: Safe changes to save format across releases; players lose/ corrupt
  progress on key-format changes.

**No cutscene/camera state machine:**
- Problem: `CameraRig.cs:21` flags this as a TODO; cutscene handling is ad hoc
  (`CutsceneEndHandler.cs`).
- Blocks: Reliable cutscene sequencing.

## Test Coverage Gaps

**Zero automated tests for first-party gameplay code:**
- What's not tested: All 69 game scripts. Every test file found belongs to
  vendored packages (`Assets/NaughtyAttributes/Scripts/Test/*`,
  `Assets/Feel/**/*Test*.cs`); there is no `Game.Tests` assembly, no EditMode or
  PlayMode tests, and no CI configuration in the repo.
- Files: entire `Assets/Scripts/` tree.
- Risk: High. Save/score logic, enemy FSMs, and the vine/gate systems are the
  most bug-prone areas and have no regression safety net. The
  `NotImplementedException` in `Hawk` and the `Gate.Update` NRE would be caught
  by even minimal tests.
- Priority: High for `SaveLoadManager`, `ScoreManager`, and `CheckpointManager`
  (pure-ish logic, easily unit-testable); Medium for enemy/vine PlayMode tests.

---

*Concerns audit: 2026-08-17*
