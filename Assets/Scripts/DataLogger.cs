using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

// Base class for all data loggers
public class DataLogger : MonoBehaviour
{
    // Path to the directory where the log file will be saved
    protected string directoryPath;

    // Path to the log file, with get method to allow access from derived classes

    protected string logPath { get; private set; }

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

    // Flag to indicate whether to include ZMQ data in the log
    public bool includeZmqData = true;

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
                Logger.Log("ZmqListener component not found in the GameObject. Please attach ZmqListener script to the GameObject.", 1);
            }

            // Initialize the log file and start the routine to flush buffered lines
            InitLog();
            StartCoroutine(FlushBufferedLinesRoutine());
        }
        else
        {
            Logger.Log("MasterDataLogger not found.");
        }
    }

    // Initializes the log file
    public virtual void InitLog()
    {
        // Get the timestamp from the MasterDataLogger
        string timestamp = FindObjectOfType<MasterDataLogger>().timestamp;

        // Get the name of the GameObject the script is attached to
        string gameObjectName = this.gameObject.name;

        // get the scene name
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Set the path to the log file
        logPath = Path.Combine(directoryPath, $"{timestamp}_{sceneName}_{gameObjectName}_.csv.gz");

        // Create the log file and a StreamWriter for it
        logFile = new StreamWriter(
            new GZipStream(File.Create(logPath), System.IO.Compression.CompressionLevel.Optimal)
        );


        // Write the header to the log file depending on whether ZMQ data is included
        if (includeZmqData)
        {
            logFile.Write(
                "Current Time,VR,Scene,SensPosX,SensPosY,SensPosZ,SensRotX,SensRotY,SensRotZ,GameObjectPosX,GameObjectPosY,GameObjectPosZ,GameObjectRotX,GameObjectRotY,GameObjectRotZ"
            );
        }
        else
        {
            logFile.Write(
                "Current Time,VR,Scene,GameObjectPosX,GameObjectPosY,GameObjectPosZ,GameObjectRotX,GameObjectRotY,GameObjectRotZ"
            );
        }



        Logger.Log("Writing data to: " + logPath);
    }

    public void UpdateLogger()


    {
        /* 
            Making the Update() method public would indeed solve the immediate problem, but it's generally not a good practice. Here's why:

            1. Encapsulation: In object-oriented programming, it's a good practice to hide the internal workings of a class and only expose what's necessary. This is known as encapsulation. The Update() method is part of Unity's MonoBehaviour lifecycle and is intended to be used internally by the class itself. By keeping it protected, you're adhering to the principle of encapsulation.

            2. Preventing unintended usage: If you make the Update() method public, it can be called from anywhere. This could lead to unintended behavior if, for example, another script calls it at the wrong time or more often than expected.

            3. Maintaining Unity's conventions: Unity's MonoBehaviour methods like Start(), Update(), Awake(), etc., are typically kept private or protected. This is a convention in Unity development, and following it makes your code easier to understand for other Unity developers.

            By creating a separate public method (UpdateLogger()) that calls Update(), you're providing a clear interface for other scripts to interact with, while keeping the internal workings of your class hidden. This is a cleaner and more elegant solution that adheres to good programming practices.
        */
        Update();
    }
    // Called every frame
    protected virtual void Update()
    {


        // Prepare and log the data
        PrepareLogData();
        LogData(line);
    }

    // Prepares a line of data to be logged
    protected virtual void PrepareLogData()
    {
        // Collect the necessary information
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"); // The current time
        string vr = this.gameObject.name; // The name of the GameObject this script is attached to
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; // The name of the current scene

        // The current position and rotation of the GameObject this script is attached to
        Vector3 gameObjectPosition = this.transform.position;
        Quaternion gameObjectRotation = this.transform.rotation;

        // Prepare the data
        line = $"\n{currentTime},{vr},{scene},{gameObjectPosition.x},{gameObjectPosition.y},{gameObjectPosition.z},{gameObjectRotation.eulerAngles.x},{gameObjectRotation.eulerAngles.y},{gameObjectRotation.eulerAngles.z}";

        // Add ZMQ data if includeZmqData is true
        if (includeZmqData)
        {
            Vector3 sensPosition = zmq.pose.position; // The position of the ZMQ pose
            Quaternion sensRotation = zmq.pose.rotation; // The rotation of the ZMQ pose

            line += $",{sensPosition.x},{sensPosition.y},{sensPosition.z},{sensRotation.eulerAngles.x},{sensRotation.eulerAngles.y},{sensRotation.eulerAngles.z}";
        }
    }

    // Logs a line of data
    protected virtual void LogData(string line)
    {
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