# Demo and recording guide

## Decision gate

Use the recognition demo only after this exact flow succeeds on the physical
Quest: pinch → real RGB capture → Moondream response → correct in-place label
update. Camera readiness alone is not enough. If recognition is slow,
inconsistent, or unverified, use the deterministic script.

## Preferred demo: recognition verified (50–60 seconds)

> **Do not use this as a claim until recognition is physically verified.**

| Time | Action | Narration |
|---|---|---|
| 0–6s | Start in Quest passthrough; hold both tracked hands in view. | “SpatialDebugger turns the room around me into a language lesson.” |
| 6–14s | Point at a chair and pinch. Keep the `ANALYZING…` card visible. | “I point naturally and pinch. The label appears immediately while the Quest camera captures what I selected.” |
| 14–23s | Wait for `CHAIR / FR chaise / ES silla`. | “A local vision model identifies the chair and the same card updates in English, French, and Spanish.” |
| 23–31s | Move one or two steps sideways while keeping the label in frame. | “Because this is mixed reality, the answer stays at that place in the room instead of following my screen.” |
| 31–43s | Point at a second known object, pinch, and show its result. | “I can keep going with the objects around me.” |
| 43–52s | Frame both annotations at once. | “Each result is independent, so the room becomes a spatial vocabulary map.” |
| 52–58s | End on the two labels and a visible tracked hand. | “Point. Pinch. Learn.” |

Operator note: choose two objects already tested under the room's actual
lighting. Do not narrate a result until the label has visibly updated.

## Reliable fallback demo (45–55 seconds)

This script is deliberately explicit that the words are prepared vocabulary,
not recognition.

| Time | Action | Narration |
|---|---|---|
| 0–7s | Start in passthrough with both hands visible. | “SpatialDebugger explores what language learning feels like when the room itself becomes the interface.” |
| 7–15s | Point at the chair location and pinch; show `CHAIR / FR chaise / ES silla`. | “I point and pinch to place the next card from our offline vocabulary.” |
| 15–24s | Move sideways to create visible parallax. | “This is the verified spatial interaction: the card stays fixed in the room as I move.” |
| 24–35s | Pinch at a second location; show the next prepared vocabulary card. | “The demo sequence is deterministic—not recognition—so it needs no camera model, server, API key, or internet.” |
| 35–45s | Pull back to show both labels simultaneously. | “Multiple labels coexist, turning the environment into a multilingual memory map.” |
| 45–52s | End on the room and labels. | “Recognition is verified and working; this script is the offline vocabulary mode, chosen deliberately.” |

This script needs `offlineVocabularyMode` ticked on `PinchAnnotationPlacer`
(on `SpatialDebuggerSystems` in the scene) and a rebuild. It is a deliberate,
before-you-go-on-stage choice, and it is the *only* way to get prepared
vocabulary cards.

Removing the USB port mapping no longer produces this script. Since recognition
is physically verified, a recognition that is attempted and fails now shows
`NOT RECOGNIZED` instead of borrowing a word from the cycle — a failed model
must not look like a successful one to a judge.

So if the model goes quiet mid-demo, do not improvise around it. Say so:

> "That is the model failing to answer, not a canned result — the card tells you
> the truth either way."

Then re-pinch. Warm inference has been measured at 1.5–2.4 s.

Run through whichever script you have chosen once before judging, on the exact
APK being demonstrated.

## Devpost video shot list

Target **60–90 seconds** total. Lead with the interaction, not a title card.

1. **Hook, Quest POV (3–5s):** real room in passthrough with a hand entering
   frame; on-screen title: “Point. Pinch. Learn.”
2. **External angle (4–6s):** person wearing Quest, clearly pointing and
   pinching toward the object used in the next POV cut.
3. **Quest POV interaction (8–12s):** hand ray/pinch and immediate label
   placement. Include `ANALYZING…` only if recognition is verified.
4. **Result close-up (6–8s):** readable English/French/Spanish text, including
   at least one accented word such as `portátil` or `sac à dos`.
5. **Spatial proof (6–8s):** continuous sideways head movement that produces
   parallax while the annotation stays in the same room location.
6. **Second object (8–12s):** second pinch and label; end with both annotations
   visible at once.
7. **External reaction/use shot (4–6s):** learner looking between the two real
   locations; avoid pretending the external camera can see virtual content.
8. **Optional architecture card (5–7s):** Quest camera → USB → local Moondream
   → translated world-space label. Mark the vision route “in validation” if it
   is not yet verified.
9. **End card (3s):** project name, tagline, team names, and repository URL.

Recording rules:

- If using deterministic mode, add a small “offline vocabulary demo” caption.
- Do not cut from a pinch on one object to an unrelated label in a way that
  implies recognition.
- Keep at least one unbroken pinch-to-label shot.
- Record clean Quest POV first; external B-roll is replaceable.
- Capture a safety take with deterministic mode even if recognition works.

## Pre-demo checklist

### Hardware and room

- [ ] Quest at least 60% charged; controllers not required
- [ ] USB cable connected securely; Mac power connected
- [ ] Hand tracking enabled; lens and cameras clean
- [ ] Two known, visually distinct demo objects in good light
- [ ] Safe standing area and a short sideways path cleared

### Mac and connection

- [ ] `adb devices` shows exactly the intended Quest as `device`
- [ ] `ollama serve` is running
- [ ] `ollama list` includes `moondream`
- [ ] `adb reverse tcp:11434 tcp:11434` succeeds
- [ ] `adb reverse --list` shows the 11434 mapping
- [ ] Correct APK exists and is installed

### Headset smoke test

- [ ] App opens in XR passthrough, not a flat window or black void
- [ ] Camera permission accepted; logs contain `[camera] READY 1280x960`
- [ ] Hands render and a pinch places a label
- [ ] Recognition test completes correctly on both chosen objects
- [ ] Sideways movement proves the first label is world-fixed
- [ ] Two annotations coexist and accented text renders correctly
- [ ] `./scripts/demo-logs.sh` shows no fatal exception or repeated failure

### Presentation safety

- [ ] Screen casting/recording is connected and already recording
- [ ] Notifications and unrelated windows are hidden
- [ ] Recognition/fallback narration selected before starting
- [ ] Fallback path rehearsed on this APK
- [ ] One clean backup video is locally available

## Fast triage

- **No Quest in `adb devices`:** unlock it, reconnect USB, and accept the
  debugging prompt.
- **Black view:** restart the app; verify passthrough and the transparent
  centre-eye camera configuration.
- **Camera not ready:** accept headset-camera permission and inspect
  `./scripts/demo-logs.sh`.
- **Ollama unreachable:** run `curl http://127.0.0.1:11434/api/tags`, then redo
  `adb reverse tcp:11434 tcp:11434`.
- **Recognition uncertain:** stop retrying after one attempt and switch to the
  fallback script.
- **Text or tracking issue:** play the backup video rather than debugging live
  in front of judges.
