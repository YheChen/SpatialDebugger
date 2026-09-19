import pytest


def test_ask_returns_speech_and_actions(client):
    response = client.post(
        "/ask",
        json={"question": "Why isn't this LED working?", "context": "ESP32 breadboard circuit"},
    )
    assert response.status_code == 200
    body = response.json()
    assert body["speech"]
    assert len(body["actions"]) > 0
    assert body["provider"] == "mock"


def test_ask_works_with_no_api_keys(client):
    """The whole point of the mock provider."""
    body = client.post("/ask", json={"question": "anything at all"}).json()
    assert body["speech"]
    assert body["provider"] == "mock"


def test_ask_is_deterministic(client):
    payload = {"question": "the sensor reads garbage values"}
    first = client.post("/ask", json=payload).json()
    second = client.post("/ask", json=payload).json()
    assert first == second


@pytest.mark.parametrize(
    ("question", "expected"),
    [
        ("my LED won't turn on", "led_not_working"),
        ("do I need a resistor here", "missing_resistor"),
        ("the i2c scan finds no address", "i2c_pullup"),
        ("there is no power on the rail", "power_rail"),
        ("the button reads random noise", "floating_input"),
    ],
)
def test_ask_keyword_routing(client, question, expected):
    body = client.post("/ask", json={"question": question}).json()
    assert body["scenario"] == expected


def test_ask_scenario_override_wins(client):
    body = client.post(
        "/ask", json={"question": "my LED won't turn on", "scenario": "i2c_pullup"}
    ).json()
    assert body["scenario"] == "i2c_pullup"


def test_ask_unknown_scenario_falls_back_rather_than_erroring(client):
    response = client.post(
        "/ask", json={"question": "my LED won't turn on", "scenario": "does_not_exist"}
    )
    assert response.status_code == 200
    assert response.json()["scenario"] == "led_not_working"


def test_ask_rejects_empty_question(client):
    response = client.post("/ask", json={"question": ""})
    assert response.status_code == 422
    assert response.json()["error"] == "invalid_request"


def test_ask_rejects_unknown_fields(client):
    response = client.post("/ask", json={"question": "hi", "nope": 1})
    assert response.status_code == 422


def test_ask_rejects_missing_body(client):
    assert client.post("/ask", json={}).status_code == 422
