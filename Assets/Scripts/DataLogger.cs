public abstract class DataLogger : MonoBehaviour
{
    protected string directoryPath;
    protected string logPath;
    protected StreamWriter logFile;
    protected List<string> bufferedLines;
    protected bool isLogging;
    protected bool isBuffering;
    protected bool isFirstLine;
    protected ZmqListener zmq;

    protected virtual void Start()
    {
        MasterDataLogger masterDataLogger = FindObjectOfType<MasterDataLogger>();
        if (masterDataLogger != null)
        {
            directoryPath = masterDataLogger.directoryPath;
            bufferedLines = new List<string>();
            isLogging = true;
            isBuffering = true;
            isFirstLine = true;

            zmq = GetComponent<ZmqListener>();
            if (zmq == null)
            {
                Debug.LogError("ZmqListener component not found in the GameObject. Please attach ZmqListener script to the GameObject.");
            }

            InitLog();
            StartCoroutine(FlushBufferedLinesRoutine());
        }
        else
        {
            Debug.LogError("MasterDataLogger not found.");
        }
    }
    protected virtual void InitLog()
    {
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");
        string gameObjectName = this.gameObject.name; // Get the name of the GameObject the script is attached to
        logPath = Path.Combine(directoryPath, $"{date}_{time}_{gameObjectName}_.csv.gz");
        
        logFile = new StreamWriter(
            new GZipStream(File.Create(logPath), System.IO.Compression.CompressionLevel.Optimal)
        );

        // Write the header row without a newline character at the end
        logFile.Write(
            "Current Time,VR,Scene,SensPosX,SensPosY,SensPosZ,SensRotX,SensRotY,SensRotZ,GameObjectPosX,GameObjectPosY,GameObjectPosZ,GameObjectRotX,GameObjectRotY,GameObjectRotZ"
        );

        Debug.Log("Writing data to: " + logPath);
    }

protected virtual void Update()
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
    Vector3 gameObjectPosition = this.transform.position;
    Quaternion gameObjectRotation = this.transform.rotation;

    // Log the data
    string line =
        $"\n{currentTime},{vr},{scene},{sensPosition.x},{sensPosition.y},{sensPosition.z},"
        + $"{sensRotation.eulerAngles.x},{sensRotation.eulerAngles.y},{sensRotation.eulerAngles.z},"
        + $"{gameObjectPosition.x},{gameObjectPosition.y},{gameObjectPosition.z},{gameObjectRotation.eulerAngles.x},"
        + $"{gameObjectRotation.eulerAngles.y},{gameObjectRotation.eulerAngles.z}";

    LogData(line);
}

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

    protected virtual void OnDestroy()
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