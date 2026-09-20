# Devpost submission draft

> **Publishing guardrail:** This draft is safe while real object recognition is
> still awaiting physical end-to-end verification. Text under “Recognition
> switch kit” must only be used after that verification succeeds.

## Project name

**SpatialDebugger**

## Tagline

**Point. Pinch. Learn — turn the room around you into a mixed-reality vocabulary lesson.**

## One-sentence pitch

SpatialDebugger lets Quest 3 learners point and pinch to place world-fixed English, French, and Spanish vocabulary labels throughout the room around them.

## Short descriptions

### About 50 words

SpatialDebugger turns any room into a mixed-reality vocabulary lesson: point
into real space, pinch, and a world-fixed label shows an English word alongside
French and Spanish translations. Built for Meta Quest 3, it combines
passthrough, hand tracking, spatial annotations, and a deterministic offline
fallback, while local vision recognition is validated.

### About 100 words

SpatialDebugger turns language learning into something you can place in the
world. Through Meta Quest 3 passthrough, a learner points with a tracked hand
and pinches to leave an English, French, and Spanish label at that location.
Labels stay fixed as the learner moves and multiple annotations build a visual
map of the room. The reliable demo uses a deterministic local vocabulary, so
it needs no internet or API key. A vision path is also implemented: capture a
real Quest RGB frame, crop around the pinched point, send it over USB to
Ollama/Moondream on a Mac, and update the same label. That final recognition
loop is pending physical verification.

### About 200 words

SpatialDebugger is a Meta Quest 3 mixed-reality prototype that turns the room
around you into a spatial language lesson. Instead of holding up a phone,
opening an app, and losing the result when you lower the screen, you point with
your hand and pinch. A compact world-space card appears at that location with
an English word and its French and Spanish translations. Walk sideways and the
card stays put; label another location and both cards remain in the room.

The interaction is already physically verified on Quest 3: passthrough,
stereo rendering, 6DoF tracking, hand tracking, pinch selection, readable
accented text, and multiple world-fixed annotations. The dependable demo mode
uses a deterministic on-device vocabulary, which means it works without a
backend, API key, or internet connection and never turns a failed model call
into a failed presentation.

We also implemented a local vision route. Meta's
`PassthroughCameraAccess` supplies a real 1280×960 RGB frame, the pinched world
point selects the image crop, and a USB `adb reverse` tunnel connects the Quest
to Moondream running in Ollama on a Mac. The response is normalized, translated
with a local lookup, and used to rewrite the same annotation. Camera capture
and USB networking are hardware-verified; the complete recognition loop is
still undergoing physical validation, so it is not yet presented as a working
feature.

## Inspiration

Vocabulary sticks when it is tied to a place, an action, and a memory. Most
translation tools flatten that moment into a phone screen: aim a camera, read a
result, then put the phone away. We wanted to know what changes when the room
itself becomes the interface—when “chair,” “chaise,” and “silla” can live beside
the chair and become part of how a learner remembers the space.

The broader idea behind SpatialDebugger is that mixed reality can attach useful
information directly to the physical thing it describes. Language learning is
our clearest, most immediate demonstration of that idea.

## What it does

SpatialDebugger uses Quest 3 passthrough so learners stay present in their real
environment. They point with a tracked hand, pinch to select a location, and
receive a trilingual world-space annotation. The label remains fixed while the
user moves, and several labels can coexist to turn a room into a vocabulary
map.

Our reliable demo mode deliberately cycles through a prepared local vocabulary
of chair, laptop, bottle, and backpack. It is deterministic—not recognition—so
the core spatial learning interaction works without a server, model, API key,
or internet connection.

The repository also contains an implemented vision route that captures the
Quest RGB camera image, crops around the selected point, calls a Mac-local
Moondream model through Ollama, and updates the original annotation. Its camera
and transport pieces are verified on hardware; the complete recognition result
is still in physical testing.

## How we built it

We built the headset client in Unity 6 with Meta XR SDK 205, OpenXR, hand
tracking, URP, and TextMeshPro. A hand pointer produces a ray and an index pinch
commits the selection. Scene or physics hits are used when available; a 1.5 m
projected target guarantees that the interaction still succeeds in empty
space. Each selected point is copied into session world coordinates so later
pinches do not move earlier annotations.

Annotations use a small structured `SpatialAction` protocol and renderer layer.
The current language demo dispatches a world-space label, keeps a handle to its
renderer, and can update that exact label in place. French and Spanish strings
come from a local lookup whose accented characters are covered by the packaged
font atlas.

For the vision path, Meta `PassthroughCameraAccess` exposes the Quest RGB frame.
We project the pinched world point back into the camera image, perform a
one-time GPU readback and JPEG crop, then call Meta's `OllamaProvider` as an
`IChatTask`. `adb reverse tcp:11434 tcp:11434` carries that request over USB to
Moondream on the Mac, avoiding venue Wi-Fi and cloud inference. Response
normalization handles the prose a small model may return. Every failure path
uses the deterministic vocabulary.

