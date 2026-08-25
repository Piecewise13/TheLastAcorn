# Context — Cave camera zones

**Date:** 2026-08-20
**Scope:** `Level2-Cave.unity`, the camera rig, and the glide-totem story beat.
**Status:** decisions locked, ready to plan.

Not filed under a phase number — `.planning/` currently holds only `codebase/`, with no
`ROADMAP.md` or phase directories. Move this into a phase directory when the roadmap exists.

## Objective

The cave reads as a series of framed rooms rather than a scrolling side-view. Walking into a
room parks the camera so the whole room is visible; walking to the next room moves and resizes
the camera to that room's framing. A game event can claim the same framing system to look away
from the player — the glide-totem wall reveal is the first case. Where no zone applies, the
camera behaves exactly as it does in the overworld.

## What already exists

**A first pass at this is in the repo and is incomplete.** `Assets/Scripts/CaveCameraZoom.cs`
is a trigger volume that calls `StartForceZoom(amount, CameraState.CaveZoomed)` on enter and
`EndForceZoom` on exit. It only changes orthographic size — it never moves or locks the camera
— and it cannot hand off between rooms, because `StartForceZoom` returns early when the state
is already `CaveZoomed` (`PlayerCameraManager.cs:180`). It is not present in
`Level2-Cave.unity` at the moment.

**Camera bounds are already solved, by hand rather than with Cinemachine.** The virtual camera
does not track the player. `CameraGhost` clamps the player's position to a `PolygonCollider2D`
each `LateUpdate`, and the Cinemachine camera tracks that ghost — this is `boundedCameraTarget`
in `CameraRig`. `CaveCameraManager.prefab` is a prefab variant of `CameraManager.prefab` whose
main override is a five-point polygon. Cinemachine 3.1.6 is installed but its `Confiner2D` and
multi-camera priority blending are unused.

**Retargeting the camera already exists and is already used twice.**
`CameraRig.SetTrackingTarget` / `ResetTrackingTarget` is called by `PlayerAbilityManager`
(unlock cinematic), `PlayerUpgradeManager`, `UnlockZoomView`, and `PlayerCameraManager.Zoom`.

**Nothing drives the glide-totem beat.** `TotemRevealWall.BeginRevealAsync` has no callers
outside its own debug button, and `StartUnlockAbility` has no C# caller. The chain from "player
unlocks glide" to "wall reveals" does not exist yet.

**`Level2-Cave.unity` contains neither camera manager prefab.** Confirm in the editor how the
cave currently gets a camera at runtime before assuming anything about its rig.

## Decisions

### A zone is an anchor plus an orthographic size

Zones lock fully — the camera parks and does not move while the player is inside — and the size
is authored by hand per room. No polygon is needed for a zone; `CameraGhost` and its polygon
stay in place for overworld bounds and are simply not involved when a zone is active.
Activating a zone means pointing the tracking target at the zone's anchor and setting the target
zoom, which the rig can already do.

Zones are scene GameObjects, not assets, because an anchor is a scene-space position.

Consequence to accept: hand-authored orthographic size guarantees a room fits vertically at any
aspect ratio, but the horizontal fit varies with it. If the game ships at more than one aspect,
wide rooms authored at one aspect may crop at another. A computed-from-bounds sizing option was
rejected for authoring control; it remains the fix if this becomes a problem.

### A camera arbiter becomes the single owner of tracking target and zoom

Callers request a claim with a priority and hold it. Releasing falls back to whatever claim is
underneath. An empty stack is the default overworld follow, so "no zones means overworld
behaviour" is the base of the stack rather than a special case.

Priority, highest first: **ability-unlock cinematic → event zone → player manual zoom-out →
room zone → default follow.**

This replaces the `CameraState` enum's hand-written guards, and that replacement is the point
rather than a side effect. Two concrete bugs come from having no arbiter: the guard at
`PlayerCameraManager.cs:180` is why room-to-room handoff cannot work, and the unmediated
`ResetTrackingTarget()` calls in `PlayerCameraManager.Zoom`, `PlayerAbilityManager`, and
`UnlockZoomView` mean a player input can silently steal the camera mid-sequence — a wall reveal
would finish pointed at the squirrel.

### Room zones block manual zoom-out

Cave rooms are framed to show the entire room, so zooming out reveals nothing. This preserves
today's behaviour. The priority order above still ranks manual zoom above room zones, so the
rule can be relaxed per zone later without a redesign.

### Zones are claimed by awaiting them

A sequence does `await zone.Hold(token)` around its beat. Release is guaranteed by the `finally`
and cancellation is inherited, so a death or scene unload mid-beat returns the camera without a
dedicated cleanup path. Thin `Activate()` / `Deactivate()` wrappers stay available for
inspector and AnimationEvent wiring, but the awaited form is the real mechanism. This follows
the project's UniTask convention (see `Assets/Scripts/World/Gate.cs`) rather than introducing a
state machine that has to be unwound by hand.

### The wall reveal is an ordinary zone

