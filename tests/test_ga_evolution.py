import numpy as np
import sys
import os

sys.path.insert(
    0,
    os.path.abspath(os.path.join(os.path.dirname(__file__), "../AI/GeneticAlgorithm")),
)

from ga_core import GAManager, BattleNet


def test_gamanager_initial_population():
    ga = GAManager(pop_size=15, mutation_power=0.05)
    assert len(ga.population) == 15


def test_gamanager_mutation():
    ga = GAManager(pop_size=2, mutation_power=0.05)
    net = BattleNet()
    original_weights = net.get_weights().copy()
    mutated_weights = ga._mutate(original_weights)
    assert not np.array_equal(original_weights, mutated_weights)
