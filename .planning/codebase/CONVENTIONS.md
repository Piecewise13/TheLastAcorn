# Coding Conventions

**Analysis Date:** 2026-08-17

Conventions below are derived from the game's own C# scripts under `Assets/Scripts/`, `Assets/Player/`, `Assets/Views/`, and `Assets/Visuals/`. Third-party plugin code (`Assets/Plugins/`, `Assets/NaughtyAttributes/`, `Assets/Feel/`, Dreamteck, MoreMountains) does NOT follow these conventions and should not be used as a style reference.

There is **no enforced style tooling** in this repo — no `.editorconfig`, `.editorconfig`-based Rider ruleset, `omnisharp.json`, or analyzers were found. Conventions are de-facto, learned from existing code, and are applied inconsistently. When adding code, match the dominant pattern described here rather than the outliers.

## Naming Patterns

**Files:**
- One `MonoBehaviour`/class per file, `PascalCase.cs`, filename matches the type name. Example: `Assets/Scripts/World/Acorn.cs` → `class Acorn`.
- Editor scripts live in an `Editor/` subfolder and use a `...Window` / `...Editor` suffix: `Assets/Scripts/TreeTool/Editor/TreeBuilderWindow.cs`, `TreePieceEditor.cs`.

**Types (classes, interfaces, enums):**
- `PascalCase` for classes and enums: `ScoreManager`, `ViewManager`, `PatrollingEnemy`, `ViewID`.
- Interfaces use the `I` prefix: `ICollectible` (`Assets/Scripts/ICollectible.cs`), `IDamageable` (`Assets/Scripts/Interfaces.cs`).
- Enum members are `PascalCase`: `ViewID { HUD, PauseMenu, AbilityUnlockMenu, Settings }` (`Assets/Scripts/Views/ViewManager.cs`).

**Methods:**
- `PascalCase` for all methods, public and private: `SpawnAcorn()`, `OnCollected()`, `GenerateAcornId()`, `AddScore()`.
- Unity lifecycle methods use the engine's casing (`Awake`, `Start`, `Update`, `FixedUpdate`, `OnTriggerEnter2D`, `OnDestroy`).
- Event-handler methods are commonly prefixed with `Handle` or `On`: `HandleAbilityUnlocked` (`Assets/Views/HUD/HUDViewBase.cs`).

**Fields:**
- Private instance fields are `camelCase` with **no** `_` prefix and **no** `m_` prefix: `private Rigidbody2D rb;`, `private CheckpointManager checkpointManager;`, `private bool isShaking;`.
- Serialized private fields are also `camelCase`: `[SerializeField] private float speed = 2f;`.
- `const` fields are `PascalCase`: `private const string LevelKey = "CurrentLevel";` (`Assets/Scripts/SaveLoadManager.cs`).
- Static private fields are `camelCase`: `private static int score;`, `private static HashSet<string> pendingAcorns`.
- Inconsistency to be aware of: some fields omit the access modifier entirely (`int currentLives;` in `PlayerLifeManager.cs`, `[SerializeField] int value = 1;` in `Acorn.cs`). Prefer an explicit `private`.

**Properties:**
- `PascalCase`, frequently expression-bodied getters that wrap a backing field: `public int Value => value;`, `public string AcornId => acornId;` (`Assets/Scripts/World/Acorn.cs`), `public int CurrentScore => score;`.
- Singleton accessor is an auto-property with a private setter: `public static ScoreManager Instance { get; private set; }`.

**Events:**
- C# `event Action<T>` in `PascalCase`, often prefixed `On`: `public event Action<int> OnScoreChanged;` (`ScoreManager`), `OnPlayerRespawn`, `OnAbilityUnlocked`.

## Code Style

**Formatting:**
- No formatter configured. Indentation is **4 spaces**; Allman braces (opening brace on its own line) is the dominant style for methods and types.
- Real files contain inconsistent/stray indentation (e.g. `Acorn.SpawnAcorn` mixes indentation levels). Do not copy these artifacts; keep new code cleanly indented.
- `var` is used for local variables when the type is obvious from the right-hand side (`var newView = Instantiate(...)`), but explicit types are also common (`Vector2 newPos = ...`). Either is acceptable; match the surrounding method.

**Linting:**
- None. No ESLint-equivalent (Roslyn analyzers, StyleCop) is configured.

**Nullable annotations:**
- Not globally enabled, but a few newer files opt into null-forgiving syntax: `[SerializeField] private Transform shakeTarget = null!;` and `public Transform Transform = null!;` (`Assets/Scripts/Effects/ShakeEffect.cs`). Treat as file-local, not project-wide.

