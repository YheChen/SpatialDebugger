"""Runtime configuration. Everything has a working default with no API keys."""

from __future__ import annotations

import os
from functools import lru_cache
from typing import List

VERSION = "0.1.0"


def _env_bool(name: str, default: bool) -> bool:
    raw = os.getenv(name)
    if raw is None:
        return default
    return raw.strip().lower() in ("1", "true", "yes", "on")


class Settings:
    """Read straight from the environment; a missing ``.env`` is not an error."""

    def __init__(self) -> None:
        # Provider selection. "mock" is the default and never needs credentials.
        self.provider: str = os.getenv("SPATIALDEBUGGER_PROVIDER", "mock").strip().lower()

        self.openai_api_key: str | None = os.getenv("OPENAI_API_KEY") or None
        self.openai_model: str = os.getenv("OPENAI_MODEL", "gpt-4o-mini")

        self.omni_api_key: str | None = os.getenv("OMNI_API_KEY") or None
        self.omni_endpoint: str | None = os.getenv("OMNI_ENDPOINT") or None

        # If a live provider errors, fall back to the deterministic scenarios
        # rather than failing the request. This is what keeps the demo alive.
        self.fallback_to_mock: bool = _env_bool("SPATIALDEBUGGER_FALLBACK_TO_MOCK", True)

        self.host: str = os.getenv("SPATIALDEBUGGER_HOST", "0.0.0.0")
        self.port: int = int(os.getenv("SPATIALDEBUGGER_PORT", "8000"))

        # Unity on a Quest is a different origin; "*" is correct for a LAN demo.
        origins = os.getenv("SPATIALDEBUGGER_CORS_ORIGINS", "*")
        self.cors_origins: List[str] = [o.strip() for o in origins.split(",") if o.strip()]

        self.request_timeout_seconds: float = float(
            os.getenv("SPATIALDEBUGGER_TIMEOUT", "20")
        )


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()


def reset_settings_cache() -> None:
    """Tests flip environment variables and need the next read to see them."""
    get_settings.cache_clear()
