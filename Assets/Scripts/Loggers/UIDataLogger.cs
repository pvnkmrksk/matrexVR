using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;

public class UIDataLogger : MonoBehaviour
{
    public InputField experimenterNameInput;
    public List<InputField> ageInputs;
    public List<InputField> starvedSinceInputs;
    public List<Dropdown> sexDropdowns;
    [FormerlySerializedAs("flyIDInputs")] public List<InputField> animalIDInputs;
    public List<InputField> flyIDInputs { get => animalIDInputs; set => animalIDInputs = value; }
    public InputField commentsInput;
    public Button saveButton;
    [FormerlySerializedAs("newFliesButton")] public Button newAnimalsButton;
    public Button newFliesButton { get => newAnimalsButton; set => newAnimalsButton = value; }
    public Button reloadButton;  // New button for reloading data

    private AnimalData animalsData; // This will store the current session's animal metadata
    private string directoryPath; // This will store the path to save data files
    private string backupDirectoryPath;

    private void Start()
    {
        // Populate before loading saved metadata. A fresh or missing value is unspecified.
        foreach (Dropdown dropdown in sexDropdowns)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string> { "Unspecified", "Female", "Male" });
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
        }
        MasterDataLogger masterDataLogger = FindObjectOfType<MasterDataLogger>();
        if (masterDataLogger != null)
        {
            directoryPath = masterDataLogger.directoryPath;
            // Set backupDirectoryPath to be a subdirectory or the same directory
            backupDirectoryPath = Path.Combine(RuntimePaths.RunDataRoot, "Backup");
            Debug.Log("UI Directory Path: " + directoryPath);
            Debug.Log("Backup Directory Path: " + backupDirectoryPath);
        }
        else
        {
            Debug.LogError("MasterDataLogger not found in the scene. Data will not be saved.");
            enabled=false;
            return;
        }
        //check if backup path exists
        if (!Directory.Exists(backupDirectoryPath))
        {
            Directory.CreateDirectory(backupDirectoryPath);
            Debug.Log("Backup Directory created: " + backupDirectoryPath);
        }
        saveButton.onClick.AddListener(SaveData);
        reloadButton.onClick.AddListener(LoadLastSessionData);

        LoadLastSessionData(); // Call to load data from the last session
    }


    public void SaveData()
    {
        AnimalData animalData = new AnimalData
        {
            ExperimentName = animalsData?.ExperimentName,
            Extensions = animalsData?.Extensions,
            ExperimenterName = experimenterNameInput.text,
            Comments = commentsInput.text,
            Animals = new List<AnimalMetadata>(),
            UsedAnimalIDs = animalsData != null ? new List<string>(animalsData.UsedAnimalIDs ?? new List<string>()) : new List<string>()
        };

        for (int i = 0; i < animalIDInputs.Count; i++)
        {
            var previous = animalsData?.Animals?.FirstOrDefault(f => f.VR == "VR" + (i + 1));
            AnimalMetadata newAnimal = new AnimalMetadata
            {
                Comments = previous?.Comments,
                SpeciesName = MainController.Instance?.GetSystemConfig("VR" + (i + 1))?.speciesName ?? previous?.SpeciesName,
                Extensions = previous?.Extensions,
                VR = "VR" + (i + 1),
                AgeDays = ageInputs[i].text,
                StarvedSinceHours = starvedSinceInputs[i].text,
                Sex = sexDropdowns[i].options[sexDropdowns[i].value].text,
                AnimalID = animalIDInputs[i].text
            };
            animalData.Animals.Add(newAnimal);
            // Simply store the string ID
            // This won't throw a format exception, even if the user typed letters
            if (!string.IsNullOrEmpty(animalIDInputs[i].text))
            {
                animalData.UsedAnimalIDs.Add(animalIDInputs[i].text);
            }
        }

        string json = JsonConvert.SerializeObject(animalData, Formatting.Indented);
        WriteJsonToFile(json);
    }


    private void SaveNormalData(string jsonContent)
    {
        string fileName = GenerateFileName();
        try
        {
            WriteToFile(directoryPath, fileName, jsonContent);
            WriteToFile(directoryPath, "AnimalMetadata.json", jsonContent);
            WriteToFile(directoryPath, "FlyMetaData.json", jsonContent);
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
            WriteToFile(backupDirectoryPath, "AnimalMetadata.json", jsonContent);
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
        return dateTimePrefix + "_AnimalMetadata.json";
    }

    // Usage
    private void WriteJsonToFile(string jsonContent)
    {
        SaveNormalData(jsonContent);
        SaveBackupData(jsonContent);
    }

    public void LoadLastSessionData()
    {
        string filePath = Path.Combine(backupDirectoryPath, "AnimalMetadata.json");
        if (!File.Exists(filePath)) filePath = Path.Combine(backupDirectoryPath, "FlyMetaData.json");
        if (File.Exists(filePath))
        {
            string jsonContent = File.ReadAllText(filePath);
            DeserializeAndSetData(jsonContent);
            Debug.Log("Data loaded from last session.");
        }
        else
        {
            Debug.Log("No previous metadata found; using the form defaults.");
        }
    }

     private void DeserializeAndSetData(string jsonData)
    {
        AnimalData animalData = JsonConvert.DeserializeObject<AnimalData>(jsonData);

        experimenterNameInput.text = animalData.ExperimenterName;
        commentsInput.text = animalData.Comments;

        animalsData = animalData;  // Store the deserialized animal metadata, including used IDs

        for (int i = 0; i < animalData.Animals.Count && i < animalIDInputs.Count && i < ageInputs.Count && i < starvedSinceInputs.Count && i < sexDropdowns.Count; i++)
        {
            AnimalMetadata animal = animalData.Animals[i];
            ageInputs[i].text = animal.AgeDays;
            starvedSinceInputs[i].text = animal.StarvedSinceHours;
            int sexIndex = sexDropdowns[i].options.FindIndex(option => string.Equals(option.text, animal.Sex, StringComparison.OrdinalIgnoreCase));
            sexDropdowns[i].SetValueWithoutNotify(Math.Max(0, sexIndex));
            sexDropdowns[i].RefreshShownValue();
            animalIDInputs[i].text = animal.AnimalID;
        }
    }    
}
