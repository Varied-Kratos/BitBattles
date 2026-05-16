import random
from deap import base, creator, tools

class GAManager:
    def __init__(self, pop_size=10, board_size=25, max_elixir=10):
        self.pop_size = pop_size
        self.max_elixir = max_elixir
        # 0: пусто, 1: Рыцарь(3), 2: Лучник(2), 3: Маг(4)
        self.unit_costs = {0: 0, 1: 3, 2: 2, 3: 4} 
        
        # Настройка DEAP
        if not hasattr(creator, "FitnessMax"):
            creator.create("FitnessMax", base.Fitness, weights=(1.0,))
        if not hasattr(creator, "Individual"):
            creator.create("Individual", list, fitness=creator.FitnessMax)

        self.toolbox = base.Toolbox()
        self.toolbox.register("attr_unit", lambda: random.choices([0, 1, 2, 3], weights=[85, 5, 5, 5])[0])
        self.toolbox.register("individual", tools.initRepeat, creator.Individual, 
                              self.toolbox.attr_unit, n=board_size)
        self.toolbox.register("population", tools.initRepeat, list, self.toolbox.individual)
        
        self.toolbox.register("mate", tools.cxTwoPoint)
        self.toolbox.register("mutate", tools.mutUniformInt, low=0, up=3, indpb=0.1)
        self.toolbox.register("select", tools.selTournament, tournsize=3)

        self.population = self.toolbox.population(n=self.pop_size)
        self.generation = 1

    def get_individual(self, idx):
        return self.population[idx]

    def set_fitness(self, idx, raw_fitness):
        ind = self.population[idx]
        total_cost = sum(self.unit_costs[u] for u in ind)
        
        if total_cost > self.max_elixir:
            # Увеличиваем штраф в 100 раз, чтобы ИИ было ОЧЕНЬ больно за перебор
            final_fitness = -10.0 - (total_cost) 
        else:
            final_fitness = raw_fitness
                
        ind.fitness.values = (final_fitness,)
        return total_cost, final_fitness

    def evolve(self):
        print(f"\n--- GEN {self.generation} DONE. EVOLVING ---")
        offspring = self.toolbox.select(self.population, len(self.population))
        offspring = list(map(self.toolbox.clone, offspring))

        for child1, child2 in zip(offspring[::2], offspring[1::2]):
            if random.random() < 0.5:
                self.toolbox.mate(child1, child2)
                del child1.fitness.values
                del child2.fitness.values

        for mutant in offspring:
            if random.random() < 0.2:
                self.toolbox.mutate(mutant)
                del mutant.fitness.values

        self.population = offspring
        self.generation += 1