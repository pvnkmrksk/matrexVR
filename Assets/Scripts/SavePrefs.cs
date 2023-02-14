using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SavePrefs : MonoBehaviour
{
int intToSave;
float floatToSave;
string stringToSave = "";
void OnGUI()
{
	if (GUI.Button(new Rect(0, 0, 125, 50), "Raise Integer"))
		intToSave++;
	if (GUI.Button(new Rect(0, 100, 125, 50), "Raise Float"))
		floatToSave += 0.1f;
	stringToSave = GUI.TextField(new Rect(0, 200, 125, 25), 
               stringToSave, 15);
	GUI.Label(new Rect(375, 0, 125, 50), "Integer value is " 
               + intToSave);
	GUI.Label(new Rect(375, 100, 125, 50), "Float value is " 
               + floatToSave.ToString("F1"));
	GUI.Label(new Rect(375, 200, 125, 50), "String value is " 
              + stringToSave);
	if (GUI.Button(new Rect(750, 0, 125, 50), "Save Your Game"))
		SaveGame();
	if (GUI.Button(new Rect(750, 100, 125, 50), 
                "Load Your Game"))
		//LoadGame()
        ;
	if (GUI.Button(new Rect(750, 200, 125, 50), 
                "Reset Save Data"))
		//ResetData()
        ;
}
void SaveGame()
{
	PlayerPrefs.SetInt("SavedInteger", intToSave);
	PlayerPrefs.SetFloat("SavedFloat", floatToSave);
	PlayerPrefs.SetString("SavedString", stringToSave);
	PlayerPrefs.Save();
	Debug.Log("Game data saved!");
}
}