It gets its own anchor and orthographic size, and the event activates it. Zones therefore carry
an activation mode — trigger, event, or both — so a wall zone does not fire when the player
walks past it.

### A new sequence script owns the glide-totem beat

Order: unlock → freeze the player → claim the wall zone → `await BeginRevealAsync` → release →
restore control. The camera system stays a service that knows nothing about totems, and
`TotemRevealWall` keeps its current shape. Since this beat has no driver at all today, this is
new wiring rather than a refactor.

### Overlapping zones sort themselves out; gaps revert to follow

Overlap needs no rule of its own. A room zone's claim is held for as long as the player is inside
it, so walking from room A into an overlap with room B puts B's claim on top, leaving A entirely
keeps B on top, and walking back into A returns to A because its claim was underneath the whole
time. Rooms should therefore overlap at doorways rather than tile exactly.

Leaving every zone reverts to overworld follow immediately, matching the base rule that an empty
claim stack is the default behaviour. The cost is that a genuine gap between two rooms produces a
visible follow-then-reframe stutter, so gap detection in the authoring harness is the mitigation
rather than an extra. These two decisions are paired.

### Transitions are a fixed duration with an ease curve

Global default, per-zone override. Position and zoom share the same timing so they arrive
together.

This replaces an earlier assumption that the existing exponential smoothing in
`PlayerCameraManager.Update` would be reused for zone moves. Exponential smoothing asymptotes
rather than arriving, which reads as drifting rather than deliberate for a cinematic reframe. The
existing smoothing stays as-is for the player's own zoom.

### A zone applied on respawn or scene entry snaps

The player is correctly framed on the first frame they see.

Spawn resolves its zone with a point query against the registry, not through triggers. This is
not a preference: teleporting the player to a checkpoint inside a trigger they already overlap
does not re-fire `OnTriggerEnter2D`, so a trigger-driven respawn would silently fail to re-apply
the zone.

### The player is frozen for any cinematic beat

Movement is disabled for the duration and restored when the camera returns, matching what
`PlayerAbilityManager.StartUnlockAbility` already does. This is load-bearing for the arbiter:
a frozen player cannot walk into another zone mid-beat, so claim contention during cinematics
is prevented by design rather than resolved at runtime.

## Authoring harness

Placement and zoom are both hand-authored, so the tooling to author them is part of the work,
not a follow-up. A zone's entire authored state is a position and an orthographic size, and both
are directly drawable — the framed region is a rectangle centred on the anchor with half-height
equal to the orthographic size and half-width equal to that times the target aspect ratio.

**Edit-mode gizmos are the primary tool.** Each zone draws its framed rectangle in the scene
view, using a target aspect configured on the manager rather than the scene view's own aspect,
which is just whatever size the window happens to be. Placement becomes visual with no play
mode involved. A scene-view handle on the rectangle edge drags the orthographic size, recorded
through `Undo`.

**A `CameraZoneManager` provides the per-zone buttons.** It is also the runtime registry the
arbiter uses to resolve zones for event activation, so there is one list rather than two. Its
editor draws a row per zone:

- **Frame in scene view** — aligns the scene view camera to the zone's exact framing, so what
  you see is what the player sees.
- **Capture from scene view** — the inverse, writing the current scene view position and size
  into the zone. Pan and zoom until it looks right, then capture. Expected to be the fastest
  loop in practice.
- **Activate / Release** — in play mode, pushes a debug claim through the arbiter so the real
  transition and easing can be checked, not just the static frame.
- **Step next / previous** — walk the zone list in order.
- **Validation** — flag zones with no collider while set to trigger activation, duplicate ids,
  and a zero orthographic size.
- **Coverage check** — flag any room zone whose trigger overlaps no neighbour, since reverting on
  exit means an unintended gap shows up in play as a follow-then-reframe stutter. This is the
  mitigation for that decision, not a nicety.

All editor code lives in `Assets/Scripts/Editor/` and any runtime-side buttons are guarded with
`#if UNITY_EDITOR`. This is explicit because runtime scripts importing `UnityEditor` is an open
bug in this project (Bug Tracker #2) that breaks standalone builds.

## Assumptions

Carried unless corrected:

- `Assets/Scripts/CaveCameraZoom.cs` is deleted; the claim-based zone replaces it entirely.
- Overworld behaviour in `Level1.unity` is untouched. Zones are opt-in per scene.
- The existing exponential smoothing in `PlayerCameraManager.Update` keeps driving the player's
  own zoom; only zone transitions use the new duration-and-curve path.

## Success criteria

- Walking between two adjacent cave rooms moves and resizes the camera to each room's authored
  framing, with no residue from the previous room.
- Leaving all zones restores overworld follow behaviour.
- The glide totem unlock frames the wall, plays the reveal, and returns to the player, and a
  player input during it cannot steal the camera.
- A room's placement and zoom can be authored without entering play mode.
- Backtracking through overlapping rooms returns to the previous room's framing with no manual
  bookkeeping.
- A respawn inside a room is already correctly framed on its first visible frame.
