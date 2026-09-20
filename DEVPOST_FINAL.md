# SpatialDebugger — final Devpost submission

## Project name

**SpatialDebugger**

## Tagline

**Point. Pinch. Learn.**

## Elevator pitch

SpatialDebugger turns a real room into a mixed-reality vocabulary map. On Meta Quest 3, point at an object and pinch: a raycast selects the spatial target, the Quest RGB camera supplies a focused crop, and Moondream running locally through Ollama on a USB-connected laptop returns an object noun. The same world-fixed card then updates with English, French, and Spanish vocabulary.

## Inspiration

A photo can tell you what an object is, but the answer stays trapped on a screen. We wanted the word for a chair to sit beside the chair, remain there as you move, and become part of the room.

Language learning made that idea easy to understand: physical objects already have location, context, and meaning. Spatial computing lets the environment itself become the interface.

## What it does

SpatialDebugger runs in Quest 3 passthrough. A tracked hand supplies a pointer ray, and an index pinch commits the selection. Meta environment depth places the target at a measured physical hit when one is available; if it cannot return a hit, the current build keeps a 1.5 m fallback instead of dropping the interaction.

An `ANALYZING…` card appears immediately. The selected world point is projected into the Quest passthrough-camera image, the app crops and JPEG-encodes that region, and the Quest sends it over USB using `adb reverse` to Ollama on the developer laptop. Moondream returns a description, the app normalizes it to an object noun, and a local vocabulary lookup adds French and Spanish when that noun is known. The original card updates in place.

Multiple annotations can coexist and remain fixed in world space during the current app session. They are not persistent spatial anchors and are not restored after restarting the app.

Recognition is real but not perfectly reliable. It depends strongly on crop composition, object size, and clutter. The live demo focuses on four visually distinct examples—**laptop, chair, table, and wall**—and shows `NOT RECOGNIZED` when a request fails or produces no usable noun. Laptop, chair, and table are the safest demonstrated live subjects. Wall is available in the vocabulary, but wall, floor, and ceiling surface attachment is still being diagnosed, so we do not present surface-aware planar placement as a completed feature.

## How we built it

We built the headset experience in Unity 6 and C# with Meta XR SDK 205, OpenXR, hand tracking, passthrough camera access, environment raycasting, URP, and world-space text.

The implemented path is:

**Quest passthrough RGB camera + tracked hand pinch → raycast/depth target → projected camera crop and JPEG → USB `adb reverse` → local Ollama/Moondream recognition → returned noun → local French/Spanish vocabulary → update the same spatial annotation.**

Location and identity stay separate. The pinch creates a session world-space target and renderer handle immediately. Recognition runs asynchronously, but each request retains the handle for its own card, so later pinches cannot steal an earlier result.

Inference is not cloud-hosted and does not use a commercial vision API. Meta's `OllamaProvider` talks to `127.0.0.1:11434` on the Quest; `adb reverse` forwards that connection to Ollama and Moondream on the USB-connected laptop.

The implementation can use a confident depth normal to orient marker and leader geometry. Measured depth placement is physically verified; reliable wall, floor, and ceiling presentation is not, so the submission only claims measured target distance and session world-fixed placement.

## Challenges we ran into

**Passthrough is not the RGB camera feed.** The visible passthrough image is compositor output. We needed Meta's dedicated camera API and permission, then had to verify that real, non-black pixels were arriving.

**One pinch crosses several coordinate systems.** A tracked hand ray and depth hit live in world space, while the crop needs a point in the camera image. We validated projection by pulling the exact JPEG from the Quest and checking its orientation and composition.

**Crop quality controls recognition quality.** A wide crop contains competing subjects; a tight crop can cut off the object. This is why the demo uses pre-tested, visually distinct objects and does not claim arbitrary-object reliability.

**Android localhost is the headset, not the laptop.** `adb reverse tcp:11434 tcp:11434` provides the USB bridge to the local Ollama service without relying on venue Wi-Fi.

**Asynchronous answers need spatial ownership.** Each request captures its JPEG and renderer handle at pinch time, allowing several world-fixed cards to coexist safely.

## Accomplishments that we're proud of

- Physically verified the end-to-end Quest 3 loop: hand pinch, depth target, real RGB crop, USB transport, local Moondream inference, noun normalization, translation lookup, and update-in-place of the original annotation.
- Physically observed successful recognition of laptop, chair, and table, with typical warm inference around 1.5–2.4 seconds. We do not claim perfect accuracy.
- Physically observed environment-depth hits at approximately 0.68 m, 0.70 m, 0.98 m, 1.06 m, 1.87 m, 2.99 m, and 3.54 m. The 1.5 m position remains a fallback, not the normal result when depth succeeds.
- Verified that several annotations can coexist and remain world-fixed while the wearer moves sideways.
- Kept runtime inference local to the developer laptop through Ollama; no hosted vision API is used.
- Made failure explicit: unsuccessful attempts display `NOT RECOGNIZED`, while the separate offline vocabulary mode identifies itself as non-AI.

## What we learned

Spatial computing makes “where?” as important as “what?”. The hand ray and environment depth identify the intended location, so the vision model can focus on a selected crop instead of detecting everything in the room.

Most failures happened at system boundaries: compositor versus camera, world coordinates versus image coordinates, GPU texture versus JPEG, Android localhost versus laptop localhost, and model prose versus a normalized noun. Physical logs and real headset captures were more valuable than an Editor-only demo.

We also learned that honest failure states matter. An uncertain result is better than silently substituting a scripted vocabulary word.

## What's next

Next we would improve crop selection and recognition accuracy, validate and tune wall/floor/ceiling presentation, expand the supported vocabulary, add pronunciation and spaced repetition, and persist annotations across sessions with spatial anchors. A packaged or on-headset model could eventually remove the USB-connected laptop.

## Built with

- Meta Quest 3
- Unity 6000.6.2f1 and C#
- Meta XR SDK 205 and MR Utility Kit environment raycasting
- Meta Passthrough Camera Access and AI Building Blocks `OllamaProvider`
- OpenXR
- Universal Render Pipeline and TextMeshPro
- Ollama and Moondream
- Android Debug Bridge (`adb reverse`)

## AI disclosure

We integrated Moondream, a pre-trained vision-language model, through Ollama running locally on the developer laptop. The Quest sends a JPEG crop over a USB `adb reverse` connection; no hosted or commercial vision API is used. We did not train or fine-tune a model, and inference does not run on the Quest itself. Recognition is limited and crop-dependent. Failed attempts show `NOT RECOGNIZED`; nouns outside the local vocabulary are not given invented translations.

## Repository

https://github.com/YheChen/SpatialDebugger
