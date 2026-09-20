# SpatialDebugger — final 45–60 second Devpost video

Project name: **SpatialDebugger**

Tagline: **Point. Pinch. Learn.**

## Recommended 56-second cut

| Time | Picture | Voiceover | On-screen text |
|---|---|---|---|
| 0:00–0:04 | Real Quest POV: chair or laptop, tracked hand entering frame. | “What if learning a word was as simple as pointing at the thing?” | `POINT. PINCH. LEARN.` |
| 0:04–0:15 | One uninterrupted pinch. Show the reticle, `ANALYZING…`, then the genuine result. | “On Quest 3, I point and pinch. A raycast selects the place, and the headset crops the real RGB camera image around it.” | `REAL QUEST RGB` |
| 0:15–0:22 | Hold on the English/French/Spanish card, then step sideways. | “Moondream recognizes that crop through Ollama running locally on this USB-connected laptop, and the same card updates in place.” | `LOCAL MOONDREAM • NO HOSTED API` |
| 0:22–0:33 | Pinch a second pre-tested object—preferably table—at a clearly different distance. | “When environment depth returns a hit, the annotation uses that measured distance instead of the 1.5 metre fallback.” | `MEASURED DEPTH WHEN AVAILABLE` |
| 0:33–0:40 | Wide view with both completed annotations; continue gentle sideways motion. | “Multiple annotations can coexist and remain fixed in the room for the current session.” | `MULTIPLE WORLD-FIXED CARDS` |
| 0:40–0:48 | Show `architecture.png`, highlighting Quest, USB bridge, and laptop zones. | “The pipeline is Quest camera and pinch, depth target, JPEG crop, local Moondream, vocabulary lookup, then an update to the original card.” | `QUEST → USB → LOCAL OLLAMA → SAME CARD` |
| 0:48–0:54 | Return to the strongest real Quest shot. | “Recognition depends on the crop, so the demo focuses on laptop, chair, table, and wall—and shows failure honestly.” | `NOT RECOGNIZED WHEN UNSURE` |
| 0:54–0:56 | End card. | “SpatialDebugger. Point. Pinch. Learn.” | `SPATIALDEBUGGER` / repository URL |

## Continuous voiceover

What if learning a word was as simple as pointing at the thing? On Quest 3, I point and pinch. A raycast selects the place, and the headset crops the real RGB camera image around it. Moondream recognizes that crop through Ollama running locally on this USB-connected laptop, and the same card updates in place. When environment depth returns a hit, the annotation uses that measured distance instead of the 1.5 metre fallback. Multiple annotations can coexist and remain fixed in the room for the current session. The pipeline is Quest camera and pinch, depth target, JPEG crop, local Moondream, vocabulary lookup, then an update to the original card. Recognition depends on the crop, so the demo focuses on laptop, chair, table, and wall—and shows failure honestly. SpatialDebugger. Point. Pinch. Learn.

## Required Quest footage

1. **Recognition master, 12–15 seconds:** Use laptop or chair. Start before the pinch and do not cut until the correct card is visible for two seconds.
2. **Spatial proof, 6–8 seconds:** Keep the completed object/card pair visible during a slow sideways step.
3. **Second-object master, 12–15 seconds:** Prefer table at a visibly different distance. Record the complete attempt.
4. **Two-card wide, 8–10 seconds:** Keep both physical objects and cards visible during gentle movement.
5. **End beauty shot, 4–5 seconds:** Real annotations, tracked hand, no casting or debug UI.

Wall is available in the implemented vocabulary, but wall/floor/ceiling attachment is not reliable enough to use as spatial-placement proof. Do not demonstrate surface-aware orientation unless a fresh physical test succeeds.

## Editing rules

- Use actual Quest footage for every product-behavior claim.
- Keep each recognition attempt intact; never splice a failed pinch to a result from another attempt.
- It is fine to trim waiting time, but keep enough of `ANALYZING…` to make the asynchronous call clear.
- Do not call inference cloud-hosted, on-device, or standalone. It runs in Ollama on the USB-connected laptop.
- Do not claim arbitrary-object recognition or perfect accuracy.
- Do not call current-session world-fixed cards persistent spatial anchors.
- If a take ends in `NOT RECOGNIZED`, either show it honestly or use a separate complete successful take.

## Safe backup cut

If only one recognition take succeeds, use that complete laptop or chair attempt, the sideways world-fixed proof, the architecture diagram, and the multi-annotation beauty shot. A truthful 40–45 second cut is stronger than a stitched or scripted result.
