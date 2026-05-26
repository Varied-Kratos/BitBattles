import pytest
import numpy as np
import torch
import sys
import os

sys.path.insert(
    0,
    os.path.abspath(
        os.path.join(os.path.dirname(__file__), "../AI/ReinforcementLearning")
    ),
)

from rl_core import RLAgent


def test_rl_agent_act():
    agent = RLAgent(state_size=27, action_size=5)
    state = np.random.rand(27).astype(np.float32)
    action = agent.act(state)
    assert 0 <= action < 5
