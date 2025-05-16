using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Base class for all data loggers in the system.
/// 
/// This class provides functionality to log data to CSV files, including:
/// - Standard data columns like time, position, and rotation
/// - Optional ZMQ data columns for sensor information
/// - Support for additional custom columns defined by derived classes
/// 
/// To create a custom logger:
/// 1. Create a new class that inherits from DataLogger
/// 2. Call AddColumns() in Start() to define your custom columns
/// 3. Override CollectAdditionalData() to populate your custom data
/// </summary>
public class DataLogger : MonoBehaviour
{
    // Path to the directory where the log file will be saved
    protected string directoryPath;

    /// <summary>
    /// Path to the log file, accessible by derived classes
    /// </summary>
    protected string logPath { get; private set; }

    /// <summary>
    /// Current line of data being built before writing to log
    /// </summary>
    protected string line;

    /// <summary>
    /// StreamWriter used to write to the log file
    /// </summary>
    protected StreamWriter logFile;

    /// <summary>
    /// Buffer for lines to be written to the log file
    /// </summary>
    protected List<string> bufferedLines;

    /// <summary>
    /// Flags controlling the logging behavior
    /// </summary>
    protected bool isLogging;
    protected bool isBuffering;
    protected bool isFirstLine;
    protected ZmqListener zmq;

    /// <summary>
    /// Flag to include ZMQ sensor data in the log. Set to false to disable.
    /// </summary>
    public bool includeZmqData = true;

    /// <summary>
    /// Reference to the main controller for accessing scene information
    /// </summary>
    protected MainController mainController;

    /// <summary>
    /// Lists of headers and values for additional data columns
    /// </summary>
    protected List<string> additionalHeaders = new List<string>();
    protected Dictionary<string, object> additionalData = new Dictionary<string, object>();
    private int    stepIndex = -1;
    private string stepName  = "";

    public void SetStep(int index, string name)
    {
        stepIndex = index;
        stepName  = name;

        // push into the per-frame dictionary so PrepareLogData() writes them
        SetData("stepIndex", index);
        SetData("stepName",  name);
    }

    /// <summary>
    /// Standard columns included in all log files
    /// </summary>
    private readonly string[] baseColumns = new string[] {
        "Current Time", "VR", "Scene", "CurrentSequenceScene",
        "ConfigFile", "CurrentTrial", "CurrentStep",
        "GameObjectPosX", "GameObjectPosY", "GameObjectPosZ",
        "GameObjectRotX", "GameObjectRotY", "GameObjectRotZ"
    };


    /// <summary>
    /// ZMQ sensor data columns (only included if includeZmqData is true)
    /// </summary>
    private readonly string[] zmqColumns = new string[] {
        "SensPosX", "SensPosY", "SensPosZ",
        "SensRotX", "SensRotY", "SensRotZ"
    };

    /// <summary>
    /// Adds one or more column headers to the log file.
    /// Call this method in Start() before base.Start() to ensure headers are included.
    /// </summary>
    /// <param name="headers">One or more header names to add</param>
    /// <example>
    /// AddColumns("Temperature", "Humidity", "Pressure");
    /// </example>

    public void AddColumns(params string[] headers)
    {
        foreach (string header in headers)
        {
            if (!additionalHeaders.Contains(header))
            {
                additionalHeaders.Add(header);
            }
        }
    }

    /// <summary>
    /// Sets a value for a column in the current log line.
    /// Only works for columns that were previously added with AddColumns().
    /// </summary>
    /// <param name="key">The column header name</param>
    /// <param name="value">The value to log in this column</param>
    /// <example>
    /// SetData("Temperature", 23.5f);
    /// </example>
    public void SetData(string key, object value)
    {
        additionalData[key] = value;
    }

