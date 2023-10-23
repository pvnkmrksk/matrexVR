using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

// Base class for all data loggers
public abstract class DataLogger : MonoBehaviour
{
    // Path to the directory where the log file will be saved
    protected string directoryPath;

    // Path to the log file
    protected string logPath;

    protected string line;  // Or 'public string line;'

    // StreamWriter used to write to the log file
    protected StreamWriter logFile;

    // List of lines to be written to the log file
    protected List<string> bufferedLines;

    // Flags to indicate whether logging and buffering are enabled
    protected bool isLogging;
    protected bool isBuffering;

    // Flag to indicate whether the current line is the first line
    protected bool isFirstLine;

    // ZMQ listener for receiving data
    protected ZmqListener zmq;

    // Called at the start of the scene
    protected virtual void Start()
    {
        // Find the MasterDataLogger in the scene
        MasterDataLogger masterDataLogger = FindObjectOfType<MasterDataLogger>();
        if (masterDataLogger != null)
        {
            // Set the directory path from the MasterDataLogger
            directoryPath = masterDataLogger.directoryPath;

            // Initialize the list of buffered lines
            bufferedLines = new List<string>();

            // Enable logging and buffering
            isLogging = true;
            isBuffering = true;

            // Set isFirstLine to true
            isFirstLine = true;

            // Get the ZmqListener component attached to the same GameObject
            zmq = GetComponent<ZmqListener>();
            if (zmq == null)
            {
                Debug.LogError("ZmqListener component not found in the GameObject. Please attach ZmqListener script to the GameObject.");
            }

            // Initialize the log file and start the routine to flush buffered lines
            InitLog();
            StartCoroutine(FlushBufferedLinesRoutine());
        }
        else
        {
            Debug.LogError("MasterDataLogger not found.");
        }
    }

    // Initializes the log file
    public virtual void InitLog()
    {
        // Get the current date and time
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");

        // Get the name of the GameObject the script is attached to
        string gameObjectName = this.gameObject.name;

        // Set the path to the log file
        logPath = Path.Combine(directoryPath, $"{date}_{time}_{gameObjectName}_.csv.gz");

        // Create the log file and a StreamWriter for it
        logFile = new StreamWriter(
            new GZipStream(File.Create(logPath), System.IO.Compression.CompressionLevel.Optimal)
        );

        // Write the header row without a newline character at the end
        logFile.Write(
            "Current Time,VR,Scene,SensPosX,SensPosY,SensPosZ,SensRotX,SensRotY,SensRotZ,GameObjectPosX,GameObjectPosY,GameObjectPosZ,GameObjectRotX,GameObjectRotY,GameObjectRotZ"
        );

        Debug.Log("Writing data to: " + logPath);
    }

    // Called every frame
    protected virtual void Update()
    {
        // If the ZmqListener is null, return
        if (zmq == null)
            return;

        // Collect the necessary information
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"); // The current time
        string vr = this.gameObject.name; // The name of the GameObject this script is attached to
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; // The name of the current scene

        Vector3 sensPosition = zmq.pose.position; // The position of the ZMQ pose
        Quaternion sensRotation = zmq.pose.rotation; // The rotation of the ZMQ pose

        // The current position and rotation of the GameObject this script is attached to
        Vector3 gameObjectPosition = this.transform.position;
        Quaternion gameObjectRotation = this.transform.rotation;

        // Log the data
        line =
            $"\n{currentTime},{vr},{scene},{sensPosition.x},{sensPosition.y},{sensPosition.z},"
            + $"{sensRotation.eulerAngles.x},{sensRotation.eulerAngles.y},{sensRotation.eulerAngles.z},"
            + $"{gameObjectPosition.x},{gameObjectPosition.y},{gameObjectPosition.z},{gameObjectRotation.eulerAngles.x},"
            + $"{gameObjectRotation.eulerAngles.y},{gameObjectRotation.eulerAngles.z}";

        // LogData(line);
    }

        // Logs a line of data
    protected virtual void LogData(string line, string additionalData = null)
    {
        if (additionalData != null)
        {
            line += "," + additionalData;
        }


            // If buffering is enabled...
        if (isBuffering)
        {
            // If this is the first line, set isFirstLine to false
            if (isFirstLine)
            {
                isFirstLine = false;
            }
            // Otherwise, add the line to the list of buffered lines
            else
            {
                bufferedLines.Add(line);
            }
        }
        // If buffering is not enabled, write the line to the log file
               else
        {
            WriteLogLine(line);
        }
    }

    // Called when the GameObject is destroyed
    protected virtual void OnDestroy()
    {
        // Disable logging and buffering
        isLogging = false;
        isBuffering = false;

        // Dispose of the StreamWriter
        logFile?.Dispose();
    }

    // Flushes the buffered lines to the log file
    async Task FlushBufferedLines()
    {
        // Copy the buffered lines to a new list and clear the buffer
        var linesToWrite = new List<string>(bufferedLines);
        bufferedLines.Clear();

        // Write each line to the log file
        foreach (var line in linesToWrite)
        {
            await logFile.WriteLineAsync(line);
        }

        // Flush the StreamWriter
        await logFile.FlushAsync();
    }

    // Coroutine to flush the buffered lines to the log file
    IEnumerator FlushBufferedLinesRoutine()
    {
        // While logging is enabled...
        while (isLogging)
        {
            // If there are buffered lines, flush them to the log file
            if (bufferedLines.Count > 0)
            {
                yield return FlushBufferedLines();
            }
            // Otherwise, yield null
            else
            {
                yield return null;
            }
        }
    }

    // Writes a line to the log file
    void WriteLogLine(string line)
    {
        // Convert the line to bytes
        byte[] lineBytes = Encoding.UTF8.GetBytes(line);

        // Write the bytes to the log file
        logFile.BaseStream.Write(lineBytes, 0, lineBytes.Length);

        // Write a newline character to the log file
        logFile.BaseStream.WriteByte((byte)'\n');
    }
}