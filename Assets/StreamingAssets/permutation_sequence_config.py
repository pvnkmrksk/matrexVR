import json
import random
from pathlib import Path
import numpy as np
from itertools import cycle


'''
after updating the information in this script, please move to the folder where this file is stored and run the following command in the terminal to generate the JSON file:

python permutation_sequence_config.py

after the shuffled JSON file is generated, please move the file to the folder where your Unity project is stored.
'''

# Read the JSON file
random_seed=2
# Specify the number of repetitions
rep=6
## Specify the name of background scene
scene_name='Choice'
#scene_name='Choice_noTextureBG'
#scene_name='Choice_uniBG'
## Specify the name of the template of Inter-trial Interval (ITI) or Inter-stimulus Interval (ISI)
#ISI_template='band'
ISI_template='bilateral_band'
#ISI_template='bifuration'
#ISI_template='choice'
## Specify the name of config file to use
#config_file_name='swarm_4kappa_condition.json'
#config_file_name='swarm_8dir_condition.json'
#config_file_name='swarm_4spe_condition.json'
#config_file_name='choice_3dire_condition.json'
#config_file_name='choice_4dire_2_color.json'
#config_file_name='choice_3dir_2_color.json'
#config_file_name='choice_uniBG_3dir_2_color.json'
#config_file_name='bifurcation_deg45.json'
#config_file_name='bifurcation_deg60_uniBG.json'
#config_file_name='bifurcation_noTextureBG_constant_speed_constant_distance_comparison.json'
#config_file_name='leader_black_constant_speed_60.json'
#config_file_name='leader_black_constant_speed_60.json'
#config_file_name='choice_noTextureBG_3_dir_closed_loop_comparison.json'
#config_file_name='choice_noTextureBG_dir_3_initial_position.json'
#config_file_name='choice_uniBG_3_dir_closed_loop_comparison.json'
config_file_name='leader_black_constant_speed_60_animated.json'

seed_range=np.arange(100)
seed_list=seed_range.tolist()
random.Random(random_seed).shuffle(seed_list)
#rng = np.random.default_rng(seed=42)
#ran_seed_range=rng.random(seed_range)
seed_list=seed_list[:rep]
## Boolean option, whether to insert ISI, ITI or not
insert_isi=True
## Boolean option, whether to use a fixed ISI or a varying lengh of ISI. Note: to vary the length of ITI, an additional JSON file that notes the length of interest is needed.
varying_isi_length=False
## If varying_isi_length is False, then ISI_duration is used in the rest of the code to set that duration for each ITI.
ISI_duration=60

shuffle_file_name=f'shuffle_{config_file_name}'
pre_stim_interval=60 #unit is sec
with open(Path(config_file_name),'r') as file:
    data = json.load(file)

# Shuffle the sequences
# random.Random(random_seed).shuffle(data['sequences'])
# ISI sequence includes "Choice_empty.json"; "Choice_uniBG_empty.json"; "bifurcation_empty_empty.json"; 
# Define the dictionary to be inserted
if insert_isi==True:
    if varying_isi_length:
        if scene_name.startswith('Choice') and ISI_template=='choice':
            isi_file_name='isi_condition_choice.json'
        elif scene_name.startswith('Choice') and ISI_template=='bilateral_band':
            isi_file_name='isi_condition_bilateral_band.json'
        elif scene_name.startswith('Choice') and ISI_template=='band':
            isi_file_name='isi_condition_band.json'    
        else:
            isi_file_name='isi_condition.json'
        with open(Path(isi_file_name),'r') as file:
            isi_list = json.load(file)
            print(isi_list)

    else:
        if scene_name.startswith('Choice') and ISI_template=='choice':
            if scene_name=='Choice_noTextureBG':
                ISI_file_name='Choice_noTextureBG_empty.json'
            else:
                ISI_file_name='Choice_empty.json'
            insert_dict = {
            "sceneName": scene_name,
            "duration": ISI_duration,
            "parameters": {
                "configFile": ISI_file_name
            }
            }
        elif scene_name.startswith('Choice') and ISI_template=='bilateral_band':
            if scene_name=='Choice_noTextureBG':
                ISI_file_name='bilateral_bandM_noTextureBG_empty.json'
            else:
                ISI_file_name='bilateral_bandM_empty.json'
            insert_dict = {
            "sceneName": scene_name,
            "duration": ISI_duration,
            "parameters": {
                "configFile": ISI_file_name
            }
            }
        elif scene_name.startswith('Choice') and ISI_template=='bifuration':
            if scene_name=='Choice_noTextureBG':
                ISI_file_name='bifurcation_noTextureBG_empty_empty.json'
            else:
                ISI_file_name='bifurcation_empty_empty.json'
            insert_dict = {
            "sceneName": scene_name,
            "duration": ISI_duration,
            "parameters": {
                "configFile": ISI_file_name
            }
            }
        elif scene_name.startswith('Choice') and ISI_template=='band':
            if scene_name=='Choice_noTextureBG':
                ISI_file_name='band_noTextureBG_empty.json'
            else:
                ISI_file_name='band_empty.json'
            insert_dict = {
            "sceneName": scene_name,
            "duration": ISI_duration,
            "parameters": {
                "configFile": ISI_file_name
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


# Remove the last inserted dictionary, if you want to skip the last ISI
# new_sequences = new_sequences[:-1]

# # # set the duration in the first scene to be a predefined value
# new_sequences[0]["duration"]=pre_stim_interval this command is problematic, because we use dictionary to manage the trial information and changing the condition in a trial will affect other trials with the same condition.

# Update the sequences in the data
data['sequences'] = new_sequences
# Write the new data back to a new JSON file
with open(Path(shuffle_file_name), 'w') as file:
    json.dump(data, file, indent=4)

print("Shuffled JSON file with inserted dictionaries created successfully.")