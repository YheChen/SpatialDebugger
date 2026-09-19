"""Deterministic demo scenarios.

These exist so the whole ``target -> reasoning -> spatial annotation`` pipeline
can be demonstrated with no API keys, no network, no computer vision and no
sponsor services. They are the product's reliable fallback, not a placeholder.

All coordinates are in TARGET space and sized for a breadboard-scale object:
the origin is whatever the user pointed at in the headset, +Y is up, and the
numbers are metres. Nothing here needs to know real Quest world coordinates.
"""

from __future__ import annotations

import hashlib
from typing import Dict, List, Sequence

from .models import ActionType, AIResponse, SpatialAction, Space

# Per-type colours. The Unity renderers have their own defaults too, so these
# are only sent where a scenario wants something other than the default.
RED = "#FF4136"
AMBER = "#FF9F1C"
CYAN = "#2EC4F3"
GREEN = "#2ECC71"


class Scenario:
    """A canned analysis: what to say and what to draw."""

    def __init__(
        self,
        scenario_id: str,
        title: str,
        speech: str,
        actions: Sequence[SpatialAction],
        keywords: Sequence[str] = (),
    ) -> None:
        self.id = scenario_id
        self.title = title
        self.speech = speech
        self.actions = list(actions)
        self.keywords = [k.lower() for k in keywords]

    def to_response(self, provider: str = "mock", confidence: float = 0.82) -> AIResponse:
        return AIResponse(
            speech=self.speech,
            # Copy so a caller mutating the response cannot corrupt the catalogue.
            actions=[a.model_copy(deep=True) for a in self.actions],
            scenario=self.id,
            provider=provider,
            confidence=confidence,
        )


def _label(position, text, **kw) -> SpatialAction:
    return SpatialAction(type=ActionType.LABEL, position=position, text=text, **kw)


def _warning(position, text, **kw) -> SpatialAction:
    return SpatialAction(type=ActionType.WARNING, position=position, text=text, **kw)


def _marker(position, **kw) -> SpatialAction:
    return SpatialAction(type=ActionType.MARKER, position=position, **kw)


def _arrow(frm, to, text=None, **kw) -> SpatialAction:
    return SpatialAction(type=ActionType.ARROW, **{"from": frm}, to=to, text=text, **kw)


def _highlight(position, radius=0.04, **kw) -> SpatialAction:
    return SpatialAction(
        type=ActionType.HIGHLIGHT, position=position, radius=radius, **kw
    )


# --------------------------------------------------------------------------
# The catalogue
# --------------------------------------------------------------------------

#: The scenario the "Demo Analysis" button fires. Keep this one exactly as the
#: demo script expects: warning + arrow into the selected point.
DEFAULT_SCENARIO_ID = "led_not_working"

