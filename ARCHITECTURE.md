# SpatialDebugger — architecture

A Meta Quest 3 mixed-reality tool for debugging physical electronics. You point
at something on a breadboard, ask, and the answer appears in the air next to the
actual wire.

```
physical electronics
        ↓
Quest passthrough camera          ← NOT IMPLEMENTED (see "Future: perception")
        ↓
multimodal perception             ← seam exists, mock today
        ↓
AI debugging / reasoning          ← seam exists, deterministic scenarios today
        ↓
structured spatial actions        ← IMPLEMENTED, the core of the system
        ↓
Quest mixed-reality annotations   ← IMPLEMENTED
```

## The one idea that holds it together

**Every annotation is expressed relative to the target the user selected, not
in world coordinates.**

The user points at a spot and pinches. That spot becomes a `SpatialTarget` — a
transform in the room. Everything after that is expressed in *its* local space:
`[0, 0.15, 0]` means "15 cm above the thing you pointed at", whatever and
wherever that is.

This is what makes the rest tractable:

- The backend never needs to know real Quest world coordinates, so a mock
  response is indistinguishable from a real one.
- An LLM can emit useful spatial output from text alone — it only has to reason
  about *relative* geometry, which it can do.
- When computer vision eventually lands, it changes exactly one thing: where
  the `SpatialTarget` transform sits. Nothing downstream changes.

## Layers

```
Unity/Assets/SpatialDebugger/
├── Core/          SpatialAction protocol, SpatialTarget, config
├── AI/            AIClient (UnityWebRequest), request/response, JSON parsing
├── Annotations/   SpatialActionDispatcher + one renderer per action type
├── Interaction/   IPointerSource, SpatialRaycaster, SpatialTargetController
├── UI/            World-space control panel
└── Demo/          On-device scenarios that need nothing external

Unity/Assets/Editor/
├── SpatialDebuggerProjectSetup.cs   XR loader, OpenXR features, Meta config
├── SpatialDebuggerSceneSetup.cs     Builds the scene from scratch
└── SpatialDebuggerBuild.cs          Headless Android build

backend/
├── app/models.py       The wire protocol (mirrored in Core/SpatialAction.cs)
├── app/scenarios.py    Deterministic demo answers
├── app/providers/      mock | openai (reasoning) | omni (perception)
└── tests/              54 tests
```

## The SpatialAction protocol

One flat JSON object per annotation. Deliberately not a class hierarchy: Unity
deserialises this on-device, and a flat shape keeps parsing free of reflection —
which IL2CPP managed stripping on Quest would otherwise break.

```json
{
  "type": "warning",
  "space": "target",
  "position": [0.0, 0.15, 0.0],
  "text": "Possible incorrect connection"
}
```

| type | needs | renders as |
|------|-------|-----------|
| `label` | `position`, `text` | caption on a leader line down to the point |
| `warning` | `position`, `text` | red caption with a pulsing cross badge |
| `marker` | `position` | pin: sphere on a stalk with a ground ring |
| `arrow` | `from`, `to`, optional `text` | shaft + cone head, tip lands exactly on `to` |
| `highlight` | `position`, optional `radius` | nested translucent spheres + footprint disc |
| `clear` | — | removes every annotation |

Optional on all: `id`, `color` (`#RRGGBB`), `scale`, `duration` (0 = persist).

`space` is `target` (default) or `world`. Almost everything should be `target`.

Python side: `backend/app/models.py`. C# side: `Core/SpatialAction.cs`. They are
kept in step by hand — there is no codegen, and that is a deliberate hackathon
trade-off. `backend/tests/test_actions.py` pins the Python half.

## Request flow

```
   [ Ask AI ] button
        │
        ▼
   DemoAnalysis.Run()
        │
        ├──► AIClient ──POST /analyze──► FastAPI ──► provider (mock | openai)
        │        │                                        │
        │        │◄────────── AIResponse ─────────────────┘
        │        │
        │   any failure: no route to host, DNS, timeout,
        │   4xx/5xx, 200-with-junk
        │        │
        │        ▼
        └──► DemoScenarios (on-device, no network)
                 │
                 ▼
        SpatialActionDispatcher.Dispatch(response)
                 │
     ┌───────────┼───────────┬──────────┬─────────────┐
     ▼           ▼           ▼          ▼             ▼
  Label      Warning      Marker      Arrow      Highlight
 Renderer    Renderer    Renderer   Renderer     Renderer
     └───────────┴───────────┴──────────┴─────────────┘
                 │
                 ▼
        parented under the SpatialTarget
```

There is no failure path that shows the user an error instead of an answer. The
backend degrades to its own offline scenarios; the client degrades to *its*
offline scenarios if the backend is unreachable at all.

## Interaction

`IPointerSource` is a one-method abstraction over "where is the user pointing
and did they just commit":

| Source | Ray | Commit | Priority |
|--------|-----|--------|----------|
| `MetaHandPointerSource` | `OVRHand.PointerPose` | index pinch | 40 |
| `MetaControllerPointerSource` | rig hand anchor | index trigger | 30 |
| `EditorMousePointerSource` | `Camera.ScreenPointToRay` | left click | 20 |
| `HeadGazePointerSource` | camera forward | space / click / API call | 10 |

The highest-priority *live* source wins each frame. This is why the whole
pipeline can be exercised in Play mode on a Mac with no headset.

`SpatialRaycaster` resolves a ray to a point in three tiers:

