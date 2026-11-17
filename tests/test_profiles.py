import math

import pytest

from tube_cad_cam import RectangularTube, RoundTube


def test_round_tube_area():
    t = RoundTube(outer_diameter=50, wall_thickness=2)
    assert t.inner_diameter == 46
    assert t.cross_section_area == pytest.approx(math.pi / 4 * (50**2 - 46**2))


def test_rectangular_tube_area():
    t = RectangularTube(width=40, height=20, wall_thickness=2)
    assert t.cross_section_area == pytest.approx(40 * 20 - 36 * 16)


def test_invalid_wall_thickness():
    with pytest.raises(ValueError):
        RoundTube(outer_diameter=10, wall_thickness=5)
    with pytest.raises(ValueError):
        RectangularTube(width=10, height=10, wall_thickness=5)
