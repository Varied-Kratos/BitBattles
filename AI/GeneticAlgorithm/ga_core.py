import torch
import torch.nn as nn
import numpy as np
import copy
import random
import socket
import json

POP_SIZE = 15
MUTATION_RATE = 0.05
INPUT_SIZE = 102
HIDDEN1 = 64
HIDDEN2 = 32
OUTPUT_SIZE = 225
HOST = '127.0.0.1'
PORT = 5005


class BattleNet(nn.Module):
    def __init__(self, input_size=102, hidden1=64, hidden2=32, output_size=225):
        super().__init__()
        self.fc1 = nn.Linear(input_size, hidden1)
        self.fc2 = nn.Linear(hidden1, hidden2)
        self.fc3 = nn.Linear(hidden2, output_size)
        for param in self.parameters():
            param.requires_grad = False

    def forward(self, x):
        x = torch.relu(self.fc1(x))
        x = torch.relu(self.fc2(x))
        x = torch.sigmoid(self.fc3(x))
        return x

    def get_weights(self):
        weights = []
        for param in self.parameters():
            weights.extend(param.numpy().flatten())
        return np.array(weights)

    def set_weights(self, weights):
        start = 0
        for param in self.parameters():
            size = param.numel()
            new_param = weights[start:start+size].reshape(param.shape)
            param.copy_(torch.from_numpy(new_param))
            start += size


UNIT_COSTS = {
    1: {1: 3, 2: 6, 3: 9},
    2: {1: 2, 2: 4, 3: 6},
    3: {1: 4, 2: 8, 3: 12}
}


def decode_output(probabilities, budget, state_vector):
    layout = ["0:0"] * 25
    current_budget = budget
    occupied_cells = set()

    our_half_grid = state_vector[0:25]
    blocked_cells = {i for i, cell_val in enumerate(our_half_grid) if cell_val == -1 or cell_val == 1}

    all_options = []
    combinations = [
        (1, 1), (1, 2), (1, 3),
        (2, 1), (2, 2), (2, 3),
        (3, 1), (3, 2), (3, 3)
    ]

    for combo_idx, (u_type, u_level) in enumerate(combinations):
        start_idx = combo_idx * 25
        chunk = probabilities[start_idx:start_idx + 25]
        for cell_idx, confidence in enumerate(chunk):
            all_options.append({
                'cell': cell_idx,
                'type': u_type,
                'level': u_level,
                'conf': confidence,
                'cost': UNIT_COSTS[u_type][u_level]
            })

    all_options.sort(key=lambda x: x['conf'], reverse=True)

    for opt in all_options:
        if opt['cell'] in occupied_cells or opt['cell'] in blocked_cells:
            continue
        if opt['conf'] < 0.2:
            continue
        if current_budget >= opt['cost']:
            layout[opt['cell']] = f"{opt['type']}:{opt['level']}"
            current_budget -= opt['cost']
            occupied_cells.add(opt['cell'])

    return ",".join(layout)


