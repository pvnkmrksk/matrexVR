/**************************************************************************
 *  MainController.cs  –  drop-in replacement
 *  -------------------------------------------------
 *  • Generates a session-specific, randomised sequence JSON at launch
 *  • Copies that JSON into your data-logging folder for traceability
 *  • Runs the experiment exactly as before (timing, looping, etc.)
 *  -------------------------------------------------
 *  Requires:
 *      - SequenceConfigGenerator.cs   (see previous message)
 *      - SequenceStep, SequenceItem, SequenceConfig classes
 *      - MasterDataLogger, Debugger, etc.  (unchanged)
 **************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;public interface ISceneController
{
    void InitializeScene(Dictionary<string, object> parameters);
}
public class MainController : MonoBehaviour
{
    /* ───────────── inspector fields ───────────── */

    public bool loopSequence = false;

    [Tooltip("0: Off, 1: Error, 2: Warning, 3: Info, 4: Debug")]
    [Range(0, 4)]
    [SerializeField] private int logLevel = 0;

    /* ───────────── runtime state ───────────── */

    public  List<SequenceStep> sequenceSteps  = new List<SequenceStep>();
    private List<int>          executionOrder = new List<int>();

    public int   currentStep   = 0;
    public int   currentTrial  = 0;
    private float timer         = 0f;
    private bool  sequenceStarted = false;

    private MasterDataLogger masterDataLogger;
    private string sessionConfigPath = string.Empty;

    /* ====================================================================
       UNITY LIFECYCLE
       ==================================================================== */

    private void Start()
    {
        /* 1 ░ house-keeping */
        Debugger.CurrentLogLevel = logLevel;
        Debugger.Log("MainController.Start()", 3);
        DontDestroyOnLoad(gameObject);

        masterDataLogger = MasterDataLogger.Instance;

        /* 2 ░ generate session-specific config (may throw) */
        SequenceConfig sessionConfig;
        try
        {
            (sessionConfig, sessionConfigPath) =
                SequenceConfigGenerator.CreateSessionConfig();
        }
        catch (Exception ex)
        {
            Debugger.Log("Failed to create session config: " + ex, 1);
            enabled = false;
            return;
        }

        /* 3 ░ convert ordered config → runtime list */
        PopulateSequenceSteps(sessionConfig);

        /* 4 ░ copy JSON next to other logs */
        if (masterDataLogger != null && File.Exists(sessionConfigPath))
        {
            string dest = Path.Combine(masterDataLogger.directoryPath,
                                       Path.GetFileName(sessionConfigPath));
            File.Copy(sessionConfigPath, dest, true);

            SaveReferencedChoiceConfigs(sessionConfig,
                                        masterDataLogger.timestamp,
                                        SceneManager.GetActiveScene().name);
        }

        /* 5 ░ build sequential executionOrder */
        executionOrder.Clear();
        for (int i = 0; i < sequenceSteps.Count; ++i)
            executionOrder.Add(i);

        /* 6 ░ ready to start – caller still decides when to call StartSequence() */
    }

    /* --------------- public control ---------------- */

    public void StartSequence()
    {
        sequenceStarted = true;
        currentStep     = 0;
        timer           = sequenceSteps[executionOrder[0]].duration;

        LoadScene(sequenceSteps[executionOrder[0]]);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void StopSequence()
    {
        sequenceStarted = false;
        currentStep     = 0;
    }

    public SequenceStep GetCurrentSequenceStep()
    {
        return currentStep < executionOrder.Count
            ? sequenceSteps[executionOrder[currentStep]]
            : null;
    }

    /* ====================================================================
       HELPERS
       ==================================================================== */

    /// <summary>
    /// Converts the ordered SequenceConfig into sequenceSteps.
    /// </summary>
    private void PopulateSequenceSteps(SequenceConfig config)
    {
        sequenceSteps.Clear();
        foreach (SequenceItem item in config.sequences)
            sequenceSteps.Add(new SequenceStep(
                item.sceneName,
                item.duration,
                item.parameters
            ));
    }

    /* ---------------- per-frame ---------------- */

    private void Update()
    {
        if (sequenceStarted) ManageTimerAndTransitions();

        if (Input.GetKeyUp(KeyCode.Escape))
            Application.Quit();
    }

    /* ---------------- scene loading ---------------- */

    private void LoadScene(SequenceStep step)
    {
        SceneManager.LoadScene(step.sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SequenceStep stepData = sequenceSteps[executionOrder[currentStep]];

        // find first object that implements ISceneController
        ISceneController controller = null;
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
        {
            if (mb is ISceneController)
            {
                controller = (ISceneController)mb;
                break;
            }
        }

        if (controller != null && stepData.parameters != null)
            controller.InitializeScene(stepData.parameters);
        else
            Debugger.Log("Scene controller or parameters missing.", 2);

        timer = stepData.duration;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /* ---------------- timing & transitions ---------------- */

    private void ManageTimerAndTransitions()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        currentStep++;

        if (currentStep >= sequenceSteps.Count)
        {
            if (loopSequence)
            {
                currentStep  = 0;
                currentTrial++;

                LoadScene(sequenceSteps[executionOrder[currentStep]]);
            }
            else
            {
                SceneManager.LoadScene("ControlScene");
                Destroy(gameObject);
            }
        }
        else
        {
            LoadScene(sequenceSteps[executionOrder[currentStep]]);
        }
    }

    /* ---------------- housekeeping helpers ---------------- */

    private void SaveReferencedChoiceConfigs(SequenceConfig cfg,
                                             string timestamp,
                                             string sceneName)
    {
        foreach (SequenceItem item in cfg.sequences)
        {
            if (item.parameters != null &&
                item.parameters.ContainsKey("configFile"))
            {
                string cfgFile = item.parameters["configFile"].ToString();
                string srcPath = Path.Combine(Application.streamingAssetsPath,
                                              cfgFile);

                if (!File.Exists(srcPath))
                {
                    Debugger.Log("Choice config missing: " + cfgFile, 2);
                    continue;
                }

                string destPath = Path.Combine(
                    masterDataLogger.directoryPath,
                    $"{timestamp}_{sceneName}_{cfgFile}"
                );
                File.Copy(srcPath, destPath, true);
            }
        }
    }
}

[System.Serializable]
public class SequenceStep
{
    public string sceneName;
    public float duration;
    public Dictionary<string, object> parameters;

    public SequenceStep(string sceneName, float duration, Dictionary<string, object> parameters)
    {
        this.sceneName = sceneName;
        this.duration = duration;
        this.parameters = parameters;
    }
}

[System.Serializable]
public class SequenceConfig
{
    public bool randomise = false; // Added field
    public int  seed        = -1; 
    public SequenceItem[] sequences;
}

[System.Serializable]
public class SequenceItem
{
    public string sceneName;
    public float duration;
    public Dictionary<string, object> parameters;
}