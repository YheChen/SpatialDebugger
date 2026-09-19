"""SpatialDebugger FastAPI service.

Contract with the Unity client:

* every endpoint returns a JSON *object*, never a bare array;
* ``/ask`` and ``/analyze`` always return 200 with a usable
  :class:`~app.models.AIResponse` unless the request itself was malformed --
  a provider outage degrades to the deterministic demo scenarios instead of
  surfacing a 5xx into the headset;
* errors that do escape use ``{"error": ..., "detail": ...}``.
"""

from __future__ import annotations

import logging

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

from . import providers, scenarios
from .config import VERSION, get_settings
from .models import (
    AnalyzeRequest,
    AskRequest,
    AIResponse,
    ErrorResponse,
    HealthResponse,
)
from .providers.base import ProviderError

logger = logging.getLogger("spatialdebugger")

app = FastAPI(
    title="SpatialDebugger",
    version=VERSION,
    description="Mixed-reality electronics debugging backend for Meta Quest 3.",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=get_settings().cors_origins,
    allow_credentials=False,
    allow_methods=["*"],
    allow_headers=["*"],
)


# ---------------------------------------------------------------------------
# Error handling
# ---------------------------------------------------------------------------


@app.exception_handler(RequestValidationError)
async def _validation_handler(request: Request, exc: RequestValidationError) -> JSONResponse:
    return JSONResponse(
        status_code=422,
        content=ErrorResponse(error="invalid_request", detail=str(exc.errors())).model_dump(),
    )


@app.exception_handler(StarletteHTTPException)
async def _http_handler(request: Request, exc: StarletteHTTPException) -> JSONResponse:
    return JSONResponse(
        status_code=exc.status_code,
        content=ErrorResponse(error="http_error", detail=str(exc.detail)).model_dump(),
    )


@app.exception_handler(Exception)
async def _unhandled_handler(request: Request, exc: Exception) -> JSONResponse:
    logger.exception("unhandled error on %s", request.url.path)
    return JSONResponse(
        status_code=500,
        content=ErrorResponse(error="internal_error", detail=str(exc)).model_dump(),
    )


# ---------------------------------------------------------------------------
# Routes
# ---------------------------------------------------------------------------


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    """Cheap liveness probe. The Unity client pings this to decide whether the
    'Ask AI' button should be enabled."""
    return HealthResponse(
        version=VERSION,
        providers=providers.availability(),
        scenarios=scenarios.scenario_ids(),
    )


@app.post("/ask", response_model=AIResponse, response_model_exclude_none=True)
def ask(request: AskRequest) -> AIResponse:
    """Answer a free-text question about the circuit."""
    return _respond(lambda p: p.ask(request), request.scenario, request.question)


@app.post("/analyze", response_model=AIResponse, response_model_exclude_none=True)
def analyze(request: AnalyzeRequest) -> AIResponse:
    """Analyse the selected target. This is what the headset's
    'Demo Analysis' button calls."""
    blob = " ".join(
        filter(None, [request.question, request.context, request.target_label])
    )
    return _respond(lambda p: p.analyze(request), request.scenario, blob or None)


@app.post("/clear", response_model=AIResponse, response_model_exclude_none=True)
def clear() -> AIResponse:
    """Return the single 'clear' action. Present so the Unity client has one
    definition of the action vocabulary to test against."""
    return providers.clear_response()


@app.get("/scenarios")
def list_scenarios() -> dict:
    """Every offline scenario, so the demo can be driven deliberately."""
    return {
        "default": scenarios.DEFAULT_SCENARIO_ID,
        "scenarios": [
            {"id": s.id, "title": s.title, "keywords": s.keywords}
            for s in scenarios.SCENARIOS.values()
        ],
    }


# ---------------------------------------------------------------------------
# Internals
# ---------------------------------------------------------------------------


def _respond(call, forced_scenario: str | None, text: str | None) -> AIResponse:
    """Run the configured provider, degrading to the deterministic scenarios.

    The headset must always get something renderable back.
    """
    provider = providers.select_reasoning_provider()
    settings = get_settings()
    failure: Exception | None = None

    try:
        return call(provider)
    except ProviderError as exc:
        logger.warning("provider %s failed: %s", getattr(provider, "name", "?"), exc)
        failure = exc
    except Exception as exc:  # a provider SDK blowing up must not reach the headset
        logger.exception("provider %s raised: %s", getattr(provider, "name", "?"), exc)
        failure = exc

    if not settings.fallback_to_mock and failure is not None:
        raise failure

    scenario = scenarios.select(text, forced_scenario)
    response = scenario.to_response(provider="mock", confidence=0.5)
    response.speech = f"{response.speech} (offline fallback)"
    return response
