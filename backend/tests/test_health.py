def test_health_ok(client):
    response = client.get("/health")
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "ok"
    assert body["service"] == "spatialdebugger-backend"
    assert body["version"]


def test_health_reports_mock_always_available(client):
    body = client.get("/health").json()
    assert body["providers"]["mock"] is True
    # No keys are set in the test environment.
    assert body["providers"]["openai"] is False
    assert body["providers"]["omni"] is False


def test_health_lists_offline_scenarios(client):
    body = client.get("/health").json()
    assert "led_not_working" in body["scenarios"]
    assert len(body["scenarios"]) >= 5
