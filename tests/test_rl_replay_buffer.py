import sys
import os
import numpy as np

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '../AI/ReinforcementLearning')))

from rl_core import RLAgent

def test_replay_buffer_push():
    agent = RLAgent(state_size=27, action_size=5)
    state = np.random.rand(27).astype(np.float32)
    agent.remember(state, 0, 1.0, state, False)
    assert len(agent.memory) == 1