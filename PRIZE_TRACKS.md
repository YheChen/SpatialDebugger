# Hack the North 2026 prize-track material

Checked against the [official Hack the North 2026 Devpost prize
page](https://hackthenorth2026.devpost.com/) on 2026-09-20. Select only tracks
whose eligibility requirements are actually met in the submitted build and
demo. The page currently lists no dedicated Meta/Quest prize track.

## Strong fit: Hack the North 2026 Finalists

**Why it fits:** This is the general award, judged on originality, user
experience, technical complexity, and wow factor. SpatialDebugger combines a
simple visible interaction with difficult XR integration and an honest,
failure-tolerant architecture.

**Relevant aspect:** Quest 3 passthrough, direct hand interaction, world-fixed
multilingual annotations, real camera access, and a polished live spatial demo.

**Paste-ready paragraph:**

SpatialDebugger turns an ordinary room into a mixed-reality vocabulary map.
Using Meta Quest 3 passthrough, learners point and pinch to place English,
French, and Spanish cards in the world, then move around them and keep multiple
labels visible at once. Behind that simple interaction are hand tracking, 6DoF
world-space placement, real Quest RGB camera access, a local vision route, and
a deterministic fallback designed to keep the experience honest and reliable.

## Conditional fit: OpenAI API Prizes

**Current eligibility assessment:** **Do not select as-is.** Codex materially
assisted development and the repo has an optional OpenAI provider adapter, but
the submitted language-learning path does not currently use the OpenAI API.
The official track requires both a product powered by the OpenAI API and a
demonstration of how Codex helped.

**Select only if:** A meaningful OpenAI API feature is integrated, exercised in
the submitted product, and shown live. Merely having an unused adapter or using
Codex for development is insufficient.

**Paste-ready paragraph after that requirement is met:**

SpatialDebugger uses the OpenAI API to **[describe the exact user-visible,
working feature]**, turning a spatial selection into **[describe the resulting
experience]**. Codex also served as our development teammate across XR
architecture, implementation review, automated tests, build diagnosis, and
submission preparation; one concrete example was **[insert a specific verified
bug or workflow Codex helped resolve]**. In the demo we show both the live API
contribution and the shipped product behavior it enables.

## Conditional fit: Aramco Americas — Best Beginner Hack

**Current eligibility assessment:** Unknown because team hackathon history is
not recorded in the repository. The official requirement is that all team
members have attended one or fewer hackathons before Hack the North 2026.

**Select only if:** Every team member confirms that requirement.

**Paste-ready paragraph if eligible:**

As a beginner team, we chose a technically ambitious interaction that forced us
to learn across XR rendering, hand tracking, Android permissions, GPU camera
capture, local AI transport, and spatial UI in one weekend. SpatialDebugger now
runs as a physically tested Quest 3 experience with a deliberately reliable
fallback, and the gap between our starting knowledge and the working
point-and-pinch demo is what we are proudest of.

## Framing-dependent fit: Warp — Best Developer Tool

**Current eligibility assessment:** Weak for the language-learning submission;
do not select it on that framing alone. It becomes plausible only if the
submitted project and live demo prominently include the repository's original
physical-electronics debugging workflow. This track has no API requirement,
but it specifically rewards developer experience.

**Relevant repository evidence:** The optional FastAPI backend emits typed
labels, warnings, markers, arrows, and highlights for selected physical
targets, with deterministic circuit scenarios and automated tests. That system
exists, but it should not be presented as live computer-vision debugging.

**Paste-ready paragraph for an electronics-debugging submission:**

SpatialDebugger explores a new developer interface for physical prototyping:
instead of translating a debugging answer back from a terminal to a
breadboard, a developer selects the real location and receives structured
labels, warnings, markers, arrows, and highlights beside it in mixed reality.
The typed spatial-action protocol, Quest renderer, deterministic circuit
scenarios, and graceful backend fallback are implemented and tested; live
visual circuit diagnosis remains future work.

## Tracks that the repository does not currently qualify for

- **Huawei OMNI Live:** The repo contains an optional, unused OMNI adapter, but
  the track requires a working cloud-API experience that meaningfully combines
  vision/video, speech/audio, and language. Current camera + Ollama recognition
  does not meet that requirement.
- **Huawei openJiuwen Multi-Agent Challenge:** No genuine multi-agent
  collaboration is implemented.
- **Baseten, Elastic, Sentry, Gemini, ElevenLabs, and other API tracks:** No
  meaningful integration is evidenced in the current project.
- **Tether/QVAC, Expo, QNX, Bracket Bot, and other platform tracks:** Their
  required platform or hardware is not part of this build.

Do not add a sponsor integration solely in the submission text. It must be in
the product, functional, and demonstrable before selecting the corresponding
track.
