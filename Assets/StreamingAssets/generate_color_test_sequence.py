import json
from pathlib import Path
# Read the JSON file
config_file_name='sequenceConfig.json'
#new_file_name='sequenceConfig_color_test_empty.json'
this_dir='/home/flyvr01/src/matrexVR/Assets/StreamingAssets/'
with open(Path(this_dir)/config_file_name,'r') as file:
    data = json.load(file)

files_number=11
#json_files=['bilateral_bandM_empty','bilateral_bandM90_','bilateral_band_cylinder_b_M90_','bilateral_band_cylinder_w_M90_','bilateral_band_locust_g_M90_','bilateral_band_locust_y1_M90_','bilateral_band_locust_y2_M90_']
this_json='bilateral_band_locust_y2_M90_'
new_file_name=f'sequenceConfig_background_color_{this_json}.json'
# Insert the dictionary between each existing dictionary
new_sequences = []
for this_file_number in range(files_number):
    #for this_json in (json_files):
    print(this_file_number)
    print(this_json)
    this_dict=f'{this_json}{this_file_number}.json'
    insert_dict = {
        "sceneName": "Choice_noBG",
        "duration": 3,
        "parameters": {
            "configFile": this_dict
        }
        }
    new_sequences.append(insert_dict)


# Remove the last inserted dictionary to avoid an extra one at the end
# new_sequences = new_sequences[:-1]

# Update the sequences in the data
data['sequences'] = new_sequences

# Write the new data back to a new JSON file
with open(Path(this_dir)/new_file_name, 'w') as file:
    json.dump(data, file, indent=4)
