using UnityEngine;

public static class Logger
{

    public static int CurrentLogLevel = 0; // 0: All, 1: Error, 2: Warning, 3: Info, 4: Debug

    /// <summary>
    /// Logs a message with the specified log level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="level">The log level of the message (1: Error, 2: Warning, 3: Info, 4: Debug).</param>
/*     feat: Add Logger class for flexible logging with different log levels

    Description:
    - Implemented the Logger class to provide a simple logging mechanism with different log levels.
    - The Logger class allows logging messages with different levels such as Error, Warning, Info, and Debug.
    - The log level can be set to control which messages are logged based on their severity.
    - The Logger class uses the Debug.LogError, Debug.LogWarning, and Debug.Log methods from Unity for logging.
    - Added a SetLogLevel method to dynamically change the log level at runtime.
    - The Logger class is designed to be easy to use and can be integrated into any Unity project. */
    public static void Log(string message, int level)
    {
        if (level <= CurrentLogLevel)
        {
            switch (level)
            {
                case 1:
                    Debug.LogError(message);
                    break;
                case 2:
                    Debug.LogWarning(message);
                    break;
                case 3:
                    Debug.Log(message);
                    break;
                case 4:
                    Debug.Log(message); // Debug level uses the same Logger.Log method
                    break;
            }
        }
    }

    public static void Log(string message)
    {
        Log(message, 1); // Default to level 1 if no level is provided
    }
    public static void SetLogLevel(int level)
    {
        CurrentLogLevel = level;
    }
}