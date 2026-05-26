import torch
import torch.nn as nn
import json
import socket


# ------------------- Архитектура нейросети -------------------
class DQN(nn.Module):
    def __init__(self, input_size, output_size):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(input_size, 128),
            nn.ReLU(),
            nn.Dropout(0.1),
            nn.Linear(128, 128),
            nn.ReLU(),
            nn.Dropout(0.1),
            nn.Linear(128, 64),
            nn.ReLU(),
            nn.Linear(64, output_size),
        )

    def forward(self, x):
        return self.net(x)


# ------------------- Класс RL-агента (только инференс) -------------------
class RLAgent:
    def __init__(self, state_size, action_size, model_path="rl_model_best.pth"):
        self.state_size = state_size
        self.action_size = action_size
        self.device = torch.device("cpu")

        self.model = DQN(state_size, action_size).to(self.device)
        self.model.eval()

        # Загружаем лучшую обученную модель
        try:
            checkpoint = torch.load(model_path, map_location=self.device)
            if isinstance(checkpoint, dict) and "model_state_dict" in checkpoint:
                self.model.load_state_dict(checkpoint["model_state_dict"])
            else:
                self.model.load_state_dict(checkpoint)
            print(f"Модель загружена: {model_path}")
        except FileNotFoundError:
            print(f"Модель не найдена: {model_path}, используется случайная")

    def act(self, state):
        state_t = torch.tensor(state, dtype=torch.float32).unsqueeze(0).to(self.device)
        with torch.no_grad():
            q_values = self.model(state_t)
        return torch.argmax(q_values).item()


# ------------------- TCP-сервер для инференса -------------------
HOST = "127.0.0.1"
PORT = 65432
STATE_SIZE = 27
ACTION_SIZE = 5

agent = RLAgent(STATE_SIZE, ACTION_SIZE, model_path="rl_model_best.pth")

print(f"RL Инференс сервер (ЛУЧШАЯ МОДЕЛЬ) запущен на {HOST}:{PORT}")
print(f"Состояние: {STATE_SIZE} чисел, Действий: {ACTION_SIZE}")

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server.bind((HOST, PORT))
server.listen(5)

while True:
    conn, addr = server.accept()
    print(f"Подключён: {addr}")

    with conn:
        while True:
            try:
                data = conn.recv(4096)
                if not data:
                    break

                msg = json.loads(data.decode().strip())
                state = msg.get("state", [])

                # Дополняем или обрезаем до STATE_SIZE
                if len(state) < STATE_SIZE:
                    state += [0.0] * (STATE_SIZE - len(state))
                else:
                    state = state[:STATE_SIZE]

                # Предсказываем действие
                action = agent.act(state)

                # Отправляем ответ
                response = json.dumps({"action": action})
                conn.sendall((response + "\n").encode())

            except json.JSONDecodeError:
                continue
            except Exception as e:
                print(f"Ошибка: {e}")
                break

    print(f"Клиент отключился: {addr}")
