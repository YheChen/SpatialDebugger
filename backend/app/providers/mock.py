"""The always-available provider. No network, no keys, no CV, no surprises."""

from __future__ import annotations

from ..models import ActionType, AnalyzeRequest, AskRequest, AIResponse, SpatialAction
from .. import scenarios


class MockProvider:
    """Deterministic scenario lookup.

    Same input always produces the same output, which is exactly what you want
    when you are about to run the demo in front of judges.
    """

    name = "mock"

    def available(self) -> bool:
        return True

    def ask(self, request: AskRequest) -> AIResponse:
        blob = " ".join(filter(None, [request.question, request.context]))
        scenario = scenarios.select(blob, request.scenario)
        return scenario.to_response(provider=self.name)

    def analyze(self, request: AnalyzeRequest) -> AIResponse:
        blob = " ".join(
            filter(None, [request.question, request.context, request.target_label])
        )
        # With nothing to go on, /analyze must still produce the headline demo.
        scenario = scenarios.select(blob or None, request.scenario)
        response = scenario.to_response(provider=self.name)
        if request.target_label:
            response.speech = f"{request.target_label}: {response.speech}"
        return response

    def describe(self, image_base64, audio_base64=None) -> str:  # PerceptionProvider
        return "An ESP32 dev board on a breadboard with jumper wires, an LED and a resistor."


def clear_response() -> AIResponse:
    """The response behind the Unity 'Clear' button, kept server-side so the
    action vocabulary has exactly one definition."""
    return AIResponse(
        speech="Annotations cleared.",
        actions=[SpatialAction(type=ActionType.CLEAR)],
        scenario=None,
        provider="mock",
        confidence=1.0,
    )
