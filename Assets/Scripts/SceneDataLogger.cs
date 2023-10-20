public class SceneDataLogger : DataLogger
{
    protected override void InitLog()
    {
        base.InitLog();

        // Add new column to the header row
        logFile.WriteLine(",Active GameObjects");
    }

    protected override void Update()
    {
        base.Update();

        // Log the number of active GameObjects in the scene
        int activeGameObjectCount = 0;
        foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
        {
            if (go.activeInHierarchy)
            {
                activeGameObjectCount++;
            }
        }

        string line = "," + activeGameObjectCount.ToString();
        LogData(line);
    }
}