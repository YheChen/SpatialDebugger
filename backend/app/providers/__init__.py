"""Provider registry.

``select_reasoning_provider`` never raises and never returns ``None``: if the
configured provider is unusable it hands back the mock, because a working demo
matters more than an honest failure at 3am.
"""

from __future__ import annotations

from typing import Dict

from ..config import get_settings
from .base import PerceptionProvider, ProviderError, ReasoningProvider
from .mock import MockProvider, clear_response
from .omni_provider import OmniPerceptionProvider
from .openai_provider import OpenAIProvider

MOCK = MockProvider()
OPENAI = OpenAIProvider()
OMNI = OmniPerceptionProvider()


def select_reasoning_provider() -> ReasoningProvider:
    name = get_settings().provider
    if name == "openai" and OPENAI.available():
        return OPENAI
    return MOCK


def availability() -> Dict[str, bool]:
    return {
        "mock": MOCK.available(),
        "openai": OPENAI.available(),
        "omni": OMNI.available(),
    }


__all__ = [
    "MOCK",
    "OPENAI",
    "OMNI",
    "MockProvider",
    "OpenAIProvider",
    "OmniPerceptionProvider",
    "PerceptionProvider",
    "ProviderError",
    "ReasoningProvider",
    "availability",
    "clear_response",
    "select_reasoning_provider",
]
