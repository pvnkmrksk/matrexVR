using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Real animals control rigs; generated or replayed individuals are agents.</summary>
public class AnimalData
{
    private List<AnimalMetadata> animals = new List<AnimalMetadata>();
    private List<string> usedAnimalIDs = new List<string>();
    private bool animalsSpecified, usedIDsSpecified;
    public string ExperimentName { get; set; }
    public string ExperimenterName { get; set; }
    public string Comments { get; set; }
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<AnimalMetadata> Animals
    { get => animals; set { animals = value ?? new List<AnimalMetadata>(); animalsSpecified = true; } }
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> UsedAnimalIDs
    { get => usedAnimalIDs; set { usedAnimalIDs = value ?? new List<string>(); usedIDsSpecified = true; } }
    // Transitional output aliases keep existing analysis scripts working. Canonical
    // values win on input regardless of JSON property order.
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<AnimalMetadata> Flies
    { get => animals; set { if (!animalsSpecified) animals = value ?? new List<AnimalMetadata>(); } }
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> UsedFlyIDs
    { get => usedAnimalIDs; set { if (!usedIDsSpecified) usedAnimalIDs = value ?? new List<string>(); } }
    [JsonExtensionData] public IDictionary<string, JToken> Extensions { get; set; }
}

public class AnimalMetadata
{
    private string animalID;
    private bool idSpecified;
    public string VR { get; set; }
    public string SpeciesName { get; set; }
    public string AnimalID
    { get => animalID; set { animalID = value; idSpecified = true; } }
    public string AgeDays { get; set; }
    public string StarvedSinceHours { get; set; }
    public string Sex { get; set; }
    public string Comments { get; set; }
    public string FlyID
    { get => animalID; set { if (!idSpecified) animalID = value; } }
    [JsonExtensionData] public IDictionary<string, JToken> Extensions { get; set; }
}

// Source compatibility for integrations that still reference the old type names.
public class FlyData : AnimalData { }
public class Fly : AnimalMetadata { }
