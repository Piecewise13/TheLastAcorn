# Testing Patterns

**Analysis Date:** 2026-08-17

## Current State: No Project Tests Exist

**There are no automated tests for the game's own code.** The project has the Unity Test Framework package installed but no test assemblies, no EditMode/PlayMode test folders, and no test files authored for `The Last Acorn`'s gameplay scripts.

Evidence gathered from a full-repo scan:
- No `*.asmdef` files reference the test framework (`UnityEngine.TestRunner` / `UnityEditor.TestRunner` / `nunit.framework`) outside third-party plugins.
- No `EditMode` or `PlayMode` directories exist anywhere under `Assets/`.
- The only `*.asmdef` files present belong to third-party plugins: `Assets/Plugins/Dreamteck/.../Dreamteck.*.asmdef` and `Assets/NaughtyAttributes/Scripts/.../NaughtyAttributes.*.asmdef`. The game's own scripts have **no** assembly definitions at all — they compile into Unity's default `Assembly-CSharp`.
- Files matching `*test*` in the repo are either **plugin demo/sample code** (`Assets/NaughtyAttributes/Scripts/Test/*.cs` — these are attribute demos, not unit tests), **scenes** (`Assets/Scenes/Miscellaneous/Testing Scenes/*.unity`, used for manual in-editor playtesting), or **performance-test JSON** left over from a package (`Assets/Resources/PerformanceTestRunSettings.json`).
- `SaveLoadManager.ClearAllSaveData()` carries a `// (for testing or reset)` comment, indicating testing is currently done **manually in the editor**, not via an automated harness.

## Test Framework

**Installed but unused for project code:**
- `com.unity.test-framework` **1.6.0** (declared in `Packages/manifest.json`). This is the Unity Test Framework (UTF), which wraps **NUnit 3** and supports two test modes: **EditMode** (fast, no Play loop) and **PlayMode** (runs in a live scene / player).
- Access via Unity Editor: **Window → General → Test Runner**.
- No assertion library beyond NUnit's `Assert` and Unity's `UnityEngine.Assertions` / `UnityEngine.TestTools` is configured.

**Run Commands:**
There are no tests to run today. Once tests exist, they run from the Test Runner window or headlessly via the Unity CLI:
```bash
# EditMode tests, headless (adjust Unity path/version to the installed editor)
Unity -runTests -batchmode -projectPath . -testPlatform EditMode -testResults results-editmode.xml

# PlayMode tests, headless
Unity -runTests -batchmode -projectPath . -testPlatform PlayMode -testResults results-playmode.xml
```

## Test File Organization

Not established. **Recommended** layout when tests are introduced (this is the Unity-standard convention):
```
Assets/
  Tests/
    EditMode/
      EditMode.asmdef            # references game asmdef(s) + test framework
      ScoreManagerTests.cs
      SaveLoadManagerTests.cs
    PlayMode/
      PlayMode.asmdef            # includePlatforms handled by UTF
      AcornCollectionTests.cs
```

**Naming (recommended):** `<TypeUnderTest>Tests.cs`, one test fixture class per production type.

## Mocking

- No mocking framework (NSubstitute, Moq) is present. If introduced, add it via NuGetForUnity or as a DLL under `Assets/Plugins/`.
- **Key testability obstacle:** most stateful systems are `MonoBehaviour` singletons accessed statically (`ScoreManager.Instance`, `CheckpointManager.Instance`, `SaveLoadManager` is a `static` class over `PlayerPrefs`). These are hard to mock/isolate. See "How Tests Would Be Added" below for the practical seam that already exists.

## Fixtures and Factories

- None exist. For PlayMode tests, the standard approach is to build objects in `[SetUp]` with `new GameObject().AddComponent<T>()` and tear them down with `Object.Destroy` in `[TearDown]`.

## Coverage

- **No coverage measured or enforced.** The `com.unity.testtools.codecoverage` package is not installed. Add it if coverage reporting is desired; it produces reports via the Test Runner or CLI `-enableCodeCoverage`.

## How Tests Would Be Added

The project already has one strong seam that makes adding tests realistic:

1. **Isolate pure logic first.** `ScoreManager` and `SaveLoadManager` contain plain integer/string logic (score add/reset/clamp, acorn-id generation via `Acorn.GenerateAcornId`, key formatting) that can be exercised with **EditMode** tests. `SaveLoadManager` already supports a debug bypass (`DebugSettings.Instance.DisablePersistence`) so tests can avoid touching real `PlayerPrefs`; alternatively call `SaveLoadManager.ClearAllSaveData()` in `[SetUp]`/`[TearDown]`.

2. **Create test assemblies.** Add `Assets/Tests/EditMode/EditMode.asmdef` and `Assets/Tests/PlayMode/PlayMode.asmdef`, each referencing the Unity test framework. Because the game code currently lives in the default `Assembly-CSharp` (no game `.asmdef`), the simplest path is to add a game assembly definition (e.g. `Assets/Scripts/Game.asmdef`) so test assemblies can reference it explicitly; otherwise EditMode tests can only see code that is also assembly-def'd. This is the main setup cost.

3. **Write EditMode unit tests** for deterministic logic:
```csharp
using NUnit.Framework;

public class ScoreManagerTests
{
    [Test]
    public void ResetCurrentSceneScore_ClampsAtZero()
    {
        // Arrange: build a ScoreManager on a GameObject, seed pending acorns...
        // Act / Assert with Assert.AreEqual(...)
    }
}
```

4. **Write PlayMode tests** for MonoBehaviour/scene behavior (collision, respawn, view stack) using `[UnityTest]` + `IEnumerator` so the Play loop advances:
```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

public class AcornCollectionTests
{
    [UnityTest]
    public IEnumerator OnCollected_AddsScore()
    {
        // Arrange scene objects (ScoreManager, CheckpointManager, Acorn)
        yield return null;            // let Awake/Start run
        // Act: acorn.OnCollected();
        // Assert: ScoreManager.Instance.CurrentScore increased
    }
}
```

5. **Reduce singleton coupling over time** to make more of the codebase unit-testable (extract pure logic out of `MonoBehaviour`s, inject dependencies instead of reaching for `.Instance`). This is optional but is the single biggest lever for test coverage given the current architecture.

## Test Types

- **Unit tests:** None. Best initial target: `ScoreManager`, `SaveLoadManager` logic (EditMode).
- **Integration / PlayMode tests:** None. Candidates: acorn collection → score/save flow, checkpoint respawn (`CheckpointManager.OnPlayerRespawn`), view stack push/pop (`ViewManager`).
- **E2E tests:** None, and not applicable for this project type.
- **Manual testing:** Current practice — dedicated scenes under `Assets/Scenes/Miscellaneous/Testing Scenes/` and a `DebugSettings` toggle for disabling persistence during playtests.

---

*Testing analysis: 2026-08-17*
