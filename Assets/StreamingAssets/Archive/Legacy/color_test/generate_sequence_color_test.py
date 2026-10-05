import json
import numpy as np
from pathlib import Path
sequence_file='sequenceConfig.json'
this_dir=Path.cwd()
with open(Path(this_dir)/sequence_file,'r') as file:
    data = json.load(file)

scene_names=["Choice_uniBG"]
#scene_names=["Choice_noBG"]
template_type=['band']
#template_type=['choice']
choice_variable=["",'LeaderLocust','LeaderLocust_yellow1','LeaderLocust_yellow2','LeaderLocust_green','LeaderLocust_black','LeaderLocust_3Dblack','LeaderLocust_white','LeaderLocust_3Dwhite']
#choice_variable=["",'LeaderLocust','LeaderLocust_yellow1','LeaderLocust_yellow2','LeaderLocust_green']
#choice_variable=["",'LeaderLocust_black','LeaderLocust_3Dblack','LeaderLocust_white','LeaderLocust_3Dwhite']
band_variable=["",'LocustBand','LocustBand_yellow1','LocustBand_yellow2','LocustBand_green','LocustBand_black','LocustBand_3Dblack','LocustBand_white','LocustBand_3Dwhite']
#band_variable=["",'LocustBand','LocustBand_yellow1','LocustBand_yellow2','LocustBand_green']
#band_variable=["",'LocustBand_black','LocustBand_3Dblack','LocustBand_white','LocustBand_3Dwhite']
background_variable=np.linspace(0,10,16)
c_radiuses=[5,8]
b_radiuses=[0,32]

new_sequences = []

for this_scene in scene_names:
    for this_template in template_type:
        config_file_name=f"{this_template}.json"
        with open(Path(this_dir)/config_file_name,'r') as file:
            agent_profile = json.load(file)
        if this_template=='band':
            foreground_variable=band_variable
            radiuses=b_radiuses
            duration=9
        else:
            foreground_variable=choice_variable
            radiuses=c_radiuses
            duration=9
        for this_radius in radiuses:
            for this_agent in foreground_variable:
                if this_template=='band':
                    agent_profile['objects'][0]['type']=this_agent
                    agent_profile['objects'][1]['type']=this_agent
                    agent_profile["objects"][0]["position"]["radius"]=this_radius
                    agent_profile["objects"][1]["position"]["radius"]=this_radius
                else:
                    agent_profile["objects"][0]['type'] =this_agent
                    agent_profile["objects"][0]["position"]["radius"]=this_radius
                if this_scene=="Choice_noBG":
                    for this_value in background_variable:
                        # for this_g in background_variable:
                        #     for this_b in background_variable:
                        file_name=f"{this_template}_{this_radius}_{this_agent}_{this_value}.json"
                        rgb_value=this_value/10
                        agent_profile['backgroundColor']['r']=rgb_value
                        agent_profile['backgroundColor']['g']=rgb_value
                        agent_profile['backgroundColor']['b']=rgb_value
                        with open(Path(this_dir)/file_name, 'w') as file:
                            json.dump(agent_profile, file, indent=4)

                        insert_dict = {
                            "sceneName": this_scene,
                            "duration": duration,
                            "parameters": {
                                "configFile": file_name
                            }
                            }
                        new_sequences.append(insert_dict)
                        
                else:
                    file_name=f"{this_template}_{this_radius}_{this_agent}.json"
                    agent_profile['backgroundColor']['r']=0.4745
                    agent_profile['backgroundColor']['g']=0.5803
                    agent_profile['backgroundColor']['b']=0.7215
                    with open(Path(this_dir)/file_name, 'w') as file:
                        json.dump(agent_profile, file, indent=4)

                    insert_dict = {
                        "sceneName": this_scene,
                        "duration": duration,
                        "parameters": {
                            "configFile": file_name
                        }
                        }
                    new_sequences.append(insert_dict)

data['sequences'] = new_sequences
# Write the new data back to a new JSON file
with open(Path(this_dir)/sequence_file, 'w') as file:
    json.dump(data, file, indent=4)