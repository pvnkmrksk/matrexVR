using UnityEngine;
using System.IO;           // For file I/O if you want to save data
using System.Collections.Generic;  // For List<T>

public class ColorDrift : MonoBehaviour
{
    [Header("OU Parameters")]
    public float meanBlueA = 50f;   // First mean
    public float meanBlueB = 200f;  // Second mean
    public float switchInterval = 5f; // Switch means every 5 seconds

    public float theta = 1f;       // OU pull strength
    public float sigma = 5f;       // OU noise scale

    [Header("Debug / Logging")]
    [SerializeField] private float currentBlue;  // Exposed in Inspector
    public bool logDataToFile = false;           // Set true to save data to file
    public string fileName = "BlueTimeseries.txt";

    // Internals
    private float timer;
    private bool useMeanA = true;
    private List<string> dataBuffer;  // To batch up data before writing
    
    // -- Add public read-only properties so external scripts can read them:
    public float CurrentBlue
    {
        get { return currentBlue; }
    }

    public bool IsUsingMeanA
    {
        get { return useMeanA; }
    }

    void Start()
    {
        currentBlue = meanBlueA;
        timer = 0f;

        // If logging data, create a new list.  
        // We'll write to disk in OnDisable() or OnApplicationQuit().
        if (logDataToFile)
        {
            dataBuffer = new List<string>();
            // Optional: write a header line
            dataBuffer.Add("Time,CurrentBlue");
        }
    }
    

    void Update()
    {
        // 1) Handle switching means
        timer += Time.deltaTime;
        if (timer >= switchInterval)
        {
            timer = 0f;
            useMeanA = !useMeanA;  // Toggle between A and B
        }
        
        float targetMean = useMeanA ? meanBlueA : meanBlueB;

        // 2) Ornstein–Uhlenbeck step
        float dt = Time.deltaTime;
        float normalSample = RandomNormal();  // see definition below
        // OU formula:
        //   dX = theta*(mu - X)*dt + sigma*sqrt(dt)*N(0,1)
        currentBlue += theta * (targetMean - currentBlue) * dt
                    + sigma * Mathf.Sqrt(dt) * normalSample;

        // 3) Clamp to [0, 254]
        currentBlue = Mathf.Clamp(currentBlue, 0f, 254f);

        // 4) Compute G so G + B = 254
        float green = 254f - currentBlue;

        // 5) Assign the color (R=0)
        GetComponent<Renderer>().material.color 
            = new Color(0f, green / 255f, currentBlue / 255f);

        // 6) Optionally buffer data for logging
        if (logDataToFile)
        {
            float currentTime = Time.time;
            // Format: "time, currentBlue"
            dataBuffer.Add(currentTime + "," + currentBlue);
        }
    }

    // We'll do the actual file-writing in OnDisable (called when object is destroyed or scene changes)
    void OnDisable()
    {
        WriteDataToFile();
    }

    // If the object remains throughout the entire session, you could also do it in OnApplicationQuit
    void OnApplicationQuit()
    {
        WriteDataToFile();
    }

    // ----- Utility methods -----

    // Simple normal random generator using Box-Muller transform
    private float RandomNormal()
    {
        float u1 = 1.0f - Random.value; // uniform(0,1] 
        float u2 = 1.0f - Random.value;
        return Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
    }

    private void WriteDataToFile()
    {
        if (!logDataToFile || dataBuffer == null || dataBuffer.Count == 0)
            return;

        // Construct a full path in persistentDataPath or anywhere you like
        string fullPath = Path.Combine(Application.persistentDataPath, fileName);

        // Write all lines
        File.AppendAllLines(fullPath, dataBuffer);

        // Clear buffer so we don't duplicate if OnDisable is called again
        dataBuffer.Clear();

        Debug.Log("Wrote data to: " + fullPath);
    }
}