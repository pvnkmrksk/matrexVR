using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

public class DataLogger : MonoBehaviour
{
    private string directoryPath;
    private string logPath;
    private StreamWriter logFile;
    private List<string> bufferedLines;
    private bool isLogging;
    private bool isBuffering;
    private bool isFirstLine;

    private ZmqListener zmq;

    void Start()
    {
        Debug.Log("Start() is executed");
        MasterDataLogger masterDataLogger = FindObjectOfType<MasterDataLogger>();
        if (masterDataLogger != null)
        {
            Debug.Log("Found MasterDataLogger");
            directoryPath = masterDataLogger.directoryPath;
            Debug.Log("directoryPath:" + directoryPath);
            bufferedLines = new List<string>();
            isLogging = true;
            isBuffering = true;
            isFirstLine = true;

            InitLog();

            StartCoroutine(FlushBufferedLinesRoutine());

            zmq = GetComponent<ZmqListener>();
            if (zmq == null)
            {
                Debug.LogError("ZmqListener component not found in the GameObject. Please attach ZmqListener script to the GameObject.");
            }
        }
        else
        {
            Debug.LogError("MasterDataLogger not found.");
        }
    }
    void InitLog()
    {
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");
        string gameObjectName = this.gameObject.name; // Get the name of the GameObject the script is attached to
        string rootDirectoryPath = Application.dataPath;
        Debug.Log("init directory path:" + directoryPath);
        // Add GameObject name in the log filename
        logPath = Path.Combine(directoryPath, $"{date}_{time}_{gameObjectName}_.csv.gz");
        
        logFile = new StreamWriter(
            new GZipStream(File.Create(logPath), System.IO.Compression.CompressionLevel.Optimal)
        );

        // Write the header row
        logFile.WriteLine(
            "Current Time,VR,Scene,SensPosX,SensPosY,SensPosZ,SensRotX,SensRotY,SensRotZ,InsectPosX,InsectPosY,InsectPosZ,InsectRotX,InsectRotY,InsectRotZ"
        );

        Debug.Log("Writing data to: " + logPath);
    }

    void Update()
    {
        if (zmq == null)
            return;

        // Collect the necessary information
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string vr = this.gameObject.name; // The name of the GameObject this script is attached to
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; // The name of the current scene

        Vector3 sensPosition = zmq.pose.position;
        Quaternion sensRotation = zmq.pose.rotation;

        // The current position and rotation of the GameObject this script is attached to
        Vector3 insectPosition = this.transform.position;
        Quaternion insectRotation = this.transform.rotation;

        // Log the data
        string line =
            $"{currentTime},{vr},{scene},{sensPosition.x},{sensPosition.y},{sensPosition.z},"
            + $"{sensRotation.eulerAngles.x},{sensRotation.eulerAngles.y},{sensRotation.eulerAngles.z},"
            + $"{insectPosition.x},{insectPosition.y},{insectPosition.z},{insectRotation.eulerAngles.x},"
            + $"{insectRotation.eulerAngles.y},{insectRotation.eulerAngles.z}";

        if (isBuffering)
        {
            if (isFirstLine)
            {
                isFirstLine = false;
            }
            else
            {
                bufferedLines.Add(line);
            }
        }
        else
        {
            WriteLogLine(line);
        }
    }
        void OnDestroy()
        {
            isLogging = false;
            isBuffering = false;
            logFile?.Dispose();
        }

        async Task FlushBufferedLines()
        {
            var linesToWrite = new List<string>(bufferedLines);
            bufferedLines.Clear();

            foreach (var line in linesToWrite)
            {
                await logFile.WriteLineAsync(line);
            }

            await logFile.FlushAsync();
        }

        IEnumerator FlushBufferedLinesRoutine()
        {
            while (isLogging)
            {
                if (bufferedLines.Count > 0)
                {
                    yield return FlushBufferedLines();
                }
                else
                {
                    yield return null;
                }
            }
        }

        void WriteLogLine(string line)
        {
            byte[] lineBytes = Encoding.UTF8.GetBytes(line);
            logFile.BaseStream.Write(lineBytes, 0, lineBytes.Length);
            logFile.BaseStream.WriteByte((byte)'\n');
        }
}
