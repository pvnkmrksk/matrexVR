import numpy as np
import random

Marchpop = 64
mu = 1
ordr = 0.001

def Rgenerator():
    while True:
        if ordr == 1.0:
            gen = np.repeat(mu, Marchpop)
            yield gen, None  # No kappa value to return when ordr is 1.0
        else:
            if ordr < 0.5:
                upperlimit = 5
            else:
                upperlimit = 100

            kappa = random.uniform(0.0, upperlimit)
            gen = np.random.vonmises(mu, kappa, Marchpop)
            order = np.round(np.sqrt(((np.sum(np.cos(gen)))**2) + ((np.sum(np.sin(gen)))**2)) / Marchpop, 2)
            np.random.shuffle(gen)

            if order == ordr:
                #print("kappa found, order is", order)
                #print("kappa is", kappa)
                yield gen, kappa

def kappa_distribution(iterations=100):
    kappas = []
    generator = Rgenerator()

    for _ in range(iterations):
        _, kappa = next(generator)
        if kappa is not None:
            kappas.append(kappa)

    return kappas

# Example usage: Generate a distribution of 'kappa' values
distribution = kappa_distribution(100)  # Generate 100 'kappa' values
print(np.mean(distribution))
