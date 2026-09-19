import os
import sys
from pathlib import Path

import pytest

# Make `app` importable when pytest is run from the backend directory.
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


@pytest.fixture(autouse=True)
def _clean_env(monkeypatch):
    """Every test starts from the documented offline defaults."""
    for key in (
        "SPATIALDEBUGGER_PROVIDER",
        "SPATIALDEBUGGER_FALLBACK_TO_MOCK",
        "OPENAI_API_KEY",
        "OMNI_API_KEY",
        "OMNI_ENDPOINT",
    ):
        monkeypatch.delenv(key, raising=False)
    from app.config import reset_settings_cache

    reset_settings_cache()
    yield
    reset_settings_cache()


@pytest.fixture
def client():
    from fastapi.testclient import TestClient

    from app.main import app

    # raise_server_exceptions=False makes the test client behave like a real
    # HTTP server: a handler that blows up becomes a 500 response rather than
    # propagating into the test. That is the contract the headset sees.
    with TestClient(app, raise_server_exceptions=False) as c:
        yield c
