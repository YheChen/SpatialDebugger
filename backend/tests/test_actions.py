"""The SpatialAction protocol itself -- this is the contract Unity mirrors."""

import pytest
from pydantic import ValidationError

from app.models import ActionType, AIResponse, SpatialAction, Space
from app import scenarios


def test_label_requires_text():
    with pytest.raises(ValidationError):
        SpatialAction(type=ActionType.LABEL, position=[0, 0, 0])


def test_label_requires_position():
    with pytest.raises(ValidationError):
        SpatialAction(type=ActionType.LABEL, text="GPIO 12")


def test_arrow_requires_from_and_to():
    with pytest.raises(ValidationError):
        SpatialAction(type=ActionType.ARROW, **{"from": [0, 0, 0]})
    with pytest.raises(ValidationError):
        SpatialAction(type=ActionType.ARROW, to=[0, 0, 0])


def test_marker_needs_only_a_position():
    action = SpatialAction(type=ActionType.MARKER, position=[0, 0, 0])
    assert action.text is None


def test_clear_needs_nothing():
    assert SpatialAction(type=ActionType.CLEAR).type is ActionType.CLEAR


def test_position_must_be_a_triple():
    with pytest.raises(ValidationError):
        SpatialAction(type=ActionType.MARKER, position=[0, 0])
    with pytest.raises(ValidationError):
        SpatialAction(type=ActionType.MARKER, position=[0, 0, 0, 0])


def test_from_serialises_under_its_wire_name():
    action = SpatialAction(type=ActionType.ARROW, **{"from": [0, 1, 0]}, to=[0, 0, 0])
    dumped = action.model_dump(by_alias=True, exclude_none=True)
    assert "from" in dumped
    assert "from_" not in dumped
    assert dumped["from"] == [0.0, 1.0, 0.0]


def test_from_deserialises_from_its_wire_name():
    action = SpatialAction.model_validate(
        {"type": "arrow", "from": [0, 1, 0], "to": [0, 0, 0]}
    )
    assert action.from_ == [0.0, 1.0, 0.0]


def test_space_defaults_to_target():
    assert SpatialAction(type=ActionType.CLEAR).space is Space.TARGET


def test_colour_must_be_hex():
    with pytest.raises(ValidationError):
        SpatialAction(type=ActionType.MARKER, position=[0, 0, 0], color="red")
    action = SpatialAction(type=ActionType.MARKER, position=[0, 0, 0], color="#ff4136")
    assert action.color == "#FF4136"


def test_unknown_action_type_is_rejected():
    with pytest.raises(ValidationError):
        SpatialAction.model_validate({"type": "explode", "position": [0, 0, 0]})


def test_scale_must_be_positive():
    with pytest.raises(ValidationError):
        SpatialAction(type=ActionType.MARKER, position=[0, 0, 0], scale=0)


def test_every_scenario_validates_and_renders_something():
    for scenario in scenarios.SCENARIOS.values():
        assert scenario.speech.strip()
        assert scenario.actions, scenario.id
        for action in scenario.actions:
            # Round-trips through the wire format without loss.
            payload = action.model_dump(by_alias=True, exclude_none=True)
            assert SpatialAction.model_validate(payload) == action


def test_scenario_responses_are_independent_copies():
    """A mutated response must not corrupt the shared catalogue."""
    first = scenarios.SCENARIOS["led_not_working"].to_response()
    first.actions[0].text = "mutated"
    second = scenarios.SCENARIOS["led_not_working"].to_response()
    assert second.actions[0].text != "mutated"


def test_selection_is_stable_for_unmatched_text():
    picks = {scenarios.select("qqqq zzzz no keywords here").id for _ in range(5)}
    assert len(picks) == 1


def test_ai_response_defaults():
    response = AIResponse(speech="hello")
    assert response.actions == []
    assert response.provider == "mock"
