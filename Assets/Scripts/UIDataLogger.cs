using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TMPro;  // Add this at the top of your script


public class UIDataLogger : MonoBehaviour
{
    public InputField experimenterNameInput;
    public List<InputField> ageInputs;
    public List<InputField> starvedSinceInputs;
    public List<Dropdown> sexDropdowns;
    public List<InputField> flyIDInputs;
    public InputField commentsInput;
    public Button saveButton;
    public Button newFliesButton;


    private string directoryPath; // This will store the path to save data files

    private void Start()
    {
        // Find the MasterDataLogger and get the directory path
        MasterDataLogger masterDataLogger = FindObjectOfType<MasterDataLogger>();
        if (masterDataLogger != null)
        {
            directoryPath = masterDataLogger.directoryPath;
            //add debug log to check the directory path
            Debug.Log("UI Directory Path: " + directoryPath);
        }
        else
        {
            Debug.LogError("MasterDataLogger not found in the scene. Data will not be saved.");
            // Optionally set a default path or disable data saving
            directoryPath = Application.persistentDataPath; // A default fallback path
        }

        newFliesButton.onClick.AddListener(GenerateNewFlyIDs);
        saveButton.onClick.AddListener(SaveData);
    }

    public void GenerateNewFlyIDs()
    {
        int maxFlyID = 0;
        foreach (var input in flyIDInputs)
        {
            if (int.TryParse(input.text, out int currentID) && currentID > maxFlyID)
            {
                maxFlyID = currentID;
            }
        }
        
        for (int i = 0; i < flyIDInputs.Count; i++)
        {
            flyIDInputs[i].text = (maxFlyID + 1 + i).ToString();
        }
    }

    public void SaveData()
    {
        var flyData = new Dictionary<string, object>();
        flyData.Add("ExperimenterName", experimenterNameInput.text);
        List<object> flies = new List<object>();

        for (int i = 0; i < flyIDInputs.Count; i++)
        {
            flies.Add(new 
            {  
                VR = "VR" + (i + 1),  // Add a VR number dynamically based on the index
                AgeDays = ageInputs[i].text,
                StarvedSinceHours = starvedSinceInputs[i].text,
                Sex = sexDropdowns[i].options[sexDropdowns[i].value].text,
                FlyID = flyIDInputs[i].text
            });
        }

        flyData.Add("Flies", flies);
        flyData.Add("Comments", commentsInput.text);

        string json = JsonConvert.SerializeObject(flyData, Formatting.Indented);
        WriteJsonToFile(json);
    }

    private void WriteJsonToFile(string jsonContent)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            //add debug log to check if the directory is created
            Debug.Log("Directory Created from UI script: " + directoryPath);
        }
        string dateTimePrefix = Path.GetFileName(directoryPath);

        // Create the filename with the date-time prefix
        string fileName = dateTimePrefix + "_FlyMetaData.json";
        string filePath = Path.Combine(directoryPath, fileName);
        File.WriteAllText(filePath, jsonContent);
    }
}
