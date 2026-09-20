# SpatialDebugger — final Devpost submission

## Project name

**SpatialDebugger**

## Tagline

**Point. Pinch. Learn — local AI labels that stay with the real world.**

## Elevator pitch

SpatialDebugger turns a room into a mixed-reality vocabulary map. On Meta Quest 3, point at an object and pinch: environment depth selects the real-world target, a local Moondream model classifies the matching camera crop, and the result appears as an English, French, and Spanish label at that place.

## Inspiration

Camera-based recognition usually ends on a flat screen. We wanted to know what changes when the answer becomes part of the room—when “chair,” “chaise,” and “silla” can stay beside the chair as you move.

That made language learning a natural spatial-computing problem. A word can be tied to an object, a place, and a gesture instead of becoming another disposable phone lookup.

## What it does

SpatialDebugger runs in Quest 3 passthrough. The user points and pinches, and an environment-depth raycast resolves the selected position. An **ANALYZING…** annotation appears immediately at that world-space target.

The app projects the target into the Quest passthrough-camera image, crops around it, and sends the JPEG over USB to Moondream running through local Ollama on the developer laptop. Moondream returns a description, and the app constrains it to a small demo allowlist. The four judge-facing camera targets are **laptop, table, chair,** and **wall**; confident depth geometry can separately resolve **floor** and **ceiling**. A local vocabulary lookup adds French and Spanish, and the original annotation updates in place.

The system does not pretend to recognize every object. Unsupported, uncertain, or malformed responses become **NOT RECOGNIZED** instead of being mapped to a convenient demo word. Genuine model successes display an **AI RECOGNIZED** footer with the measured selection distance.

## How we built it

The headset client uses Unity 6, C#, Meta XR SDK 205, OpenXR, hand tracking, environment raycasting, Passthrough Camera Access, URP, and world-space text.

The real pipeline is:

**Quest RGB camera → hand ray and pinch → environment-depth target → projected camera crop → Moondream through local Ollama → constrained object class → local French/Spanish vocabulary → update the same spatial annotation**

Ollama runs on the laptop. During development, Android Debug Bridge reverse port forwarding carries requests over USB from the Quest to laptop localhost. No hosted vision service or commercial AI API is used.

Each pinch snapshots its own world-space point and annotation handle. That keeps earlier labels independent and ensures an asynchronous model response updates the card that initiated it.

Depth placement has been physically verified on Quest 3 at real measured distances. The current build can also identify floor and ceiling from a confident depth normal plus target height; those results are marked **DEPTH SURFACE**, not AI recognition. Normal-aware annotation orientation is implemented and test-covered, but we do not make a broader claim that every wall, floor, or ceiling presentation is reliable in every room.

## Challenges we ran into

**Passthrough is not a camera texture.** The image shown by the Quest compositor cannot be captured with a normal Unity screen grab. We had to use Meta’s camera-access API, request the correct permission, and wait for a real non-black frame.

**One pinch crosses several coordinate systems.** A hand ray selects a world-space point, depth resolves its real distance, and the crop needs the corresponding camera pixel. We verified the actual JPEG pulled from the headset to make sure it was upright and centered on the selected region.

**Small-model prompts need guardrails.** In our tested Ollama configuration, instruction-style one-word prompts could return an empty answer or a junk token. A plain visual question was more reliable, so the app maps the resulting description through a narrow allowlist instead. Unsupported objects stay unrecognized.

**Spatial ownership matters.** The user can move or pinch again while inference runs, so every request retains the target and renderer handle it started with.

## Accomplishments that we’re proud of

- A hardware-verified Quest 3 loop from pinch and real RGB crop through local Moondream inference to an in-place world-space label update.
- Correct Quest recognition observed for laptop, chair, table, and wall; floor and ceiling were also verified as separate depth-derived labels rather than model claims.
- Real environment-depth hits observed at approximately 0.68 m, 0.70 m, 0.98 m, 1.06 m, 1.87 m, 2.99 m, and 3.54 m.
- Multiple labels that remain fixed at their selected world positions during the current session.
- Cloud-free recognition after setup: the model runs locally on the laptop and communicates with the Quest over USB.
- Honest failure behavior: an attempted recognition failure displays **NOT RECOGNIZED** rather than a scripted substitute.

## What we learned

Spatial recognition becomes simpler when the interaction already answers “where?”. The hand ray and depth target identify the region of interest, so the model only needs to answer “what is at this selected point?”

We also learned that XR reliability lives at system boundaries: compositor versus camera, world space versus camera space, GPU textures versus JPEG input, Quest localhost versus laptop localhost, and model output versus a safe UI label.

Most importantly, a narrow result that is visibly honest is better than broad recognition that sometimes invents confidence.

## What’s next

We want to validate the surface-normal presentation across more real walls, floors, and ceilings; improve recognition beyond the four demo categories; expand the vocabulary; add pronunciation and spaced repetition; and persist labels across restarts with spatial anchors. Longer term, packaged or on-headset inference could remove the laptop tether.

## Built With

- Meta Quest 3
- Unity 6000.6.2f1 and C#
- Meta XR SDK 205
- Meta MR Utility Kit and Environment Raycasting
- Meta Passthrough Camera Access
- Meta AI Building Blocks / OllamaProvider
- OpenXR 1.18
- Unity Input System
- Universal Render Pipeline
- TextMeshPro
- Ollama
- Moondream
- Android Debug Bridge reverse port forwarding

## Devpost AI question

### Did you implement a generative AI model or API in your hack this weekend? If so, how and why did you use it?

Yes. We integrated Moondream, a pre-trained generative vision-language model, through Ollama running locally on the developer laptop. When the user points and pinches, the Quest uses environment depth to select a physical target, captures a passthrough-camera crop centered on that point, and sends the JPEG over a USB ADB reverse connection to Ollama. The app constrains Moondream's description to a small demo allowlist centered on laptop, table, chair, and wall; a local lookup adds French and Spanish, and the same world-space annotation updates with the result. We used a vision-language model because the spatial gesture already identifies where to look, while local inference avoids a hosted vision API. Unsupported or uncertain answers become NOT RECOGNIZED.

Truthful form selections:

| Question | Answer |
|---|---|
| Implemented or used a generative AI model | **Yes — Moondream** |
| Used a pre-trained/open model | **Yes** |
| Used local/self-hosted AI | **Yes — Ollama on the laptop** |
| Used a hosted/external AI API | **No** |
| Used the OpenAI API in the submitted feature | **No** |
| Trained or fine-tuned a model | **No** |
| On-device AI inference | **No** |
