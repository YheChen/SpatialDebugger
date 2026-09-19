"""Huawei OMNI perception adapter -- optional, never required.

Responsibility in the architecture::

    camera/video + speech/audio + language -> multimodal perception

This is a seam, not an integration. It deliberately does not guess at an API
surface: :meth:`describe` raises unless both ``OMNI_ENDPOINT`` and
``OMNI_API_KEY`` are configured, and the request shape is isolated to one
method so it can be corrected in a single place once the real API is known.
"""

from __future__ import annotations

from typing import Optional

from ..config import get_settings
from .base import ProviderError


class OmniPerceptionProvider:
    name = "omni"

    def available(self) -> bool:
        settings = get_settings()
        return bool(settings.omni_api_key and settings.omni_endpoint)

    def describe(
        self, image_base64: Optional[str], audio_base64: Optional[str] = None
    ) -> str:
        settings = get_settings()
        if not self.available():
            raise ProviderError("OMNI_ENDPOINT and OMNI_API_KEY are not configured")

        try:
            import httpx
        except ImportError as exc:
            raise ProviderError("the 'httpx' package is not installed") from exc

        payload = {
            "image_base64": image_base64,
            "audio_base64": audio_base64,
            "prompt": (
                "Describe the electronics on this breadboard: the board, every component, "
                "and which pin each wire connects to. Be concrete and terse."
            ),
        }
        try:
            response = httpx.post(
                settings.omni_endpoint,
                json={k: v for k, v in payload.items() if v is not None},
                headers={"Authorization": f"Bearer {settings.omni_api_key}"},
                timeout=settings.request_timeout_seconds,
            )
            response.raise_for_status()
            data = response.json()
        except Exception as exc:
            raise ProviderError(f"omni call failed: {exc}") from exc

        # Accept the common shapes rather than asserting one we have not seen.
        for key in ("description", "text", "output", "result"):
            value = data.get(key)
            if isinstance(value, str) and value.strip():
                return value.strip()
        raise ProviderError("omni response contained no description")
