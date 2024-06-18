using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System;

public class FlyData
{
    public string ExperimenterName { get; set; }
    public List<Fly> Flies { get; set; }
    public string Comments { get; set; }
}

public class Fly
{
    public string VR { get; set; }
    public string AgeDays { get; set; }
    public string StarvedSinceHours { get; set; }
    public string Sex { get; set; }
    public string FlyID { get; set; }
}


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
    private string backupDirectoryPath;

    private void Start()
    {
        MasterDataLogger masterDataLogger = FindObjectOfType<MasterDataLogger>();
        if (masterDataLogger != null)
        {
            directoryPath = masterDataLogger.directoryPath;
            // Set backupDirectoryPath to be a subdirectory or the same directory
            backupDirectoryPath = Application.dataPath + "/RunData/Backup"; 
            Debug.Log("UI Directory Path: " + directoryPath);
            Debug.Log("Backup Directory Path: " + backupDirectoryPath);
        }
        else
        {
            Debug.LogError("MasterDataLogger not found in the scene. Data will not be saved.");
            //directoryPath = Application.persistentDataPath;  // Use a default path
            //backupDirectoryPath = Path.Combine(Application.persistentDataPath, "Backup");
        }
        //check if backup path exists
        if (!Directory.Exists(backupDirectoryPath))
        {
            Directory.CreateDirectory(backupDirectoryPath);
            Debug.Log("Backup Directory created: " + backupDirectoryPath);
        }
        newFliesButton.onClick.AddListener(GenerateNewFlyIDs);
        saveButton.onClick.AddListener(SaveData);

        LoadLastSessionData(); // Call to load data from the last session
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

    private void SaveNormalData(string jsonContent)
    {
        string fileName = GenerateFileName();
        try
        {
            WriteToFile(directoryPath, fileName, jsonContent);
            Debug.Log("Normal data saved to file: " + Path.Combine(directoryPath, fileName));
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to write normal data: " + ex.Message);
        }
    }

    private void SaveBackupData(string jsonContent)
    {
        string fileName = "FlyMetaData.json";
        try
        {
            WriteToFile(backupDirectoryPath, fileName, jsonContent);
            Debug.Log("Backup data saved to file: " + Path.Combine(backupDirectoryPath, fileName));
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to write backup data: " + ex.Message);
        }
    }

    private void WriteToFile(string path, string fileName, string content)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            Debug.Log("Directory created: " + path);
        }
        string filePath = Path.Combine(path, fileName);
        File.WriteAllText(filePath, content);
        Debug.Log("Data saved to file: " + filePath);
    }

    private string GenerateFileName()
    {
        // Generates a filename with a date-time prefix
        string dateTimePrefix = DateTime.Now.ToString("yyyyMMddHHmmss");
        return dateTimePrefix + "_FlyMetaData.json";
    }

    // Usage
    private void WriteJsonToFile(string jsonContent)
    {
        SaveNormalData(jsonContent);
        SaveBackupData(jsonContent);
    }

    private void LoadLastSessionData()
    {
        string filePath = Path.Combine(backupDirectoryPath, "FlyMetaData.json");
        if (File.Exists(filePath))
        {
            string jsonContent = File.ReadAllText(filePath);
            DeserializeAndSetData(jsonContent);
            Debug.Log("Data loaded from last session.");
        }
        else
        {
            Debug.LogError("No backup data file found.");
        }
    }

     private void DeserializeAndSetData(string jsonData)
    {
        var flyData = JsonConvert.DeserializeObject<FlyData>(jsonData);

        experimenterNameInput.text = flyData.ExperimenterName;

        for (int i = 0; i < flyData.Flies.Count; i++)
        {
            var fly = flyData.Flies[i];
            ageInputs[i].text = fly.AgeDays;
            starvedSinceInputs[i].text = fly.StarvedSinceHours;
            sexDropdowns[i].value = sexDropdowns[i].options.FindIndex(option => option.text == fly.Sex);
            flyIDInputs[i].text = fly.FlyID;
        }
        commentsInput.text = flyData.Comments;
    }

    
}
