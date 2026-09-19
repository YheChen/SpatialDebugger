# SpatialDebugger backend

FastAPI service that turns a question about a physical circuit into **speech +
structured spatial actions** that the Quest client renders as mixed-reality
annotations.

**It runs with no API keys.** The default provider is a deterministic set of
demo scenarios, so the whole `target -> reasoning -> annotation` pipeline works
offline, on a plane, with the sponsor APIs down.

## Run it

```bash
cd backend
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

`--host 0.0.0.0` matters: the Quest is a separate device on your LAN and cannot
reach `localhost` on your Mac. Find the address to give Unity with:

```bash
ipconfig getifaddr en0
```

Then set that IP on the `AIClient` component in the Unity scene (or via the
in-headset settings). Interactive API docs: <http://localhost:8000/docs>.

## Tests

```bash
cd backend
.venv/bin/python -m pytest
```

54 tests cover the action protocol, both endpoints, keyword routing,
determinism, malformed input, and provider degradation.

## Endpoints

| Method | Path         | Purpose |
|--------|--------------|---------|
| GET    | `/health`    | Liveness + which providers are configured + offline scenario ids |
| POST   | `/ask`       | Free-text question about the circuit |
| POST   | `/analyze`   | Analyse the selected target (the headset's **Demo Analysis** button) |
| POST   | `/clear`     | Returns the single `clear` action |
| GET    | `/scenarios` | Every offline scenario, for driving the demo deliberately |

### Example

```bash
curl -s localhost:8000/ask -H 'content-type: application/json' -d '{
  "question": "Why isn'\''t this LED working?",
  "context": "ESP32 breadboard circuit"
}'
```

```json
{
  "speech": "The LED's anode looks like it lands on GPIO 12, ...",
  "actions": [
    {"type": "marker",  "space": "target", "position": [0.0, 0.0, 0.0]},
    {"type": "label",   "space": "target", "position": [0.0, 0.06, 0.0], "text": "GPIO 12"},
    {"type": "warning", "space": "target", "position": [0.0, 0.15, 0.0], "text": "Possible incorrect connection"},
    {"type": "arrow",   "space": "target", "from": [0.1, 0.25, 0.0], "to": [0.0, 0.02, 0.0], "text": "Connect here"}
  ],
  "scenario": "led_not_working",
  "provider": "mock",
  "confidence": 0.82
}
```

## The SpatialAction protocol

One flat object per annotation. Flat, not polymorphic, because Unity has to
deserialise it on-device.

| Field      | Type       | Used by |
|------------|------------|---------|
| `type`     | `label \| warning \| marker \| arrow \| highlight \| clear` | all |
| `space`    | `target \| world` (default `target`) | all |
| `position` | `[x,y,z]`  | label, warning, marker, highlight |
| `from`     | `[x,y,z]`  | arrow |
| `to`       | `[x,y,z]`  | arrow |
| `text`     | string     | label, warning (required); arrow (optional) |
| `color`    | `#RRGGBB`  | optional, renderer has per-type defaults |
| `scale`    | float > 0  | optional |
| `radius`   | float      | highlight |
| `duration` | seconds, `0` = persist | optional |

**Coordinates are metres, relative to the target the user selected in the
headset** (`space: "target"`, the default). The target is `[0,0,0]` and `+Y` is
up. This is the key design decision: the backend never needs to know real Quest
world coordinates, so mock responses are fully usable.

## Configuration

Copy `.env.example` to `.env`. Every value is optional.

| Variable | Default | Meaning |
|----------|---------|---------|
| `SPATIALDEBUGGER_PROVIDER` | `mock` | `mock` or `openai` |
| `SPATIALDEBUGGER_FALLBACK_TO_MOCK` | `true` | On provider failure, serve the offline scenario instead of a 5xx |
| `SPATIALDEBUGGER_PORT` | `8000` | |
| `SPATIALDEBUGGER_CORS_ORIGINS` | `*` | Comma-separated |
| `OPENAI_API_KEY` / `OPENAI_MODEL` | unset | Optional reasoning provider |
| `OMNI_API_KEY` / `OMNI_ENDPOINT` | unset | Optional Huawei OMNI perception seam |

Keep `SPATIALDEBUGGER_FALLBACK_TO_MOCK=true` for the demo: `/ask` and
`/analyze` then always return 200 with something renderable, even if a live
provider times out mid-presentation.

## Sponsor seams

```
Huawei OMNI          camera/video + speech/audio -> multimodal perception
  app/providers/omni_provider.py      (PerceptionProvider)

OpenAI               scene/context -> reasoning -> structured SpatialActions
  app/providers/openai_provider.py    (ReasoningProvider)
```

Both are adapters behind `app/providers/base.py` protocols and both import
their SDK lazily, so the service starts and its tests pass with neither
installed. `app/providers/__init__.py::select_reasoning_provider` never returns
`None` — if the configured provider is unusable it hands back the mock.

## Not committed

`.env`, `.venv/`, `__pycache__/`. See the repo `.gitignore`.
