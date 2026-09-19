"""The /analyze contract, including the exact demo the headset performs."""


def test_analyze_with_empty_body_produces_the_demo(client):
    """Phase 8: press 'Demo Analysis' with nothing selected but a target and
    you must get a warning plus an arrow into the target."""
    response = client.post("/analyze", json={})
    assert response.status_code == 200
    body = response.json()

    assert body["scenario"] == "led_not_working"
    types = [a["type"] for a in body["actions"]]
    assert "warning" in types
    assert "arrow" in types
    assert "marker" in types
    assert "label" in types


def test_demo_warning_text_is_the_scripted_one(client):
    body = client.post("/analyze", json={}).json()
    warning = next(a for a in body["actions"] if a["type"] == "warning")
    assert warning["text"] == "Possible incorrect connection"
    assert len(warning["position"]) == 3


def test_demo_arrow_points_at_the_selected_target(client):
    body = client.post("/analyze", json={}).json()
    arrow = next(a for a in body["actions"] if a["type"] == "arrow")
    assert "from" in arrow and "to" in arrow
    # The arrow head must land on (or next to) the target origin.
    assert all(abs(v) < 0.05 for v in arrow["to"])
    # ...and start somewhere clearly away from it.
    assert any(abs(v) > 0.05 for v in arrow["from"])


def test_all_actions_are_target_relative_by_default(client):
    """The backend must never need real Quest world coordinates."""
    body = client.post("/analyze", json={}).json()
    for action in body["actions"]:
        assert action["space"] == "target"


def test_analyze_coordinates_stay_within_arms_reach(client):
    for scenario in client.get("/scenarios").json()["scenarios"]:
        body = client.post("/analyze", json={"scenario": scenario["id"]}).json()
        for action in body["actions"]:
            for key in ("position", "from", "to"):
                vec = action.get(key)
                if vec:
                    assert all(abs(v) <= 0.3 for v in vec), (scenario["id"], action)


def test_analyze_prefixes_target_label(client):
    body = client.post("/analyze", json={"target_label": "LED anode"}).json()
    assert body["speech"].startswith("LED anode:")


def test_analyze_accepts_and_ignores_an_image(client):
    """image_base64 is reserved for the future CV pipeline; sending it today
    must not break anything."""
    response = client.post("/analyze", json={"image_base64": "ZmFrZQ=="})
    assert response.status_code == 200
    assert response.json()["actions"]


def test_analyze_is_deterministic(client):
    payload = {"context": "ESP32 breadboard circuit", "target_label": "row 14"}
    assert client.post("/analyze", json=payload).json() == client.post(
        "/analyze", json=payload
    ).json()


def test_clear_endpoint_returns_a_single_clear_action(client):
    body = client.post("/clear").json()
    assert len(body["actions"]) == 1
    assert body["actions"][0]["type"] == "clear"


def test_scenarios_endpoint(client):
    body = client.get("/scenarios").json()
    assert body["default"] == "led_not_working"
    ids = [s["id"] for s in body["scenarios"]]
    assert "led_not_working" in ids
    assert len(ids) == len(set(ids))
