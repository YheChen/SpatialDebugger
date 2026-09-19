"""Provider seams.

Tonight only :class:`~app.providers.mock.MockProvider` is wired up. The other
two adapters exist so a sponsor API can be dropped in without touching the
routes, the wire protocol or any Unity code.

    perception (Huawei OMNI)  ->  camera/audio/language -> scene description
    reasoning  (OpenAI)       ->  scene description     -> SpatialActions
"""

from __future__ import annotations

from typing import Optional, Protocol, runtime_checkable

from ..models import AnalyzeRequest, AskRequest, AIResponse


class ProviderError(RuntimeError):
    """A provider could not answer. Callers may fall back to the mock."""


@runtime_checkable
class ReasoningProvider(Protocol):
    """Turns a question plus scene context into speech and spatial actions."""

    name: str

    def available(self) -> bool:
        """True when this provider is configured and usable right now."""
        ...

    def ask(self, request: AskRequest) -> AIResponse:
        ...

    def analyze(self, request: AnalyzeRequest) -> AIResponse:
        ...


@runtime_checkable
class PerceptionProvider(Protocol):
    """Turns raw passthrough camera / audio into a textual scene description.

    Not used tonight. This is the seam Huawei OMNI plugs into: the Unity client
    would attach ``image_base64`` to an ``/analyze`` request, perception would
    describe the board, and the reasoning provider would consume that text.
    """

    name: str

    def available(self) -> bool:
        ...

    def describe(self, image_base64: Optional[str], audio_base64: Optional[str] = None) -> str:
        ...
