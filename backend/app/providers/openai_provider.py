"""OpenAI reasoning adapter -- optional, never required.

Responsibility in the architecture::

    scene/context -> debugging reasoning -> structured SpatialActions

The import of ``openai`` is deliberately lazy so the service starts, serves and
passes its tests on a machine that has never installed the SDK.
"""

from __future__ import annotations

import json
from typing import Any, Dict, List

from ..config import get_settings
from ..models import AnalyzeRequest, AskRequest, AIResponse, SpatialAction
from .base import ProviderError

SYSTEM_PROMPT = """You are SpatialDebugger, an assistant that debugs physical electronics
(ESP32/Arduino/breadboard) for a developer wearing a mixed-reality headset.

You must reply with a single JSON object:
{
  "speech": "<one or two spoken sentences>",
  "actions": [ <spatial action objects> ]
}

A spatial action is one of:
  {"type":"label",     "position":[x,y,z], "text":"..."}
  {"type":"warning",   "position":[x,y,z], "text":"..."}
  {"type":"marker",    "position":[x,y,z]}
  {"type":"highlight", "position":[x,y,z], "radius":0.05}
  {"type":"arrow",     "from":[x,y,z], "to":[x,y,z], "text":"..."}
  {"type":"clear"}

Coordinates are METRES and are RELATIVE TO THE TARGET the user selected: the
target itself is [0,0,0], +Y is up, and the whole board is roughly 0.15m across.
Keep every coordinate within 0.3m of the origin. Emit at most 6 actions.
Do not invent absolute world coordinates -- you cannot see the room."""


def _schema_hint() -> str:
    return SYSTEM_PROMPT


class OpenAIProvider:
    """Thin adapter. Anything that goes wrong raises :class:`ProviderError`
    so the route can fall back to the deterministic mock."""

    name = "openai"

    def available(self) -> bool:
        settings = get_settings()
        if not settings.openai_api_key:
            return False
        try:
            import openai  # noqa: F401
        except ImportError:
            return False
        return True

    # -- internals ---------------------------------------------------------

    def _client(self) -> Any:
        settings = get_settings()
        if not settings.openai_api_key:
            raise ProviderError("OPENAI_API_KEY is not set")
        try:
            from openai import OpenAI
        except ImportError as exc:
            raise ProviderError("the 'openai' package is not installed") from exc
        return OpenAI(api_key=settings.openai_api_key, timeout=settings.request_timeout_seconds)

    def _complete(self, user_content: str) -> AIResponse:
        settings = get_settings()
        client = self._client()
        try:
            completion = client.chat.completions.create(
                model=settings.openai_model,
                response_format={"type": "json_object"},
                messages=[
                    {"role": "system", "content": _schema_hint()},
                    {"role": "user", "content": user_content},
                ],
            )
            raw = completion.choices[0].message.content or "{}"
        except Exception as exc:  # network, auth, rate limit, anything
            raise ProviderError(f"openai call failed: {exc}") from exc

        return self._parse(raw)

    def _parse(self, raw: str) -> AIResponse:
        try:
            payload: Dict[str, Any] = json.loads(raw)
        except json.JSONDecodeError as exc:
            raise ProviderError(f"openai returned non-JSON: {exc}") from exc

        speech = str(payload.get("speech") or "").strip()
        if not speech:
            raise ProviderError("openai response had no 'speech'")

        actions: List[SpatialAction] = []
        for item in payload.get("actions") or []:
            try:
                actions.append(SpatialAction.model_validate(item))
            except Exception:
                # Drop malformed actions rather than failing the whole answer;
                # a partial annotation set still demos.
                continue

        return AIResponse(
            speech=speech, actions=actions, scenario=None, provider=self.name, confidence=0.6
        )

    # -- ReasoningProvider -------------------------------------------------

    def ask(self, request: AskRequest) -> AIResponse:
        parts = [f"Question: {request.question}"]
        if request.context:
            parts.append(f"Context: {request.context}")
        return self._complete("\n".join(parts))

    def analyze(self, request: AnalyzeRequest) -> AIResponse:
        parts = ["Analyse the circuit around the selected target and report the most likely fault."]
        if request.target_label:
            parts.append(f"Target: {request.target_label}")
        if request.context:
            parts.append(f"Context: {request.context}")
        if request.question:
            parts.append(f"Question: {request.question}")
        if request.image_base64:
            parts.append("(An image was supplied but this adapter does not send images yet.)")
        return self._complete("\n".join(parts))
