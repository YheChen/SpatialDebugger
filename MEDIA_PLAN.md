# SpatialDebugger — final Devpost media order

Use these five assets in this order. If only three are allowed, use items 1, 2, and 4.

## 1. Demo video — primary proof

**Asset:** A 45–60 second edited video following VIDEO_FINAL.md.

**Purpose:** Prove the real interaction, recognition, depth placement, and world registration.

**Must be real Quest footage:** At least one uninterrupted pinch from **ANALYZING…** to a correct result, followed by sideways movement. Use chair first and laptop or table second.

**Reality rule:** Use only complete, successful takes from the current build. Wall is verified, but laptop, chair, and table remain the clearest judge-facing examples.

## 2. Real Quest hero still

**Purpose:** Communicate the whole product at a glance.

**Composition:** Real Quest POV in a classroom or lab, one tracked hand visible, with two genuine annotations at different positions. Prefer chair plus laptop.

**Exact in-headset text:** Use only what the running build produced, ideally **CHAIR / FR chaise / ES silla** and **LAPTOP / FR ordinateur portable / ES portátil**.

**Editorial title:** **SPATIALDEBUGGER — Point. Pinch. Learn.**

**Aspect ratio:** 16:9.

**Reality rule:** Do not composite labels or distances into the passthrough view.

## 3. Recognition close-up

**Purpose:** Show that the model result updates the annotation rather than appearing as a cutaway.

**Composition:** Two frames from one uninterrupted chair or laptop attempt: **ANALYZING…** followed by the correct trilingual card. Keep the physical object and leader line visible.

**Editorial caption:** **Real Quest crop → local Moondream → same spatial annotation**

**Aspect ratio:** 16:9 or a 3–5 second loop.

**Reality rule:** Do not join a failed pinch to a result from another attempt.

## 4. Architecture diagram

**Asset:** [architecture.png](assets/devpost/architecture.png)

**Purpose:** Explain the actual implementation and make the local-compute boundary obvious.

**Visible flow:** **Quest passthrough camera → pinch + environment depth → world-space target → project and crop → USB ADB reverse → Moondream/Ollama on laptop → constrained vision result → local FR/ES lookup → update the same annotation**

**Reality rule:** Caption it **EXPLANATORY DIAGRAM**, not a Quest screenshot.

## 5. Depth-placement diagram

**Asset:** [depth-placement.png](assets/devpost/depth-placement.png)

**Purpose:** Show that placement uses real measured depth rather than one fixed visual plane.

**Visible text:** **0.70 m**, **1.87 m**, **3.54 m**, the recorded observation list, and the **1.5 m fallback** used only when depth does not return a hit.

**Reality rule:** It is an explanatory graphic using observed Quest distances. It does not claim that the illustrated distances belong to particular objects or that all wall/floor/ceiling orientation is reliable.

## Capture checklist

- One uninterrupted chair recognition.
- One uninterrupted laptop or table recognition.
- One 5–8 second sideways movement keeping an object and its label in frame.
- One wide frame with two genuine labels.
- One clean room plate for title cards.
- Optional wall take; use it only if the complete current-build attempt is visually clear.

## Asset not recommended for the final gallery

[hero-concept.png](assets/devpost/hero-concept.png) is clearly labeled concept art and may be used for social promotion or as a temporary thumbnail. Prefer real Quest evidence in the Devpost gallery.
