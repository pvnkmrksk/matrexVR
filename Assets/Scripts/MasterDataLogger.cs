using System.Collections.Generic;
using UnityEngine;

public class MasterDataLogger : MonoBehaviour
{
    public string directoryPath { get; private set; }
    private List<DataLogger> dataLoggers;

    void Start()
    {
        Debug.Log("Start() is executed");
        string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
        directoryPath = Application.dataPath + "/RunData/" + timestamp;
        Directory.CreateDirectory(directoryPath);

        // Find all DataLogger instances in the scene
        dataLoggers = new List<DataLogger>();
        foreach (var logger in FindObjectsOfType<DataLogger>())
        {
            if (logger.gameObject.scene == gameObject.scene)
            {
                dataLoggers.Add(logger);
            }
        }
        // Initialize all DataLogger instances
        foreach (var logger in dataLoggers)
        {
            logger.InitLog();
        }
    }

    public void ZipDataFolder()
    {
        string zipPath = Application.dataPath + "/RunData/" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip";
        ZipFile.CreateFromDirectory(directoryPath, zipPath);
    }
}
