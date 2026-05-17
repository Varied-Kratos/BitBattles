import torch
import torch.nn as nn
import numpy as np


class BattleNet(nn.Module):
    def __init__(self, input_size=102, hidden1 = 64, hidden2 = 32, output_size = 225):
        super().__init__()
        self.fc1 = nn.Linear(input_size,hidden1)
        self.fc2 = nn.Linear(hidden1,hidden2)
        self.fc3 = nn.Linear(hidden2,output_size)
        
        for param in self.parameters():
            param.requires_grad = False
    
    def forward(self, x):
        x = torch.FloatTensor(x)
        x = self.fc1(x)
        x = torch.relu(x)
        x = self.fc2(x)
        x = torch.relu(x)
        x = self.fc3(x)
        
        return torch.sigmoid(x)
    
    def get_weights(self):
        weights = []
        for param in self.parameters():
            weights.extend(param.numpy().flatten())
        return np.array(weights)
    
    def set_weights(self, weights):
        """Принимает список чисел (от ГА) и вставляет их обратно в структуру нейросети"""
        start = 0
        for param in self.parameters():
            # Считаем, сколько чисел нужно для конкретного слоя
            size = param.numel()
            # Берем нужный кусок из длинного списка и меняем его форму под слой
            new_param = weights[start:start+size].reshape(param.shape)
            param.copy_(torch.from_numpy(new_param))
            start += size

def decode_output(probabilities, budget):
    """
    probabilities: 225 чисел от нейронки
    budget: сколько эликсира дала Unity
    """
    # Стоимость: [тип][уровень]
    # 1: Рыцарь, 2: Лучник, 3: Маг
    UNIT_COSTS = {
        1: {1: 3, 2: 6, 3: 9},
        2: {1: 2, 2: 4, 3: 6},
        3: {1: 4, 2: 8, 3: 12}
    }

    layout = ["0:0"] * 25
    current_budget = budget
    occupied_cells = set()

    # 1. Группируем выходы: каждые 25 нейронов — это конкретный юнит+уровень
    # Нам нужно превратить 225 чисел в список понятных вариантов
    all_options = []
    
    # Всего 9 комбинаций (3 типа * 3 уровня)
    combinations = [
        (1, 1), (1, 2), (1, 3), # Рыцари 1,2,3
        (2, 1), (2, 2), (2, 3), # Лучники 1,2,3
        (3, 1), (3, 2), (3, 3)  # Маги 1,2,3
    ]

    for combo_idx, (u_type, u_level) in enumerate(combinations):
        start_idx = combo_idx * 25
        # Берем 25 чисел, отвечающих за эту конкретную комбинацию
        chunk = probabilities[start_idx : start_idx + 25]
        
        for cell_idx, confidence in enumerate(chunk):
            all_options.append({
                'cell': cell_idx,
                'type': u_type,
                'level': u_level,
                'conf': confidence,
                'cost': UNIT_COSTS[u_type][u_level]
            })

    # 2. Сортируем все 225 вариантов по уверенности (самые желанные — в начало)
    all_options.sort(key=lambda x: x['conf'], reverse=True)

    # 3. "Жадный" выбор: ставим, если есть место и деньги
    for opt in all_options:
        if opt['cell'] in occupied_cells:
            continue
        
        if opt['conf'] < 0.2: # Порог "осмысленности"
            continue

        if current_budget >= opt['cost']:
            layout[opt['cell']] = f"{opt['type']}:{opt['level']}"
            current_budget -= opt['cost']
            occupied_cells.add(opt['cell'])

    return ",".join(layout)
