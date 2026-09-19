"""Wire protocol shared by the FastAPI backend and the Unity client.

Design constraints that shape these models:

* Unity deserialises these payloads on-device. Keep every response a JSON
  *object* at the top level (never a bare array) and keep action objects
  flat -- no polymorphic nesting -- so both Newtonsoft.Json and Unity's
  built-in JsonUtility can read them.
* ``from``/``to`` are reserved words in Python and C#. They travel on the wire
  under their natural names and are aliased locally.
* Positions are ``[x, y, z]`` float triples in Unity's left-handed,
  Y-up, metres coordinate system.
"""

from __future__ import annotations

from enum import Enum
from typing import Annotated, List, Optional

from pydantic import BaseModel, ConfigDict, Field, field_validator

Vec3 = Annotated[List[float], Field(min_length=3, max_length=3)]


class ActionType(str, Enum):
    """Every spatial action the Unity dispatcher knows how to render."""

    LABEL = "label"
    WARNING = "warning"
    MARKER = "marker"
    ARROW = "arrow"
    HIGHLIGHT = "highlight"
    CLEAR = "clear"


class Space(str, Enum):
    """Which frame ``position``/``from``/``to`` are expressed in.

    ``TARGET`` is the default and the important one: coordinates are relative
    to the debugging target the user selected in the headset, so the backend
    never needs to know real Quest world coordinates.
    """

    TARGET = "target"
    WORLD = "world"


class SpatialAction(BaseModel):
    """One renderable annotation.

    Deliberately a single flat model rather than a discriminated union: Unity
    cannot cheaply deserialise polymorphic JSON, so unused fields are simply
    omitted and the renderer keys off ``type``.
    """

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    type: ActionType
    id: Optional[str] = Field(
        default=None,
        description="Stable id so a later action can update or clear just this one.",
    )
    space: Space = Space.TARGET

    position: Optional[Vec3] = Field(
        default=None, description="label / warning / marker / highlight anchor"
    )
    from_: Optional[Vec3] = Field(
        default=None, alias="from", description="arrow tail"
    )
    to: Optional[Vec3] = Field(default=None, description="arrow head")

    text: Optional[str] = Field(default=None, max_length=280)
    color: Optional[str] = Field(
        default=None,
        description="#RRGGBB or #RRGGBBAA. Renderer falls back to a per-type default.",
    )
    scale: float = Field(default=1.0, gt=0.0, le=20.0)
    radius: Optional[float] = Field(
        default=None, gt=0.0, le=10.0, description="highlight sphere radius, metres"
    )
    duration: float = Field(
        default=0.0,
        ge=0.0,
        description="Seconds before auto-despawn. 0 means persist until cleared.",
    )

    @field_validator("color")
    @classmethod
    def _validate_color(cls, v: Optional[str]) -> Optional[str]:
        if v is None:
            return v
        if not v.startswith("#") or len(v) not in (7, 9):
            raise ValueError("color must be #RRGGBB or #RRGGBBAA")
        try:
            int(v[1:], 16)
        except ValueError as exc:  # pragma: no cover - message clarity only
            raise ValueError("color must be hexadecimal") from exc
        return v.upper()

    def model_post_init(self, __context: object) -> None:
        """Enforce the geometry each action type actually needs."""
        needs_position = {
            ActionType.LABEL,
            ActionType.WARNING,
            ActionType.MARKER,
            ActionType.HIGHLIGHT,
        }
        if self.type in needs_position and self.position is None:
            raise ValueError(f"action type '{self.type.value}' requires 'position'")
        if self.type is ActionType.ARROW and (self.from_ is None or self.to is None):
            raise ValueError("action type 'arrow' requires both 'from' and 'to'")
        if self.type in (ActionType.LABEL, ActionType.WARNING) and not self.text:
            raise ValueError(f"action type '{self.type.value}' requires 'text'")


class AskRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    question: str = Field(min_length=1, max_length=2000)
    context: Optional[str] = Field(default=None, max_length=4000)
    scenario: Optional[str] = Field(
        default=None,
        description="Force a specific demo scenario id. Overrides keyword matching.",
    )


class AnalyzeRequest(BaseModel):
    """A request to analyse the circuit around the user's selected target."""

    model_config = ConfigDict(extra="forbid")

    context: Optional[str] = Field(default=None, max_length=4000)
    scenario: Optional[str] = Field(default=None)
    question: Optional[str] = Field(default=None, max_length=2000)
    image_base64: Optional[str] = Field(
        default=None,
        description="Reserved for the future passthrough-camera pipeline. Ignored by the mock provider.",
    )
    target_label: Optional[str] = Field(
        default=None, max_length=120, description="Human name of the selected target."
    )


class AIResponse(BaseModel):
    """What Unity's SpatialActionDispatcher consumes."""

    model_config = ConfigDict(populate_by_name=True)

    speech: str = Field(description="One or two sentences, spoken/shown to the user.")
    actions: List[SpatialAction] = Field(default_factory=list)
    scenario: Optional[str] = Field(default=None, description="Which demo scenario answered.")
    provider: str = Field(default="mock", description="mock | openai | omni")
    confidence: float = Field(default=1.0, ge=0.0, le=1.0)


class HealthResponse(BaseModel):
    status: str = "ok"
    service: str = "spatialdebugger-backend"
    version: str
    providers: dict[str, bool] = Field(
        description="Which providers are configured and usable right now."
    )
    scenarios: List[str] = Field(description="Demo scenario ids available offline.")


class ErrorResponse(BaseModel):
    error: str
    detail: Optional[str] = None