## Import Organization

**`using` order:** No strict ordering; the common shape is:
1. `System` / `System.*` and `System.Collections.Generic`
2. Third-party namespaces (`Cysharp.Threading.Tasks`, `NaughtyAttributes`)
3. `UnityEngine` and `UnityEngine.*` (e.g. `UnityEngine.SceneManagement`, `UnityEngine.UI`, `UnityEngine.Events`)

- Watch for duplicated `using` blocks — `Assets/Scripts/Views/ViewBase.cs` literally repeats its three `using` lines twice. Do not replicate; de-duplicate when editing.

**Path aliases / namespaces:**
- The overwhelming majority of game scripts declare **no namespace** and live in the global namespace. This is the default convention.
- Only two game files use a namespace: `namespace Common` (`Assets/Scripts/EventHandler.cs`) and `namespace ScriptEffects` (`Assets/Scripts/Effects/ShakeEffect.cs`). Namespaces are the exception, not the rule.

## MonoBehaviour Patterns

**Component caching in `Awake`/`Start`:**
- Cache `GetComponent<T>()` results into private fields inside `Awake()` (preferred) or `Start()`:

```csharp
private void Awake()
{
    rb = GetComponent<Rigidbody2D>();
    rb.gravityScale = 0f;
    rb.freezeRotation = true;
}
```
(`Assets/Scripts/NPC/PatrollingEnemy.cs`)

**Component requirements:**
- Declare hard dependencies with `[RequireComponent(...)]` on the class: `[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]` (`PatrollingEnemy`).

**Singletons (dominant manager pattern):**
- Managers use a static `Instance` singleton, initialized in `Awake`, with duplicate-destroy guarding. Two variants appear; the fuller one persists across scenes:

```csharp
public static ScoreManager Instance { get; private set; }

private void Awake()
{
    if (Instance == null)
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    else Destroy(gameObject);
}
```
(`Assets/Scripts/ScoreManager.cs`; also `CheckpointManager`, `ViewManager`, `TimeManager`, `LevelScoreManager`, `CameraRig`, and others.)

- Consumers call through the static accessor: `CheckpointManager.Instance.SetCheckpoint(...)`, `ScoreManager.Instance.AddScore(Value)`. Be aware this creates implicit ordering/lifetime coupling.

**Physics vs. frame updates:**
- Movement and rigidbody work goes in `FixedUpdate` using `rb.MovePosition` / `Vector2.MoveTowards` (`PatrollingEnemy.FixedUpdate`). Input polling and visual-only logic go in `Update`.

**Trigger/collision handlers:**
- Early-return on tag mismatch, then act. Standard idiom:

```csharp
private void OnTriggerEnter2D(Collider2D other)
{
    if (!other.CompareTag("Player")) return;
    snakeAttack?.Play();
    ...
}
```
Use `CompareTag(...)` (not `tag ==`) and guard nullable references with `?.`.

**Event subscription lifecycle:**
- Subscribe to manager events after confirming the manager exists (sometimes via a coroutine poll), and always unsubscribe in `OnDestroy`:

```csharp
IEnumerator SubscribeWhenReady()
{
    while (PlayerAbilityManager.Instance == null)
        yield return null;
    PlayerAbilityManager.Instance.OnAbilityUnlocked += HandleAbilityUnlocked;
}

private void OnDestroy()
{
    if (PlayerAbilityManager.Instance == null) return;
    PlayerAbilityManager.Instance.OnAbilityUnlocked -= HandleAbilityUnlocked;
}
```
(`Assets/Views/HUD/HUDViewBase.cs`)

## Serialized Field Usage

- Expose Inspector-editable data with `[SerializeField] private` (keep fields private, serialize explicitly) rather than `public` fields. Example: `[SerializeField] private float speed = 2f;`.
- Provide sensible default values inline where relevant: `= 2f`, `= 20f`, `isGoldenAcorn = false`.
- Group and document fields for the Inspector:
  - `[Header("...")]` to section the Inspector: `[Header("Feedback")]`, `[Header("Damage Settings")]`, `[Header("Player Components")]`.
  - `[Tooltip("...")]` for per-field explanations: `[Tooltip("Units / second")]`, `[Tooltip("Leave null to use parent object's transform")]`.
- Expose read-only access to serialized data via expression-bodied properties instead of making the field public (`public int Value => value;`).
- `NaughtyAttributes` (package present, used heavily in one game file) provides conditional Inspector attributes such as `[ShowIf(nameof(shakeChildren))]` (`Assets/Scripts/Effects/ShakeEffect.cs`). It is available project-wide but rarely used in gameplay code.