    /// <summary>
    /// Sets multiple column values at once from a dictionary.
    /// Only values for columns previously added with AddColumns() will be used.
    /// </summary>
    /// <param name="data">Dictionary mapping column names to values</param>
    /// <example>
    /// var data = new Dictionary<string, object> {
    ///     { "Temperature", 23.5f },
    ///     { "Humidity", 60 }
    /// };
    /// SetData(data);
    /// </example>
    public void SetData(Dictionary<string, object> data)
    {
        foreach (var kvp in data)
        {
            additionalData[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Initializes the logger, finds dependencies, and sets up the log file.
    /// When overriding in derived classes, call base.Start() after adding your columns.
    /// </summary>
    protected virtual void Start()
    {
        // Find dependencies
        mainController = FindObjectOfType<MainController>();
        zmq = GetComponent<ZmqListener>();

        // Find master logger to get directory path
        MasterDataLogger masterDataLogger = FindObjectOfType<MasterDataLogger>();
        if (masterDataLogger != null)
        {
            directoryPath = masterDataLogger.directoryPath;
            bufferedLines = new List<string>();
            AddColumns("stepIndex", "stepName"); 

            // Enable logging
            isLogging = true;
            isBuffering = true;
            isFirstLine = true;

            // Initialize log file
            InitLog();
            StartCoroutine(FlushBufferedLinesRoutine());
        }
        else
        {
            Debug.LogError("MasterDataLogger not found, logging disabled");
        }
    }

    /// <summary>
    /// Initializes the log file, creates the CSV file and writes the header row.
    /// This is called automatically by Start(), but can be called manually to reset the log.
    /// </summary>
    public virtual void InitLog()
    {
        // Get the timestamp from the MasterDataLogger
        string timestamp = FindObjectOfType<MasterDataLogger>().timestamp;
        string gameObjectName = this.gameObject.name;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Set log path
        logPath = Path.Combine(directoryPath, $"{timestamp}_{sceneName}_{gameObjectName}_.csv");

        // Open/create log file
        FileStream fileStream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        logFile = new StreamWriter(fileStream);

        // Write headers if new file
        if (fileStream.Length == 0)
        {
            // Write base columns
            string headerLine = string.Join(",", baseColumns);

            // Add ZMQ columns if enabled
            if (includeZmqData)
            {
                headerLine += "," + string.Join(",", zmqColumns);
            }

            // Add additional columns
            if (additionalHeaders.Count > 0)
            {
                headerLine += "," + string.Join(",", additionalHeaders);
            }

            logFile.Write(headerLine);
            logFile.Flush();
        }

        Debug.Log($"Logging to: {logPath}");
    }

    /// <summary>
    /// Public method to trigger a log update manually from outside this class.
    /// </summary>
    public void UpdateLogger()
    {
        Update();
    }

    /// <summary>
    /// Called every frame to log the current data.
    /// </summary>
    protected virtual void Update()
    {
        PrepareLogData();
        LogData(line);
    }

    /// <summary>
    /// Prepares a line of data to be logged by gathering all information.
    /// The standard flow is:
    /// 1. Clear any previous additional data
    /// 2. Build the base data (time, position, etc.)
    /// 3. Call CollectAdditionalData() to let derived classes add their data
    /// 4. Format everything into the line variable
    /// </summary>
    protected virtual void PrepareLogData()
    {
        // Clear previous data
        additionalData.Clear();

        // Build base data
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string vr = this.gameObject.name;
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Vector3 position = this.transform.position;
        Vector3 rotation = this.transform.rotation.eulerAngles;

        // Get sequence data
        string currentSequenceScene = "Unknown";
        string configFileName = "";
        int currentTrial = 0;
        int currentStep = 0;

        if (mainController != null)
        {
            SequenceStep step = mainController.GetCurrentSequenceStep();
            if (step != null)
            {
                currentSequenceScene = step.sceneName;
                if (step.parameters != null && step.parameters.ContainsKey("configFile"))
                {
                    configFileName = step.parameters["configFile"].ToString();
                }
            }
            currentTrial = mainController.currentTrial;
            currentStep = mainController.currentStep;
        }

        // Create base line
        line = $"\n{currentTime},{vr},{scene},{currentSequenceScene},{configFileName},{currentTrial},{currentStep},{position.x},{position.y},{position.z},{rotation.x},{rotation.y},{rotation.z}";

        // Add ZMQ data
        if (includeZmqData && zmq != null)
        {
            Vector3 sensPos = zmq.pose.position;
            Vector3 sensRot = zmq.pose.rotation.eulerAngles;
            line += $",{sensPos.x},{sensPos.y},{sensPos.z},{sensRot.x},{sensRot.y},{sensRot.z}";
        }

        // Allow subclasses to add additional data
        CollectAdditionalData();

        // Add additional column data
        foreach (var header in additionalHeaders)
        {
            if (additionalData.TryGetValue(header, out object value))
            {
                line += $",{value}";
            }
            else
            {
                line += ",";
            }
        }
    }

    /// <summary>
    /// Override this method in derived classes to add custom data to the log.
    /// Use SetData() to add values for columns that were added with AddColumns().
    /// This method is called automatically by PrepareLogData().
    /// </summary>
    /// <example>
    /// protected override void CollectAdditionalData()
    /// {
    ///     // Add custom data
    ///     SetData("Temperature", GetComponent<TemperatureSensor>().temperature);
    ///     SetData("Humidity", GetComponent<HumiditySensor>().humidity);
    /// }
    /// </example>
    protected virtual void CollectAdditionalData()
    {
        // Base implementation does nothing
    }

    /// <summary>
    /// Logs a line of data to the file or buffer.
    /// </summary>
    /// <param name="line">The line to log</param>
    protected virtual void LogData(string line)
    {
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

    /// <summary>
    /// Called when the GameObject is destroyed.
    /// Cleans up resources and flushes remaining data.
    /// </summary>
    protected virtual void OnDestroy()
    {
        isLogging = false;
        isBuffering = false;
        logFile?.Dispose();
    }

    /// <summary>
    /// Asynchronously flushes buffered lines to the log file.
    /// </summary>
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

    /// <summary>
    /// Coroutine that periodically flushes buffered lines to the log file.
    /// </summary>
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

    /// <summary>
    /// Writes a line to the log file.
    /// </summary>
    /// <param name="line">The line to write</param>
    void WriteLogLine(string line)
    {
        byte[] lineBytes = Encoding.UTF8.GetBytes(line);
        logFile.BaseStream.Write(lineBytes, 0, lineBytes.Length);
    }
}
