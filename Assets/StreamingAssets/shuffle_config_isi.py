import json
import random
from pathlib import Path
# Read the JSON file
# random_seed=3
# rep=3
insert_isi=True
config_file_name='swarm_4kappa_condition.json'
shuffle_file_name='shuffled_sequenceConfig_4kappa3.json'
with open(Path(config_file_name),'r') as file:
    data = json.load(file)

# Shuffle the sequences
# random.Random(random_seed).shuffle(data['sequences'])
random.shuffle(data['sequences'])

# Define the dictionary to be inserted
if insert_isi==True:
    insert_dict = {
      "sceneName": "Swarm",
      "duration": 60,
      "parameters": {
        "numberOfLocusts": 0,
        "mu": 0,
        "kappa" :1,
        "locustSpeed" : 2
      }
    }

# Insert the dictionary between each existing dictionary
new_sequences = []
# for this_rep in range(rep):
for sequence in data['sequences']:
    if insert_isi==True:
        new_sequences.append(insert_dict)
    new_sequences.append(sequence)


# Remove the last inserted dictionary to avoid an extra one at the end
# new_sequences = new_sequences[:-1]

# Update the sequences in the data
data['sequences'] = new_sequences

# Write the new data back to a new JSON file
with open(Path(shuffle_file_name), 'w') as file:
    json.dump(data, file, indent=4)

print("Shuffled JSON file with inserted dictionaries created successfully.")
