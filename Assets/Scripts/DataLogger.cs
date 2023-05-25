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
    private string logPath;
    private StreamWriter logFile;
    private List<string> bufferedLines;
    private bool isLogging;
    private bool isBuffering;
    private bool isFirstLine;

    private SinusoidalGrating sinusoidalGrating;
    private DrumRotator drumRotator;
    private ZmqListener zmq;

    void Start()
    {
        bufferedLines = new List<string>();
        isLogging = true;
        isBuffering = true;
        isFirstLine = true;

        InitLog();

        StartCoroutine(FlushBufferedLinesRoutine());

        sinusoidalGrating = FindObjectOfType<SinusoidalGrating>();
        drumRotator = FindObjectOfType<DrumRotator>();
        zmq = FindObjectOfType<ZmqListener>();
    }

    void InitLog()
    {
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss");
        string rootDirectoryPath = Application.dataPath;
        string directoryPath = Path.Combine(rootDirectoryPath, "data", date);
        Directory.CreateDirectory(directoryPath);
        logPath = Path.Combine(directoryPath, $"{date}_{time}.csv.gz");
        logFile = new StreamWriter(
            new GZipStream(File.Create(logPath), System.IO.Compression.CompressionLevel.Optimal)
        );

        // Write the header row
        logFile.WriteLine(
            "Current Time,DrumPosX,DrumPosY,DrumPosZ,DrumRotX,DrumRotY,DrumRotZ,SensPosX,SensPosY,SensPosZ,SensRotX,SensRotY,SensRotZ,Frequency,Level,IsPaused,IsStepping"
        );

        Debug.Log("Writing data to: " + logPath);
    }

    void Update()
    {
        // Collect the necessary information
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        GameObject gratingDrum = GameObject.Find("GratingDrum"); // Replace "GratingDrum" with the actual name of the drum object
        Vector3 drumPosition = gratingDrum.transform.position;
        Quaternion drumRotation = gratingDrum.transform.rotation;

        Vector3 sensPosition = zmq.pose.position;
        Quaternion sensRotation = zmq.pose.rotation;

        // Vector3 sensPosition = FindObjectOfType<ZmqListener>().positionToUpdate;
        // Quaternion sensRotation = FindObjectOfType<ZmqListener>().rotationToUpdate;

        // Get frequency and level from SinusoidalGrating
        float frequency = sinusoidalGrating.frequency;
        float level = sinusoidalGrating.level;

        // Get isPaused and isStepping from DrumRotator
        bool isPaused = drumRotator.isPaused;
        bool isStepping = drumRotator.isStepping;

        // Log the data
        string line =
            $"{currentTime},{drumPosition.x},{drumPosition.y},{drumPosition.z},{drumRotation.eulerAngles.x},"
            + $"{drumRotation.eulerAngles.y},{drumRotation.eulerAngles.z},{sensPosition.x},{sensPosition.y},"
            + $"{sensPosition.z},{sensRotation.eulerAngles.x},{sensRotation.eulerAngles.y},{sensRotation.eulerAngles.z},"
            + $"{frequency},{level},{isPaused},{isStepping}";

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