Separately, the repo includes a FastAPI/Pydantic backend for the project's
general spatial-debugging mode. It emits labels, warnings, markers, arrows, and
highlights and degrades to offline scenarios when a provider fails. The current
language-learning vision path talks directly to Ollama and does not route
through this backend.

## Challenges we ran into

- **Passthrough is not the camera feed.** Quest passthrough is compositor
  output, so Unity cannot capture it with `WebCamTexture` or a screen grab. We
  used `PassthroughCameraAccess`, requested the dedicated headset-camera
  permission, and waited for a real texture instead of trusting component
  startup.
- **A GPU frame is not yet model input.** The camera texture requires a GPU
  readback. We do it once per pinch, crop around the selected point, and encode
  that crop rather than continuously moving full frames.
- **Spatial labels must survive the next interaction.** A single moving target
  made earlier labels follow later pinches. Dispatching in world space and
  retaining the individual renderer handle let multiple labels coexist and let
  asynchronous results update the right card even when requests finish out of
  order.
- **Quest build details fail silently.** We caught and fixed OpenXR loader,
  duplicate native library, Android permission, scene selection, and font
  resource issues by inspecting built APKs and adding tests instead of assuming
  a successful Unity build meant a working headset experience.
- **A live model cannot be the only demo path.** Camera permission, USB, and a
  local model introduce real failure modes. The deterministic vocabulary makes
  failure graceful and visible without pretending that a fallback word was
  recognized.

## Accomplishments that we're proud of

- A physically verified Quest 3 interaction from passthrough hand pointing and
  pinch input to readable, world-fixed multilingual annotations.
- Multiple annotations that remain independently fixed while the user moves.
- Real, non-black 1280×960 Quest RGB camera frames through Meta's supported API.
- A vision pipeline that keeps inference local to a USB-connected Mac and
  updates the exact annotation that initiated each request.
- Honest degradation: an unknown model result stays unknown, while a failure is
  explicitly routed to a deterministic fallback.
- A latest recorded test checkpoint of 104 Unity EditMode tests and 54 backend
  tests, plus a verified Android build pipeline.

## What we learned

Mixed reality has several coordinate systems that look interchangeable until
the user moves. Capturing the selected world point at pinch time—and expressing
annotations relative to an intentional frame—was more important than any visual
effect.

We also learned that rendering passthrough and accessing the physical RGB
camera are separate privileges and separate pipelines. The most useful
architecture split location from identity: the hand ray already answers
“where,” so the vision model only needs to answer “what.” That made a compact
crop plus a one-word response more appropriate than a full object-detection
stack.

Finally, a trustworthy fallback is part of the product. It keeps the spatial
interaction demonstrable without disguising scripted output as AI.

## What's next

First, we will finish physical validation of the complete
camera-to-Moondream-to-label path and tune its latency and prompt against real
rooms. Then we want to replace fixed-distance fallback placement with scene or
depth geometry, add pronunciation and spaced repetition, grow the translation
dictionary, and persist vocabulary cards across sessions with spatial anchors.
Longer term, packaged or on-headset inference could remove the Mac tether
entirely.

## Built with

- Meta Quest 3
- Unity 6000.6.2f1 and C#
- Meta XR SDK 205 and MR Utility Kit
- OpenXR 1.18 and XR Hands
- Meta `PassthroughCameraAccess`
- TextMeshPro and URP
- Ollama and Moondream *(vision path implemented; final device verification pending)*
- Android Debug Bridge (`adb reverse`)
- FastAPI, Pydantic, pytest, and optional OpenAI/OMNI adapters *(general annotation backend)*

## Recognition switch kit

Use this material **only after the full pinch → real camera frame → Moondream →
updated label flow has been observed on the physical Quest**.

### Replacement one-sentence pitch

SpatialDebugger lets Quest 3 learners point at a real object, pinch to recognize it with a local vision model, and leave its English, French, and Spanish name fixed beside it.

### Replacement “What it does”

SpatialDebugger uses Quest 3 passthrough so learners stay present in their real
environment. Point at an object and pinch: an `ANALYZING…` card appears
immediately, a real Quest RGB crop is sent over USB to Moondream running locally
on a Mac, and that same world-fixed card updates with the recognized English
noun plus French and Spanish translations. Move around to see that the card
stays attached to the chosen place, then label another object and keep both in
the room. If camera or inference fails, SpatialDebugger switches to a clearly
identified deterministic vocabulary path instead of breaking the interaction.

### Replacement accomplishment

- End-to-end real-object recognition on Quest 3: pinch-centred RGB capture,
  local Moondream inference over USB, translation lookup, and in-place update of
  a world-fixed annotation.

### Facts to record before switching

- Objects tried and which were recognized correctly
- Typical observed response time (do not estimate)
- Whether multiple overlapping requests were tested on hardware
- Exact APK/commit used for the successful run

## Final Devpost fill-ins

- Team member names and badge IDs
- Repository URL
- Demo video URL and thumbnail
- Final screenshots/GIF
- Sponsor prizes selected before the event cutoff
- Recognition copy switched only if the physical test passes