_SCENARIOS: List[Scenario] = [
    Scenario(
        "led_not_working",
        "LED does not light",
        "The LED's anode looks like it lands on GPIO 12, but the sketch is driving GPIO 13. "
        "Move that jumper one row over and re-flash.",
        [
            _marker([0.0, 0.0, 0.0], color=RED),
            _label([0.0, 0.06, 0.0], "GPIO 12", color=CYAN),
            _warning([0.0, 0.15, 0.0], "Possible incorrect connection", color=RED),
            _arrow([0.10, 0.25, 0.0], [0.0, 0.02, 0.0], "Connect here", color=AMBER),
            _highlight([0.0, 0.0, 0.0], radius=0.05, color=RED),
        ],
        keywords=["led", "light", "lit", "dark", "not working", "won't turn on", "blink"],
    ),
    Scenario(
        "missing_resistor",
        "LED wired without a current-limiting resistor",
        "That LED goes straight from the GPIO pin to ground with no series resistor. "
        "Add about 220 ohms in line before you power it again.",
        [
            _marker([0.0, 0.0, 0.0], color=RED),
            _warning([0.0, 0.16, 0.0], "No current-limiting resistor", color=RED),
            _label([0.0, 0.07, 0.0], "Add 220 ohm", color=AMBER),
            _arrow([-0.09, 0.22, 0.02], [0.0, 0.01, 0.0], "Insert resistor here", color=AMBER),
            _highlight([0.0, 0.0, 0.0], radius=0.045, color=AMBER),
        ],
        keywords=["resistor", "burn", "burnt", "too bright", "current limit", "ohm"],
    ),
    Scenario(
        "gpio_mismatch",
        "Pin in firmware does not match the wiring",
        "The wire is seated in GPIO 12 but the firmware configures GPIO 4. "
        "Either move the wire or change the pin number in your sketch.",
        [
            _marker([0.0, 0.0, 0.0], color=AMBER),
            _label([0.0, 0.06, 0.0], "wired: GPIO 12", color=CYAN),
            _label([0.0, 0.11, 0.0], "code: GPIO 4", color=AMBER),
            _warning([0.0, 0.18, 0.0], "Pin mismatch", color=RED),
            _arrow([0.0, 0.11, 0.0], [0.0, 0.02, 0.0], color=AMBER),
        ],
        keywords=["gpio", "pin", "wrong pin", "mismatch", "pinmode", "digitalwrite"],
    ),
    Scenario(
        "power_rail",
        "Breadboard power rail not bridged",
        "The top and bottom power rails are not bridged, so the right-hand half of the board "
        "has no 3V3. Run a jumper across the rails.",
        [
            _marker([0.0, 0.0, 0.0], color=RED),
            _warning([0.0, 0.17, 0.0], "Rail not powered", color=RED),
            _label([0.06, 0.06, 0.0], "3V3 missing", color=CYAN),
            _arrow([-0.12, 0.05, 0.0], [0.12, 0.05, 0.0], "Bridge the rails", color=GREEN),
            _highlight([0.0, 0.0, 0.0], radius=0.07, color=RED),
        ],
        keywords=["power", "rail", "3v3", "5v", "vcc", "no power", "dead", "not powered"],
    ),
    Scenario(
        "ground_missing",
        "No common ground between ESP32 and the breadboard",
        "The ESP32's GND is not tied to the breadboard's ground rail, so nothing has a return path. "
        "Add a jumper from any GND pin to the blue rail.",
        [
            _marker([0.0, 0.0, 0.0], color=RED),
            _warning([0.0, 0.16, 0.0], "No common ground", color=RED),
            _label([0.0, 0.07, 0.0], "GND", color=CYAN),
            _arrow([0.08, 0.20, -0.02], [0.0, 0.01, 0.0], "Tie to ground rail", color=AMBER),
        ],
        keywords=["ground", "gnd", "common ground", "return path", "floating ground"],
    ),
    Scenario(
        "i2c_pullup",
        "I2C bus missing pull-up resistors",
        "SDA and SCL are floating -- there are no pull-ups on the bus, so the scan finds nothing. "
        "Add 4.7k from each line to 3V3.",
        [
            _marker([0.0, 0.0, 0.0], color=AMBER),
            _label([-0.04, 0.06, 0.0], "SDA", color=CYAN),
            _label([0.04, 0.06, 0.0], "SCL", color=CYAN),
            _warning([0.0, 0.17, 0.0], "Missing 4.7k pull-ups", color=RED),
            _arrow([-0.04, 0.20, 0.0], [-0.04, 0.02, 0.0], color=AMBER),
            _arrow([0.04, 0.20, 0.0], [0.04, 0.02, 0.0], color=AMBER),
        ],
        keywords=["i2c", "sda", "scl", "pull-up", "pullup", "sensor not found", "address"],
    ),
    Scenario(
        "floating_input",
        "Button input left floating",
        "That button pin has no pull-up or pull-down, so it reads random noise when the button is open. "
        "Use INPUT_PULLUP or add a 10k to 3V3.",
        [
            _marker([0.0, 0.0, 0.0], color=AMBER),
            _warning([0.0, 0.15, 0.0], "Floating input", color=AMBER),
            _label([0.0, 0.06, 0.0], "use INPUT_PULLUP", color=CYAN),
            _arrow([0.09, 0.19, 0.0], [0.0, 0.02, 0.0], "Add 10k here", color=AMBER),
        ],
        keywords=["button", "switch", "floating", "noise", "random", "bounce", "input"],
    ),
    Scenario(
        "reversed_polarity",
        "Component inserted backwards",
        "The LED is in backwards -- the flat edge and short leg are the cathode and they are on the "
        "3V3 side. Rotate it 180 degrees.",
        [
            _marker([0.0, 0.0, 0.0], color=RED),
            _warning([0.0, 0.15, 0.0], "Reversed polarity", color=RED),
            _label([0.0, 0.06, 0.0], "cathode (flat edge)", color=CYAN),
            _arrow([-0.07, 0.10, 0.0], [0.07, 0.10, 0.0], "Flip 180 degrees", color=GREEN),
            _highlight([0.0, 0.0, 0.0], radius=0.035, color=RED),
        ],
        keywords=["backwards", "reversed", "polarity", "cathode", "anode", "diode", "upside down"],
    ),
]

SCENARIOS: Dict[str, Scenario] = {s.id: s for s in _SCENARIOS}


def scenario_ids() -> List[str]:
    return list(SCENARIOS.keys())


def get(scenario_id: str) -> Scenario | None:
    return SCENARIOS.get(scenario_id)


def select(text: str | None, forced: str | None = None) -> Scenario:
    """Pick a scenario deterministically.

    ``forced`` wins. Otherwise the best keyword match wins. With no match we
    hash the text so the same question always produces the same answer -- a
    demo that changes its mind between run-throughs is worse than a wrong one.
    """
    if forced:
        chosen = SCENARIOS.get(forced)
        if chosen is not None:
            return chosen

    if not text:
        return SCENARIOS[DEFAULT_SCENARIO_ID]

    lowered = text.lower()
    best: Scenario | None = None
    best_score = 0
    for scenario in _SCENARIOS:
        # Longer keyword matches are stronger evidence than short ones.
        score = sum(len(k) for k in scenario.keywords if k in lowered)
        if score > best_score:
            best, best_score = scenario, score

    if best is not None:
        return best

    # No keyword hit: stable hash so the answer is reproducible across runs.
    # (Python's built-in hash() is salted per-process, so use blake2b.)
    digest = hashlib.blake2b(lowered.encode("utf-8"), digest_size=8).digest()
    index = int.from_bytes(digest, "big") % len(_SCENARIOS)
    return _SCENARIOS[index]


def clear_action() -> SpatialAction:
    """The one action that carries no geometry."""
    return SpatialAction(type=ActionType.CLEAR, space=Space.TARGET)