1. **Meta depth / MRUK surfaces** — real geometry. Optional; see below.
2. **Unity physics colliders.**
3. **A projected point at a fixed distance** — so pointing at empty space still
   places a target. This tier is why targeting cannot fail.

### Why the UI is not a Canvas

The world-space panel is built from box colliders and procedural quads, driven
by the same `IPointerSource`. Two reasons:

- This project is Input-System-only (`activeInputHandler: 1`), which rules out
  Meta's `OVRInputModule` — it is `ENABLE_LEGACY_INPUT_MANAGER`-gated.
- The Interaction SDK's canvas path needs a `PointableCanvas` +
  `PointableCanvasModule` + `RayInteractable` + `ClippedPlaneSurface` +
  `BoundsClipper` graph, none of which can be verified without a headset.

A collider the existing pointer already hits is far less to get wrong, and it
behaves identically under a mouse in the Editor and a pinch on device.

## Provider seams

```
Huawei OMNI ── camera/video + speech/audio ──► scene description
   app/providers/omni_provider.py  (PerceptionProvider)

OpenAI ── scene description + question ──► speech + SpatialActions
   app/providers/openai_provider.py  (ReasoningProvider)
```

Both import their SDK lazily and both are optional.
`select_reasoning_provider()` never returns `None` — an unusable provider
yields the mock. `/ask` and `/analyze` catch provider failures and answer from
the offline scenarios rather than surfacing a 5xx into a headset.

## Future: perception (NOT IMPLEMENTED)

The intended pipeline, and what the *installed* SDK actually offers for each
step. Nothing below is built; this is a map, not a claim.

```
Quest passthrough camera
        │   Meta.XR.PassthroughCameraAccess          (in the MRUK package)
        │     one component per eye, CameraPosition = Left | Right
        │     GetTexture()  -> Texture (GPU)
        │     GetColors()   -> NativeArray<Color32> (CPU)
        │     Intrinsics    -> FocalLength, PrincipalPoint,
        │                      SensorResolution, LensOffset
        ▼
image + intrinsics + camera pose
        │   OMNI or OpenAI vision, via AnalyzeRequest.image_base64
        ▼
2D detection: "the LED's anode is at pixel (412, 233)"
        │   PassthroughCameraAccess.ViewportPointToRay(Vector2, Pose?)
        │   and .GetCameraPose()
        ▼
ray in world space
        │   Meta.XR.EnvironmentRaycastManager.Raycast(ray, out hit, maxDistance)
        │     depth-sensor based, no Space Setup required, device only
        │   or MRUKRoom.Raycast(ray, maxDist, new LabelFilter(), out hit, out anchor)
        │     captured planes, requires Space Setup
        ▼
world point
        │
        ▼
SpatialTarget.transform.position = world point
        │
        ▼
...and every existing annotation renderer works unchanged.
```

Meta ships a **worked reference implementation of exactly this loop** in the
Core package: `Scripts/BuildingBlocks/AIBlocks` — `ObjectDetectionAgent`,
`ObjectDetectionVisualizer`, `ImageSegmentation*`, `SegmentationMask3DVisualizer`,
`DepthTools`, `DepthTextureAccess`. Read that before writing any of it.

**What is currently switched off**, and would need enabling first:

| Needed | Current state |
|--------|---------------|
| `horizonos.permission.HEADSET_CAMERA` | absent from `AndroidManifest.xml` |
| `OVRProjectConfig.isPassthroughCameraAccessEnabled` | `0` |
| `com.oculus.permission.USE_SCENE` (for the depth raycast) | now enabled via `sceneSupport`; re-run the setup tool |

**What is not available here**: `Meta.XR.EnvironmentDepth.EnvironmentDepthManager`
compiles a real provider only under `XR_OCULUS_4_2_0_OR_NEWER` or
`OPEN_XR_META_2_1_OR_NEWER`. Neither `com.unity.xr.oculus` nor
`com.unity.xr.meta-openxr` is installed, so `IsSupported` returns false. The
depth path that *does* work is MRUK's `EnvironmentRaycastManager`. There is no
`WebCamTexture` or Camera2 route to the passthrough feed — it is
`PassthroughCameraAccess` or nothing.

The last two lines are the point: the annotation system is already agnostic to
how the target got placed. CV is a better `SpatialTarget` setter, not a rewrite.

`AnalyzeRequest.image_base64` already exists on the wire and is accepted and
ignored, so the client can start sending frames before anything consumes them.

## Deliberate trade-offs

**Annotations are built in code, not from prefabs.** No asset GUIDs to go
stale, no YAML to hand-edit, and the Editor setup tool stays simple enough to
regenerate the whole scene.

**No assembly definitions.** Everything compiles into `Assembly-CSharp`, which
auto-references every package. Asmdefs would mean hand-maintaining references to
Oculus.VR, Newtonsoft, XR Management, MRUK, Input System and TextMeshPro — a
whole class of failure removed for a hackathon-appropriate cost.

**The scene is a build output.** `Tools > SpatialDebugger > 2. Build MR Scene`
regenerates it from scratch. Editing it by hand works but will be overwritten.

**MRUK is not in the scene by default.** It needs Space Setup to have been run,
its default prefab silently loads a *synthetic* room when there is no real scan,
and `LoadSceneFromDevice` can yank the user into the system Space Setup flow
mid-demo. Physics + projected fallback always works. Opt in when you have a
headset and a captured room.
