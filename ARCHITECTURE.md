# SpatialDebugger — architecture

> **Current extension:** The general spatial-action architecture below remains
> valid. The language-learning demo now also has a direct Quest-camera-to-local-
> Ollama route, documented in “Language-learning vision extension.” Camera
> capture is physically verified; final recognition remains unverified on
> hardware.

A Meta Quest 3 mixed-reality tool for debugging physical electronics. You point
at something on a breadboard, ask, and the answer appears in the air next to the
actual wire.

```
physical electronics
        ↓
hand/controller target selection  ← IMPLEMENTED
        ↓
AI debugging / reasoning          ← optional provider seams; deterministic scenarios today
        ↓
structured spatial actions        ← IMPLEMENTED, the core of the system
        ↓
Quest mixed-reality annotations   ← IMPLEMENTED AND PHYSICALLY VERIFIED
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

## Language-learning vision extension

**Implementation status:** the route is implemented and test-covered. Quest RGB
camera capture and USB port reversing are physically verified. The complete
Moondream recognition result has not yet been declared verified on hardware.

This route deliberately does not pass through the FastAPI provider seams above:

```
hand ray + pinch
        │
        ├──► selected world point ──► immediate ANALYZING label
        │
        ▼
Meta.XR.PassthroughCameraAccess
        │   real Quest RGB Texture (GPU)
        ▼
world point projected into camera viewport
        │
        ▼
one-time GPU readback + JPEG crop
        │
        ▼
Meta OllamaProvider / IChatTask
        │   http://127.0.0.1:11434 on Quest
        │   adb reverse tcp:11434 tcp:11434 over USB
        ▼
Moondream in Ollama on the Mac
        │
        ▼
normalized English noun ──► local FR/ES lookup
        │
        ▼
rewrite the same world-space label
```

The hand ray already establishes **where** the user means, so the model only
answers **what**. That is why `VisionRecognizer` uses Meta's `IChatTask` rather
than an object-detection task with bounding boxes. The crop follows the pinched
point, reducing irrelevant image content without asking the model to relocate
the selection.

`PinchAnnotationPlacer` retains the `LabelRenderer` returned by
`DispatchTracked`, so each asynchronous request updates its own label even when
multiple requests finish out of order. Any capture, transport, timeout, or
response failure updates that label from the deterministic vocabulary instead.

The headset-camera permission and passthrough-camera project setting are now
enabled. The supported camera route is `PassthroughCameraAccess`; passthrough
itself is compositor output and cannot be captured with `WebCamTexture` or a
normal Unity framebuffer grab.

Surface-accurate depth remains future work. The current interaction first uses
available scene/physics hits and otherwise places a target 1.5 m along the hand
ray. That fixed distance is a reliable fallback, not a depth measurement.

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
