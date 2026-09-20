# Judging pitch and technical proof

## 15-second pitch

SpatialDebugger turns any room into a mixed-reality vocabulary lesson. On a
Quest 3, point and pinch to leave English, French, and Spanish labels fixed in
the space around you—no phone between you and the object you are learning.

## 30-second pitch

Language apps usually pull your attention onto a flat screen. SpatialDebugger
makes the room the interface: through Quest 3 passthrough, you point with your
hand, pinch, and place an English, French, and Spanish vocabulary card at that
location. The labels remain fixed as you move, and several can coexist. That
core experience is physically verified, with a deterministic offline mode that
cannot lose the demo. We have also implemented local Moondream recognition over
USB; its final end-to-end hardware validation is still in progress.

## 60-second pitch

Vocabulary is easier to remember when a word is tied to a place and an action.
SpatialDebugger turns a real room into that memory map. Wearing a Quest 3, I
point naturally at a location and pinch. A world-space card appears with an
English word and its French and Spanish translations. I can walk around it and
the card stays there; I can label another place and keep both.

Under the hood, Meta hand tracking gives us the selection ray, 6DoF tracking
keeps the annotations stable, and Meta's passthrough camera API gives us real
RGB frames. Our dependable demo uses a local deterministic vocabulary, so it
needs no backend, API key, or internet and we never misrepresent it as
recognition. We have also implemented a route that crops the camera frame at
the pinched point, sends it over USB to Moondream running locally on a Mac, and
updates the same annotation. The camera and transport are hardware-verified;
the final recognition loop is the remaining physical validation. The result is
a simple interaction—point, pinch, learn—with a reusable spatial annotation
engine underneath it.

## 60-second pitch after recognition is verified

> Use only after the full physical flow succeeds.

Vocabulary is easier to remember when a word is tied to a place and an action.
SpatialDebugger turns a real room into that memory map. Wearing a Quest 3, I
point at an object and pinch. An `ANALYZING…` card appears immediately, then
updates with the recognized English noun and its French and Spanish
translations. I can walk around it and the card stays fixed; I can recognize a
second object and keep both labels in the room.

Meta hand tracking tells us where the learner pointed. We project that world
point into a real Quest RGB frame, crop the relevant region, and send it over a
USB tunnel to Moondream running locally in Ollama on a Mac. The response updates
the exact card that started the request, even with multiple annotations. No
cloud inference or venue Wi-Fi is required after setup. If camera or inference
fails, a clearly deterministic vocabulary path keeps the spatial interaction
working. SpatialDebugger replaces the phone-camera lookup with a direct,
embodied loop: point, pinch, learn.

## Likely judge Q&A

### Why mixed reality for language learning?

It binds a word to the object, location, gesture, and visual memory at the same
time. Passthrough also keeps the learner in the real environment; they do not
have to alternate attention between an object and a phone screen. Our prototype
demonstrates that interaction model rather than claiming learning-outcome
metrics we have not measured.

### How does the spatial anchoring work?

At pinch time we copy the selected point into Unity world coordinates and
dispatch the annotation in world space. Quest 6DoF tracking then preserves that
relationship as the headset moves, which we verified by walking sideways and
observing parallax. These are session-local world positions, not persisted Meta
spatial anchors; labels disappear when the app restarts.

### Is recognition actually real?

**Current answer:** The camera frames are real and hardware-verified, and the
Moondream request/update route is implemented and tested in code. We have not
yet declared the complete recognition result physically verified, so today's
guaranteed demo uses a deterministic vocabulary and we label it honestly.

**After physical verification:** Yes. A pinch triggers a crop of the real Quest
RGB frame, Moondream returns a noun, and that same annotation updates. We still
keep a deterministic fallback for failures; the logs and UI make the two paths
distinguishable.

### Where does AI inference happen?

On the Mac, in Ollama, using Moondream. The Quest calls
`http://127.0.0.1:11434`; `adb reverse` carries that traffic over the USB cable
to the Mac. It does not use the FastAPI backend and it does not send the image
to a cloud vision service.

### Why Ollama?

It gives us a local, inspectable inference endpoint with no API key or venue
network dependency. Meta's SDK already exposes an `OllamaProvider`, so we could
use a supported task interface while keeping the prototype architecture small.
The trade-off is the current USB-connected Mac requirement.

### Does it work offline?

The deterministic language-learning mode is fully offline and on-device. The
recognition route needs a Mac over USB, but after Moondream is installed it does
not need internet or cloud inference. So it is network-independent, not yet a
fully standalone headset experience.

### Why use a fixed 1.5 m depth?