## Async / Coroutines

- Two async styles coexist:
  - **UniTask** (`Cysharp.Threading.Tasks`, `com.cysharp.unitask`) for view transitions and awaitable flows. `ViewBase` / `ViewManager` return `UniTask` / `UniTask<T>`, accept `CancellationToken`, and use `UniTask.CompletedTask`:

```csharp
public virtual async UniTask Show(CancellationToken token)
{
    gameObject.SetActive(true);
    await RunAsync(token);
}
```
(`Assets/Scripts/Views/ViewBase.cs`)

  - **Classic coroutines** (`IEnumerator` + `StartCoroutine`) survive in older files for timing/poll-until-ready logic (`HUDViewBase.SubscribeWhenReady`, `PlayerLifeManager` immunity timing). Treat these as legacy.
- **UniTask is the standard for all new async code, gameplay included** — see `.cursor/rules/unitask-over-coroutines.mdc` for the required patterns and the narrow list of cases where a coroutine is still acceptable. `Assets/Scripts/World/Gate.cs` is the gameplay reference: a synchronous entry point calls `Sequence(destroyCancellationToken).Forget()`, and the `async UniTask` body threads that token through `UniTask.Yield` / `WaitUntil` / `WhenAll`. Use a `CancellationTokenSource` for flows needing explicit cancellation (see `ViewManager.viewTransitionCts`).

## Error Handling

- **No exception-based error handling in gameplay code.** No `try`/`catch`/`throw` was found in the game scripts; the game relies on Unity's runtime behavior and defensive guards instead.
- Preferred defensive patterns:
  - Null-conditional invocation: `OnScoreChanged?.Invoke(score);`, `snakeAttack?.Play();`.
  - Early-return guards: `if (viewStack.Count == 0) return;`, `if (!other.CompareTag("Player")) return;`, `if (string.IsNullOrEmpty(currentSceneName)) return;`.
  - Clamping instead of throwing: `if (score < 0) score = 0;` (`ScoreManager.ResetCurrentSceneScore`).
- **Logging:** use `UnityEngine.Debug`. `Debug.LogWarning` for recoverable/unexpected states (`EventHandler.TriggerEvent` when an event name is missing), `Debug.Log` for diagnostics. There is a project convention of prefixing log messages with a bracketed source tag and gating verbose logs behind a debug flag:

```csharp
if (ShowDebugLogs) Debug.Log($"[SaveLoadManager] DEBUG MODE: Skipping save level name: {levelName}");
```
(`Assets/Scripts/SaveLoadManager.cs`, gated by `DebugSettings.Instance.ShowDebugLogs`.) Use `$"..."` string interpolation for log/message formatting.

## Persistence Convention

- Save/load is centralized in a `static` utility class `SaveLoadManager` (`Assets/Scripts/SaveLoadManager.cs`) backed by `PlayerPrefs`. Keys are `const string` fields; call `PlayerPrefs.Save()` after writes. A `DebugSettings` singleton flag (`DisablePersistence`) short-circuits saves during testing. Route all persistence through this class rather than calling `PlayerPrefs` directly from gameplay code.

## Comments

**When to comment:**
- Short `//` comments explain intent above non-obvious blocks (`// Generate unique ID if not set`, `// Reached the target? Flip to the other one.`).
- Unfinished work is flagged with `//TODO:` (occasionally attributed to a teammate). Existing examples: `Assets/Scripts/World/ReturnSign.cs`, `Assets/Scripts/World/FoxBush.cs`, `Assets/Scripts/CameraRig.cs`.
- Auto-generated Unity boilerplate comments (`// Start is called once before the first execution of Update...`) are frequently left in place. These add no value — omit them in new code.

**XML doc comments (`///`):**
- Used sparingly on public API surfaces. `ViewManager.PushView` documents `<summary>`, `<param>`, and `<returns>`. Prefer `///` docs on non-trivial public methods of shared systems (managers, `ViewBase`), but they are not required everywhere.

## Function & Module Design

- Methods are generally small and single-purpose. One-liners are sometimes collapsed onto a single line (`public int GetScore(){return score;}` in `ScoreManager`) — acceptable but expression-bodied members are cleaner for new code.
- No barrel/partial-class organization; one type per file. Multiple small related types occasionally share a file when tightly coupled (`NamedEvent` + `EventHandler` in `EventHandler.cs`; the enum `ViewID` sits alongside `ViewManager`).
- Shared cross-cutting utilities are `static` classes (`SaveLoadManager`) or extension-method classes (`MMFPlayerExtensions.cs`).

---

*Convention analysis: 2026-08-17*
