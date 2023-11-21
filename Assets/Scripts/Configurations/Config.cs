using System.Collections.Generic;

[System.Serializable]
public class ParadigmList
{
    public List<ParadigmConfig> Paradigms;
}

[System.Serializable]
public class ParadigmConfig
{
    public string Name;
    public string ExperimentsConfig; // This is a string because it's a reference to a JSON file
}

[System.Serializable]
public class ExperimentList
{
    public List<ExperimentConfig> Experiments;
}

[System.Serializable]
public class ExperimentConfig
{
    public string Name;
    public string SceneName;
    public float PreStimulusDuration;
    public float PostStimulusDuration;
    public int TrialRepetitions;
    public Dictionary<string, object> AdditionalParameters;
}