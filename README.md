# SpatialDebugger

Debug physical electronics in mixed reality on a Meta Quest 3. Point at a wire
on a breadboard, ask what's wrong, and the answer appears in the air next to the
actual hardware.

```
      ❌ Possible incorrect connection
                  │
              GPIO 12
                  │
     ┌────────────▼─────────────┐
     │  ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪   │   ← your real breadboard, in passthrough
     │  ▪ ▪ ▪ ◉ ▪ ▪ ▪ ▪ ▪ ▪ ▪   │      ◉ = the point you pinched
     └──────────────────────────┘
                  ▲
          ──── Connect here
```

Hack the North 2026.

## What works right now

- **The full pipeline**: select a target → run an analysis → spatial
  annotations appear anchored to it.
- **Five annotation types**: label, warning, marker, arrow, highlight — plus
  clear.
- **Demo mode that cannot fail**: no network, no computer vision, no sponsor
  APIs, no internet. The Quest answers from on-device scenarios.
- **A FastAPI backend** with 54 passing tests that runs with no API keys.
- **48 Unity EditMode tests** over the protocol, the JSON parser and the demo.
- **A verified Quest 3 APK** — builds headlessly, correct permissions, one
  OpenXR loader. Not yet run on hardware.
- **Seams for Huawei OMNI** (perception) and **OpenAI** (reasoning), both
  optional.

Precise computer-vision localisation is **not** implemented — see
[ARCHITECTURE.md](ARCHITECTURE.md) for the intended pipeline and which installed
Meta XR APIs would support it.

Current status, what still needs a physical headset, and the morning checklist
are in [CLAUDE_PROGRESS.md](CLAUDE_PROGRESS.md).

## Layout

```
SpatialDebugger/
├── Unity/          Unity 6000.6.2f1 project, Meta XR SDK v205, URP
├── backend/        FastAPI service
├── ARCHITECTURE.md How it fits together and why
└── CLAUDE_PROGRESS.md  Status, known issues, morning checklist
```

## Run the backend

```bash
cd backend
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

`--host 0.0.0.0` matters: the Quest is a separate device and cannot reach
`localhost` on your Mac. Details in [backend/README.md](backend/README.md).

## Open the Unity project

Open `Unity/` in Unity **6000.6.2f1**, then run, in order:

1. `Tools > SpatialDebugger > 1. Configure Project`
2. `Tools > SpatialDebugger > 2. Build MR Scene`
3. `Tools > SpatialDebugger > 3. Build Android APK`

Step 1 is idempotent and handles XR plug-in setup, OpenXR features, Meta
project config, TextMeshPro resources, shader inclusion and Android player
settings. Step 2 regenerates `Assets/Scenes/SpatialDebugger.unity` from
scratch — it is a build output, not a hand-edited file.

## Try it without a headset

Open `Assets/Scenes/SpatialDebugger.unity` and press Play. The scene contains a
stand-in breadboard (Editor only). Point at it with the mouse, click to place a
target, then press **Demo Analysis** on the panel. You should get a warning, a
label, a marker, an arrow and a highlight, all anchored to where you clicked.

## Headless build

```bash
/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath Unity -logFile build.log \
  -executeMethod SpatialDebugger.EditorTools.SpatialDebuggerBuild.BuildAndroid
```

The Unity Editor must be closed: two Unity processes cannot hold the same
project path.
