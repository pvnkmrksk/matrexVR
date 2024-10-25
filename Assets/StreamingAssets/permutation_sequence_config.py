import json
import random
from pathlib import Path
import numpy as np
from itertools import cycle
# Read the JSON file
random_seed=6
rep=10
scene_name='Choice_noBG'
seed_range=np.arange(100)
seed_list=seed_range.tolist()
random.Random(random_seed).shuffle(seed_list)
#rng = np.random.default_rng(seed=42)
#ran_seed_range=rng.random(seed_range)
seed_list=seed_list[:rep]
insert_isi=True
varying_isi_length=True
choice_assay=True
#config_file_name='swarm_4kappa_condition.json'
#config_file_name='swarm_8dir_condition.json'
#config_file_name='swarm_4spe_condition.json'
#config_file_name='choice_3dire_condition.json'
config_file_name='choice_5dir_band.json'
shuffle_file_name=f'shuffle_{config_file_name}'
pre_stim_interval=60 #unit is sec
with open(Path(config_file_name),'r') as file:
    data = json.load(file)

# Shuffle the sequences
# random.Random(random_seed).shuffle(data['sequences'])

# Define the dictionary to be inserted
if insert_isi==True:
    if varying_isi_length:
        if scene_name.startswith('Choice'):
            isi_file_name='isi_condition_band.json'
        else:
            isi_file_name='isi_condition.json'
        with open(Path(isi_file_name),'r') as file:
            isi_list = json.load(file)
            print(isi_list)

    else:
        if scene_name.startswith('Choice'):
            insert_dict = {
            "sceneName": scene_name,
            "duration": 60,
            "parameters": {
                "configFile": "Choice_empty.json"
            }
            }
        else:
            insert_dict = {
            "sceneName": scene_name,
            "duration": 30,
            "parameters": {
                "numberOfLocusts": 0,
                "mu": 0,
                "kappa" :1,
                "locustSpeed" : 2
            }
            }

# Insert the dictionary between each existing dictionary
new_sequences = []
for this_seed in seed_list:
    random.Random(this_seed).shuffle(data['sequences'])
    if insert_isi and varying_isi_length:
        random.Random(this_seed).shuffle(isi_list['sequences'])
        if len(data['sequences'])==len(isi_list['sequences']):
            for this_trial,this_isi in zip(data['sequences'],isi_list['sequences']):
                new_sequences.append(this_isi)
                new_sequences.append(this_trial)

        else:
            for this_trial,this_isi in zip(data['sequences'],cycle(isi_list['sequences'])):
                new_sequences.append(this_isi)
                new_sequences.append(this_trial)
    else:
        for sequence in data['sequences']:
            if insert_isi==True:
                new_sequences.append(insert_dict)
            new_sequences.append(sequence)


# Remove the last inserted dictionary to avoid an extra one at the end
# new_sequences = new_sequences[:-1]

# # # set the duration in the first scene to be a predefined value
# new_sequences[0]["duration"]=pre_stim_interval this command has a bug

# Update the sequences in the data
data['sequences'] = new_sequences
# Write the new data back to a new JSON file
with open(Path(shuffle_file_name), 'w') as file:
    json.dump(data, file, indent=4)

print("Shuffled JSON file with inserted dictionaries created successfully.")
