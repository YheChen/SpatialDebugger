# SpatialDebugger — final 54-second Devpost video

## Edit order

| Time | Picture | Voiceover | Caption | Source |
|---|---|---|---|---|
| 0:00–0:04 | Real Quest POV; hand points toward a chair. | “What if learning a word was as simple as pointing at the thing?” | **POINT. PINCH. LEARN.** | Quest capture |
| 0:04–0:15 | One uninterrupted chair pinch: target, **ANALYZING…**, then the correct chair card. | “On Quest 3, I point and pinch. Depth selects the real location, and Moondream recognizes a crop from the Quest camera.” | **REAL QUEST RGB • LOCAL MOONDREAM** | Quest capture |
| 0:15–0:22 | Hold the chair result and move sideways. | “The answer stays at that place in the room instead of following a phone screen.” | **WORLD-FIXED FOR THIS SESSION** | Quest capture |
| 0:22–0:33 | Complete laptop or table attempt at another position. | “A second pinch creates its own label, with French and Spanish from a local vocabulary.” | **MULTIPLE INDEPENDENT ANNOTATIONS** | Quest capture |
| 0:33–0:40 | Wide shot with both genuine labels and visible parallax. | “Together, the labels turn the room into a spatial vocabulary map.” | **CONSTRAINED CLASSES • LAPTOP • CHAIR • TABLE • WALL** | Quest capture |
| 0:40–0:48 | Show the architecture diagram with a left-to-right highlight. | “The pipeline is Quest camera, pinch and depth target, focused crop, local Ollama and Moondream, then an update to the same spatial card.” | **LOCAL LAPTOP INFERENCE • NO HOSTED VISION API** | Explanatory diagram |
| 0:48–0:54 | Return to the strongest real Quest frame, then end card. | “Uncertain answers stay unrecognized. SpatialDebugger: point, pinch, learn.” | **SPATIALDEBUGGER — Point. Pinch. Learn.** | Quest capture + title card |

## Continuous voiceover

What if learning a word was as simple as pointing at the thing? On Quest 3, I point and pinch. Depth selects the real location, and Moondream recognizes a crop from the Quest camera. The answer stays at that place in the room instead of following a phone screen. A second pinch creates its own label, with French and Spanish from a local vocabulary. Together, the labels turn the room into a spatial vocabulary map. The pipeline is Quest camera, pinch and depth target, focused crop, local Ollama and Moondream, then an update to the same spatial card. Uncertain answers stay unrecognized. SpatialDebugger: point, pinch, learn.

## Quest footage to record

1. **Chair master:** One unbroken clip from before the pinch through **ANALYZING…** to **CHAIR / chaise / silla**.
2. **Spatial proof:** A slow sideways step with the chair and its completed label both visible.
3. **Second-object master:** One unbroken laptop or table recognition.
4. **Two-label wide:** Both genuine annotations visible during gentle head movement.
5. **End frame:** Multiple labels and a tracked hand, with no debug UI.
6. **Optional wall take:** Include only after the current build visibly returns **WALL / mur / pared**.

## Backup edit

If one attempt fails, use another complete successful take. Never splice the pinch from a failed attempt to a result from a different attempt.

If only one object succeeds cleanly, keep that uninterrupted recognition, the sideways spatial proof, the architecture card, and the multi-label or depth-placement shot. Do not claim a second recognition.

An honest failure can be useful: briefly show **NOT RECOGNIZED** and explain that unsupported or uncertain answers are not replaced with scripted words.

## Edit rules

- Lead with real Quest footage, not the logo.
- Keep the object, pinch, and final result causally clear.
- Do not display synthetic distance text as though it came from the headset UI.
- Do not call surface-normal wall/floor/ceiling presentation hardware-verified.
- If concept art appears, keep **CONCEPT VISUAL — NOT QUEST CAPTURE** visible.
