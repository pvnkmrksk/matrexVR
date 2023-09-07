using System.IO;
using System.IO.Compression;
using UnityEngine;

public class MasterDataLogger : MonoBehaviour
{
    public string directoryPath { get; private set; }

    void Start()
    {
        Debug.Log("Start() is executed");
        string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
        directoryPath = Application.dataPath + "/RunData/" + timestamp;
        Directory.CreateDirectory(directoryPath);
    }

    public void ZipDataFolder()
    {
        string zipPath = Application.dataPath + "/RunData/" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip";
        ZipFile.CreateFromDirectory(directoryPath, zipPath);
    }
}
