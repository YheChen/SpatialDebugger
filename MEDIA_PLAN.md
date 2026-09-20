# SpatialDebugger — final Devpost media plan

Project name: **SpatialDebugger**

Tagline: **Point. Pinch. Learn.**

Use the Devpost video field for the 45–60 second demo. Upload exactly these four gallery assets, in this order:

## 1. Real Quest hero still

Show a bright room-wide Quest capture with a tracked hand and two genuine annotations visible at once. Use **chair + laptop** if possible; **table** is the backup. Keep both physical objects and their cards readable so judges understand the project before reading any copy.

Do not composite invented labels into the passthrough view. A small editorial title strip with `SPATIALDEBUGGER` and `Point. Pinch. Learn.` is fine.

Format: 1920×1080 PNG or JPEG.

## 2. Real recognition proof

Use a two-panel still from one uninterrupted attempt:

- Left: the user points and pinches a clearly visible laptop or chair; the card says `ANALYZING…`.
- Right: the same attempt updates to the genuine label and French/Spanish vocabulary.

Caption: `Quest RGB crop → local Moondream/Ollama → same spatial card`.

Do not join the pinch from one attempt to the result from another.

Format: 1920×1080 PNG.

## 3. Architecture diagram

Upload [architecture.png](assets/devpost/architecture.png). It shows the real boundary between Quest capture/targeting and local laptop inference, including USB `adb reverse`, Ollama, Moondream, vocabulary lookup, and update-in-place.

This is an explanatory diagram, not a screenshot.

## 4. Measured-depth diagram

Upload [depth-placement.png](assets/devpost/depth-placement.png). It uses real observed Quest 3 distances and explicitly states that 1.5 m is the fallback when environment depth does not return a hit.

This diagram proves variable measured distance only. It does not claim reliable wall, floor, or ceiling orientation.

## Capture priorities

Use these four demo examples consistently:

1. **Laptop** — strongest recognition close-up.
2. **Chair** — strongest spatial-learning visual.
3. **Table** — safest second distance and multi-annotation subject.
4. **Wall** — available in the vocabulary, but do not use it as placement proof until wall attachment is physically reliable.

Record these Quest clips before editing:

- One complete laptop or chair attempt from pinch through `ANALYZING…` to the final result.
- One successful second object, preferably table, at a clearly different measured distance.
- One 5–8 second sideways movement with two completed annotations in frame.
- One clean room-wide beauty shot with the tracked hand and no debug console.
- Optional: one honest `NOT RECOGNIZED` attempt for a limitations beat or judge Q&A.

## Do not upload as evidence

[hero-concept.png](assets/devpost/hero-concept.png) is generated explanatory art, not Quest footage. Keep it only as a backup social thumbnail, with its disclosure intact. Real Quest evidence should lead the submission.

Do not publish any frame that implies:

- hosted or commercial vision inference;
- reliable arbitrary-object recognition;
- persistent anchors across restarts;
- verified wall/floor/ceiling attachment;
- that the 1.5 m fallback is a measured depth hit.
