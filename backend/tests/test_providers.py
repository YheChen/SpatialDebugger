"""Provider selection, degradation and the sponsor seams."""

import pytest

from app import providers
from app.config import get_settings, reset_settings_cache
from app.models import AnalyzeRequest, AskRequest
from app.providers.base import ProviderError
from app.providers.openai_provider import OpenAIProvider
from app.providers.omni_provider import OmniPerceptionProvider


def test_mock_is_selected_by_default():
    assert providers.select_reasoning_provider().name == "mock"


def test_openai_unavailable_without_a_key(monkeypatch):
    monkeypatch.setenv("SPATIALDEBUGGER_PROVIDER", "openai")
    reset_settings_cache()
    assert OpenAIProvider().available() is False
    # ...and selection must still hand back something usable.
    assert providers.select_reasoning_provider().name == "mock"


def test_omni_unavailable_without_config():
    assert OmniPerceptionProvider().available() is False


def test_omni_describe_raises_when_unconfigured():
    with pytest.raises(ProviderError):
        OmniPerceptionProvider().describe("ZmFrZQ==")


def test_openai_parses_a_well_formed_response():
    parsed = OpenAIProvider()._parse(
        '{"speech":"Check GPIO 12.","actions":[{"type":"marker","position":[0,0,0]}]}'
    )
    assert parsed.speech == "Check GPIO 12."
    assert len(parsed.actions) == 1
    assert parsed.provider == "openai"


def test_openai_drops_malformed_actions_but_keeps_the_answer():
    parsed = OpenAIProvider()._parse(
        '{"speech":"ok","actions":[{"type":"marker","position":[0,0,0]},'
        '{"type":"label","position":[0,0,0]},{"type":"nonsense"}]}'
    )
    # The label has no text and "nonsense" is not a type; both are discarded.
    assert len(parsed.actions) == 1


def test_openai_rejects_non_json():
    with pytest.raises(ProviderError):
        OpenAIProvider()._parse("I'm afraid I can't do that")


def test_openai_rejects_a_response_with_no_speech():
    with pytest.raises(ProviderError):
        OpenAIProvider()._parse('{"actions":[]}')


class _BrokenProvider:
    name = "broken"

    def available(self):
        return True

    def ask(self, request):
        raise ProviderError("simulated outage")

    def analyze(self, request):
        raise RuntimeError("simulated SDK explosion")


def test_route_degrades_to_the_demo_when_a_provider_fails(client, monkeypatch):
    monkeypatch.setattr(providers, "select_reasoning_provider", lambda: _BrokenProvider())
    response = client.post("/ask", json={"question": "why is my LED dark"})
    assert response.status_code == 200
    body = response.json()
    assert body["provider"] == "mock"
    assert body["actions"]
    assert "offline fallback" in body["speech"]


def test_route_degrades_when_a_provider_raises_an_unexpected_error(client, monkeypatch):
    monkeypatch.setattr(providers, "select_reasoning_provider", lambda: _BrokenProvider())
    response = client.post("/analyze", json={})
    assert response.status_code == 200
    assert response.json()["actions"]


def test_fallback_can_be_disabled(client, monkeypatch):
    monkeypatch.setenv("SPATIALDEBUGGER_FALLBACK_TO_MOCK", "false")
    reset_settings_cache()
    monkeypatch.setattr(providers, "select_reasoning_provider", lambda: _BrokenProvider())
    response = client.post("/ask", json={"question": "why is my LED dark"})
    assert response.status_code == 500
    assert response.json()["error"] == "internal_error"


def test_mock_provider_handles_both_request_types():
    mock = providers.MOCK
    assert mock.ask(AskRequest(question="led is dark")).actions
    assert mock.analyze(AnalyzeRequest()).actions
