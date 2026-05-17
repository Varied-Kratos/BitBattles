import numpy as np
import copy
import random
import torch
from GA.network import BattleNet


class GAManager:
    def __init__(self, pop_size=15, mutation_power=0.05):
        self.pop_size = pop_size
        self.mutation_power = mutation_power # Насколько сильно меняются веса
        # Создаем популяцию
        self.population = [BattleNet() for _ in range(pop_size)]
        self.fitness_history = []

    def evolve(self, fitness_scores):
        """
        Переход к новому поколению
        fitness_scores: список урона для каждой сети [150, 10, 300...]
        """
        # 1. Логируем статистику для графиков
        self.fitness_history.append({
            'max': max(fitness_scores),
            'avg': np.mean(fitness_scores)
        })

        # 2. Селекция (Tournament Selection)
        # Мы не просто убиваем всех, а даем шанс разным особям
        new_population = []
        
        # Элитизм: сохраняем лучшего без изменений
        best_idx = np.argmax(fitness_scores)
        new_population.append(self.population[best_idx])

        # 3. Заполняем остальную популяцию
        while len(new_population) < self.pop_size:
            # Скрещиваем двух случайных лидеров из ТОП-3
            parent1, parent2 = self._select_parents(fitness_scores)
            
            child_weights = self._crossover(parent1, parent2)
            child_weights = self._mutate(child_weights)
            
            child = BattleNet()
            child.set_weights(child_weights)
            new_population.append(child)
            
        self.population = new_population
    
    def _select_parents(self, fitness_scores):
        """Выбирает двух лучших из случайной группы (Турнир)"""
        def tournament():
            subset_indices = random.sample(range(self.pop_size), 4)
            best_in_subset = max(subset_indices, key=lambda i: fitness_scores[i])
            return self.population[best_in_subset]
        
        return tournament(), tournament()
    
    def _crossover(self, p1, p2):
        """Смешиваем веса двух родителей"""
        w1 = p1.get_weights()
        w2 = p2.get_weights()
        mask = np.random.randint(0, 2, size=w1.shape).astype(bool)
        return np.where(mask, w1, w2)
    
    def _mutate(self, weights):
        """Добавляем случайную мутацию"""
        mutation = np.random.normal(0, self.mutation_power, size=weights.shape)
        return weights + mutation