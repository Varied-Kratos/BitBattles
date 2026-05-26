import pytest
import numpy as np


@pytest.fixture
def sample_state():
    return np.random.rand(27).astype(np.float32)


@pytest.fixture
def sample_ga_state():
    state = np.zeros(102)
    state[0] = 1.0
    state[25] = 1.0
    state[100] = 15
    state[101] = 1
    return state
