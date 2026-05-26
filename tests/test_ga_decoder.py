import pytest
import numpy as np
import sys
import os


sys.path.insert(
    0,
    os.path.abspath(os.path.join(os.path.dirname(__file__), "../AI/GeneticAlgorithm")),
)


def test_decode_output_respects_budget():

    from ga_core import decode_output, UNIT_COSTS

    probs = np.zeros(225)
    probs[0] = 0.9
    probs[25] = 0.9
    probs[50] = 0.9

    state_vector = np.zeros(102)

    layout = decode_output(probs, budget=5, state_vector=state_vector)

    cells = layout.split(",")
    total_cost = 0
    placed_units = 0

    for cell in cells[:25]:
        if cell != "0:0":
            placed_units += 1
            type_id, level = map(int, cell.split(":"))
            total_cost += UNIT_COSTS[type_id][level]

    assert total_cost <= 5
    assert placed_units <= 2


def test_decode_output_no_units_on_blocked_cells():

    from ga_core import decode_output

    probs = np.ones(225)
    state_vector = np.zeros(102)
    state_vector[0] = -1

    layout = decode_output(probs, budget=15, state_vector=state_vector)
    cells = layout.split(",")

    assert cells[0] == "0:0"


def test_decode_output_respects_occupied_cells():

    from ga_core import decode_output

    probs = np.ones(225)
    state_vector = np.zeros(102)

    layout = decode_output(probs, budget=30, state_vector=state_vector)
    cells = layout.split(",")

    occupied = 0
    for cell in cells[:25]:
        if cell != "0:0":
            occupied += 1

    assert occupied <= 10


def test_decode_output_confidence_threshold():

    from ga_core import decode_output

    probs = np.zeros(225)
    probs[0] = 0.1
    state_vector = np.zeros(102)

    layout = decode_output(probs, budget=15, state_vector=state_vector)
    cells = layout.split(",")

    assert cells[0] == "0:0"