It is a targeting fallback, not an estimate of object distance. If no scene or
physics surface is available, projecting 1.5 m along the hand ray guarantees
that a pinch still creates a stable target at a comfortable scale. The next
step is to use scene/depth geometry for surface-accurate placement while
retaining the fallback.

### How is this different from pointing a phone camera at something?

A phone gives a result inside its own rectangle and the result leaves when the
phone moves. Here, the selection is hands-first and the output becomes part of
the room: users can walk around labels, keep several visible, and build a
spatial memory map while still seeing their environment stereoscopically.

### What Meta Quest APIs did you use?

Meta XR SDK 205 supplies the Quest rig, passthrough, hand tracking, and
`PassthroughCameraAccess`; OpenXR supplies the XR runtime integration. The
vision route uses Meta's AI Building Blocks `OllamaProvider` through its chat
task interface. We use our own small pointer, targeting, and world-space
annotation layer around those APIs.

### Why a chat task instead of object-detection bounding boxes?

The hand ray already establishes the location the user means. We only need the
object noun, so computing boxes would add work that we immediately discard.
Meta's `OllamaProvider` also implements the chat task interface, not the object
detection task interface.

### What was technically difficult?

The hardest parts crossed system boundaries: getting the actual Quest camera
texture rather than compositor passthrough, reading a GPU texture only when
needed, mapping the selected world point back into the image, carrying local
traffic from Android to macOS, updating the correct asynchronous label, and
making the APK include the right OpenXR loader, permissions, scene, and fonts.
Many of those failures still allow Unity to report a successful build, so we
verified the built artifact and the physical headset behavior separately.

### What happens when the model is wrong or unavailable?

An unknown but genuine noun remains visible with translations marked missing;
we do not silently substitute a convenient word. A request failure is logged
and falls back to the next deterministic vocabulary entry. In the demo we say
which mode is running so scripted output is never presented as recognition.

### What would you build next?

First, finish physical recognition validation and measure its real latency and
accuracy on representative room objects. Then add surface depth, pronunciation,
spaced repetition, a larger translation source, persisted spatial anchors, and
an inference package that removes the Mac tether.

### Why is the repository still called SpatialDebugger?

The underlying system is a general spatial annotation engine: a selected real
location can receive labels, markers, highlights, arrows, or warnings. It began
as a way to annotate physical debugging targets. The language-learning demo is
the most immediate expression of the same idea—attach useful information to
the physical thing it describes.

## Technical achievements, with evidence

### Verified on physical Quest 3

- **Passthrough, stereo OpenXR, and 6DoF:** the room remains visible and
  annotations exhibit the correct parallax while the wearer moves.
- **Tracked-hand pinch interaction:** `MetaHandPointerSource` feeds the same
  target controller used by the demo.
- **World-fixed, concurrent annotations:** each pinch snapshots a world point;
  prior labels remain independent of later selections.
- **Readable multilingual text:** world-space TextMeshPro labels render French
  and Spanish accents covered by the packaged font atlas.
- **Quest RGB camera access:** `CameraFeed` receives real, non-black 1280×960
  frames from `Meta.XR.PassthroughCameraAccess`.
- **Quest-to-Mac USB networking:** port 11434 is reachable through `adb reverse`.
- **Deterministic language fallback:** CHAIR, LAPTOP, BOTTLE, and BACKPACK with
  local French/Spanish lookups.

### Implemented and test-covered; final device validation pending

- Pinch-centred viewport projection and JPEG crop of the camera texture.
- Local Moondream request through Meta's `OllamaProvider`.
- Response normalization that prefers a known noun without hiding unknown
  genuine results.
- Per-request annotation handles, so overlapping asynchronous requests can
  update the correct labels out of order.
- Failure handling for camera readiness, capture, transport, timeout, malformed
  response, and callback exceptions.

### Build and automated verification

- Latest recorded checkpoint: **104 Unity EditMode tests** and **54 FastAPI
  backend tests** passing.
- Unity tests exercise parsing, geometry, real renderer GameObjects,
  update-in-place behavior, vocabulary/font support, and recognition text/error
  paths.
- Backend tests exercise validation, deterministic scenarios, typed spatial
  actions, provider selection, and graceful degradation.
- Android build inspection confirms the MR scene, one OpenXR loader, ARM64
  libraries, hand/scene/camera permissions, and application id
  `com.htn2026.spatialdebugger`.

## Claims to avoid

- “Recognition works on Quest” until the owner confirms the physical result.
- “On-device AI” — inference currently runs on a Mac.
- “Persistent anchors” — labels are stable only during the current session.
- “Depth-aware placement” — 1.5 m is a fallback distance, not measured depth.
- “Fully offline recognition” — it is cloud-free after setup, but Mac-tethered.
- Accuracy, latency, frame rate, or learning-impact numbers that were not
  measured.
