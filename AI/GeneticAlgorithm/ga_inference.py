import torch
import numpy as np
import socket


class BattleNet(torch.nn.Module):
    def __init__(self):
        super().__init__()
        self.fc1 = torch.nn.Linear(102, 64)
        self.fc2 = torch.nn.Linear(64, 32)
        self.fc3 = torch.nn.Linear(32, 225)

    def forward(self, x):
        x = torch.relu(self.fc1(x))
        x = torch.relu(self.fc2(x))
        x = torch.sigmoid(self.fc3(x))
        return x


UNIT_COSTS = {1: {1: 3, 2: 6, 3: 9}, 2: {1: 2, 2: 4, 3: 6}, 3: {1: 4, 2: 8, 3: 12}}


def decode_output(probs, budget, state_vector):
    layout = ["0:0"] * 25
    current_budget = budget
    occupied_cells = set()
    blocked_cells = {
        i for i in range(25) if state_vector[i] == -1 or state_vector[i] == 1
    }

    combinations = [
        (1, 1),
        (1, 2),
        (1, 3),
        (2, 1),
        (2, 2),
        (2, 3),
        (3, 1),
        (3, 2),
        (3, 3),
    ]
    all_options = []

    for combo_idx, (u_type, u_level) in enumerate(combinations):
        start_idx = combo_idx * 25
        for cell_idx, conf in enumerate(probs[start_idx : start_idx + 25]):
            all_options.append(
                {
                    "cell": cell_idx,
                    "type": u_type,
                    "level": u_level,
                    "conf": conf,
                    "cost": UNIT_COSTS[u_type][u_level],
                }
            )

    all_options.sort(key=lambda x: x["conf"], reverse=True)

    for opt in all_options:
        if opt["cell"] in occupied_cells or opt["cell"] in blocked_cells:
            continue
        if opt["conf"] < 0.2:
            continue
        if current_budget >= opt["cost"]:
            layout[opt["cell"]] = f"{opt['type']}:{opt['level']}"
            current_budget -= opt["cost"]
            occupied_cells.add(opt["cell"])

    return ",".join(layout)


model = BattleNet()
model.load_state_dict(
    torch.load(
        "/Users/alan/Desktop/BitBattles/AI/GeneticAlgorithm/best_ga_model_best.pth",
        map_location="cpu",
    )
)
model.eval()

HOST = "127.0.0.1"
PORT = 5005

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server.bind((HOST, PORT))
server.listen(5)

print(f"GA Инференс сервер (ЛУЧШАЯ МОДЕЛЬ) запущен на {HOST}:{PORT}")

while True:
    conn, addr = server.accept()
    data = conn.recv(4096).decode().strip()
    parts = data.split("|")
    budget = float(parts[0])
    state_vector = np.fromstring(parts[2], sep=",")

    with torch.no_grad():
        probs = model(torch.FloatTensor(state_vector)).numpy()

    layout = decode_output(probs, budget, state_vector)
    conn.sendall(layout.encode())
    conn.close()