class GAManager:
    def __init__(self, pop_size=30, mutation_power=0.05):
        self.pop_size = pop_size
        self.mutation_power = mutation_power
        self.initial_mutation_power = mutation_power
        self.population = [BattleNet() for _ in range(pop_size)]
        self.fitness_history = []
        self.best_fitness = -999
        self.best_model = None
        self.stagnation_counter = 0

    def _select_parents(self, fitness_scores):
        def tournament():
            subset_indices = random.sample(range(self.pop_size), 4)
            best_in_subset = max(subset_indices, key=lambda i: fitness_scores[i])
            return self.population[best_in_subset]
        parent1 = tournament()
        parent2 = tournament()
        attempts = 0
        while parent1 is parent2 and attempts < 10:
            parent2 = tournament()
            attempts += 1
        return parent1, parent2

    def _crossover(self, p1, p2):
        w1 = p1.get_weights()
        w2 = p2.get_weights()
        mask = np.random.randint(0, 2, size=w1.shape).astype(bool)
        return np.where(mask, w1, w2)

    def _mutate(self, weights):
        mutation = np.random.normal(0, self.mutation_power, size=weights.shape)
        return weights + mutation

    def evolve(self, fitness_scores):
        all_weights = [bot.get_weights() for bot in self.population]
        diversity = float(np.std(all_weights))

        wins = sum(1 for score in fitness_scores if score > 2.0)
        win_rate = (wins / self.pop_size) * 100.0

        current_max = max(fitness_scores)
        current_avg = np.mean(fitness_scores)

        if len(self.fitness_history) > 0:
            prev_max = self.fitness_history[-1]['max']
            if current_max <= prev_max + 1:
                self.stagnation_counter += 1
            else:
                self.stagnation_counter = 0
            if self.stagnation_counter >= 5:
                self.mutation_power = min(0.15, self.mutation_power * 1.1)
            else:
                self.mutation_power = self.initial_mutation_power
        else:
            self.stagnation_counter = 0

        self.fitness_history.append({
            'max': float(current_max),
            'avg': float(current_avg),
            'min': float(min(fitness_scores)),
            'win_rate': win_rate,
            'diversity': diversity,
            'mutation_power': self.mutation_power
        })

        try:
            with open('ga_metrics.json', 'w', encoding='utf-8') as f:
                json.dump(self.fitness_history, f, indent=4, ensure_ascii=False)
        except Exception as e:
            print(f"Ошибка: {e}")

        new_population = []
        selection_scores = list(fitness_scores)
        sorted_indices = np.argsort(selection_scores)[::-1]
        elite_count = max(1, self.pop_size // 10)

        best_idx = sorted_indices[0]
        champion = self.population[best_idx]

        for i in range(elite_count):
            elite_idx = sorted_indices[i]
            new_population.append(copy.deepcopy(self.population[elite_idx]))

        if current_max > self.best_fitness:
            self.best_fitness = current_max
            self.best_model = copy.deepcopy(champion)
            try:
                torch.save(self.best_model.state_dict(), 'best_ga_model_best.pth')
                print(f"Новая лучшая модель! Fitness: {self.best_fitness:.2f}")
            except Exception as e:
                print(f"Ошибка: {e}")

        try:
            torch.save(champion.state_dict(), 'best_ga_model.pth')
        except Exception as e:
            print(f"Ошибка: {e}")

        while len(new_population) < self.pop_size:
            parent1, parent2 = self._select_parents(selection_scores)
            child_weights = self._crossover(parent1, parent2)
            child_weights = self._mutate(child_weights)
            child = BattleNet()
            child.set_weights(child_weights)
            new_population.append(child)

        self.population = new_population


def run_evolution_cycle():
    ga = GAManager(pop_size=POP_SIZE, mutation_power=MUTATION_RATE)
    generation = 0
    print(f"ГА Сервер запущен на {HOST}:{PORT}")

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server_socket:
        server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server_socket.bind((HOST, PORT))
        server_socket.listen(POP_SIZE)

        while True:
            fitness_scores = []
            print(f"\n--- [Поколение {generation}]: Ожидание {POP_SIZE} подключений ---")

            for i in range(POP_SIZE):
                conn, addr = server_socket.accept()
                with conn:
                    raw_data = conn.recv(4096).decode().strip()
                    parts = raw_data.split('|')
                    budget = float(parts[0])
                    last_fitness = float(parts[1])
                    state_vector = np.fromstring(parts[2], sep=',')

                    fitness_scores.append(last_fitness)

                    current_brain = ga.population[i]
                    with torch.no_grad():
                        state_tensor = torch.FloatTensor(state_vector)
                        probabilities = current_brain(state_tensor).numpy()

                    layout = decode_output(probabilities, budget, state_vector)
                    conn.sendall(layout.encode())
                    print(f"  [OK] Бот {i+1}/{POP_SIZE} обсчитан. Fitness: {last_fitness:.2f}")

            if len(fitness_scores) == POP_SIZE:
                ga.evolve(fitness_scores)
                generation += 1


if __name__ == "__main__":
    run_evolution_cycle()