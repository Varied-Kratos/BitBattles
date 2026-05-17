import socket
import json
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
from collections import deque
import random
import os

# ------------------- Нейросеть -------------------
class DQN(nn.Module):
    def __init__(self, input_size, output_size):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(input_size, 64),
            nn.ReLU(),
            nn.Linear(64, 64),
            nn.ReLU(),
            nn.Linear(64, output_size)
        )

    def forward(self, x):
        return self.net(x)

# ------------------- Агент с памятью -------------------
class RLAgent:
    def __init__(self, state_size, action_size, lr=0.001):
        self.state_size = state_size
        self.action_size = action_size
        self.memory = deque(maxlen=2000)
        self.gamma = 0.95
        self.epsilon = 0.3   # шанс случайного действия (уменьшается)
        self.epsilon_min = 0.05
        self.epsilon_decay = 0.995
        self.batch_size = 32

        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        self.model = DQN(state_size, action_size).to(self.device)
        self.optimizer = optim.Adam(self.model.parameters(), lr=lr)
        self.loss_fn = nn.MSELoss()

    def remember(self, state, action, reward, next_state, done):
        self.memory.append((state, action, reward, next_state, done))

    def act(self, state, explore=True):
        if explore and np.random.rand() <= self.epsilon:
            return random.randrange(self.action_size)
        state_t = torch.tensor(state, dtype=torch.float32).unsqueeze(0).to(self.device)
        with torch.no_grad():
            q_values = self.model(state_t)
        return torch.argmax(q_values).item()

    def replay(self):
        if len(self.memory) < self.batch_size:
            return
        minibatch = random.sample(self.memory, self.batch_size)
        states, targets = [], []
        for state, action, reward, next_state, done in minibatch:
            state_t = torch.tensor(state, dtype=torch.float32).to(self.device)
            next_t = torch.tensor(next_state, dtype=torch.float32).to(self.device)
            target = reward
            if not done:
                with torch.no_grad():
                    target = reward + self.gamma * torch.max(self.model(next_t)).item()
            q_vals = self.model(state_t).detach().cpu().numpy()
            q_vals[action] = target
            states.append(state)
            targets.append(q_vals)
        states_t = torch.tensor(np.array(states), dtype=torch.float32).to(self.device)
        targets_t = torch.tensor(np.array(targets), dtype=torch.float32).to(self.device)
        self.optimizer.zero_grad()
        loss = self.loss_fn(self.model(states_t), targets_t)
        loss.backward()
        self.optimizer.step()
        if self.epsilon > self.epsilon_min:
            self.epsilon *= self.epsilon_decay

    def save(self, path="rl_model.pth"):
        torch.save(self.model.state_dict(), path)

    def load(self, path="rl_model.pth"):
        if os.path.exists(path):
            self.model.load_state_dict(torch.load(path, map_location=self.device))
            print("Модель загружена")
        else:
            print("Модель не найдена, используется новая")

# ------------------- Сервер -------------------
HOST = '127.0.0.1'
PORT = 65432
STATE_SIZE = 17   # Должно совпадать с длиной вектора из Unity
ACTION_SIZE = 5   # 0-4

agent = RLAgent(STATE_SIZE, ACTION_SIZE)
agent.load()      # загружаем, если есть

print(f"RL сервер с нейросетью слушает {HOST}:{PORT}")
print(f"Состояние: {STATE_SIZE} чисел, Действий: {ACTION_SIZE}")

with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
    s.bind((HOST, PORT))
    s.listen()
    conn, addr = s.accept()
    print('Подключён:', addr)

    # Переменные для хранения опыта (одного шага)
    last_state = None
    last_action = None
    last_unit_id = None

    with conn:
        while True:
            data = conn.recv(4096)
            if not data:
                break
            try:
                message = json.loads(data.decode().strip())
                state = message.get('state', [])
                reward = message.get('reward', 0)
                done = message.get('done', False)
                unit_id = message.get('unit_id', 'none')

                # Если есть предыдущий опыт – сохраняем в память
                if last_state is not None and last_unit_id == unit_id:
                    agent.remember(last_state, last_action, reward, state, done)
                    agent.replay()   # обучаемся на каждом шаге

                if len(state) != STATE_SIZE:
                    print(f"Неверный размер состояния: {len(state)}, ожидалось {STATE_SIZE}")
                    # Принудительно обрезаем или дополняем
                    if len(state) < STATE_SIZE:
                        state += [0.0] * (STATE_SIZE - len(state))
                    else:
                        state = state[:STATE_SIZE]
                # Выбираем действие
                action = agent.act(state)
                last_state = state
                last_action = action
                last_unit_id = unit_id

                # Отправляем действие обратно
                response = json.dumps({"action": action})
                conn.sendall((response + '\n').encode())

                # Если эпизод завершён – сбрасываем последний опыт
                if done:
                    last_state = None
                    last_action = None
                    last_unit_id = None

                # Периодически сохраняем модель
                if random.random() < 0.01:
                    agent.save()
            except Exception as e:
                print("Ошибка:", e)

agent.save()
print("Сервер остановлен, модель сохранена")