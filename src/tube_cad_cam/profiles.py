"""Einfache Rohrprofile (Querschnitte) mit Kennwerten. Einheiten: mm."""

from __future__ import annotations

import math
from dataclasses import dataclass


@dataclass(frozen=True)
class RoundTube:
    outer_diameter: float
    wall_thickness: float

    def __post_init__(self) -> None:
        if self.outer_diameter <= 0 or self.wall_thickness <= 0:
            raise ValueError("Durchmesser und Wandstärke müssen positiv sein")
        if 2 * self.wall_thickness >= self.outer_diameter:
            raise ValueError("Wandstärke zu groß für den Durchmesser")

    @property
    def inner_diameter(self) -> float:
        return self.outer_diameter - 2 * self.wall_thickness

    @property
    def cross_section_area(self) -> float:
        return math.pi / 4 * (self.outer_diameter**2 - self.inner_diameter**2)


@dataclass(frozen=True)
class RectangularTube:
    width: float
    height: float
    wall_thickness: float

    def __post_init__(self) -> None:
        if min(self.width, self.height, self.wall_thickness) <= 0:
            raise ValueError("Maße müssen positiv sein")
        if 2 * self.wall_thickness >= min(self.width, self.height):
            raise ValueError("Wandstärke zu groß für das Profil")

    @property
    def cross_section_area(self) -> float:
        t = self.wall_thickness
        return self.width * self.height - (self.width - 2 * t) * (self.height - 2 * t)
