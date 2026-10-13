using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>Validate cross-block swarm references without depending on any setup GUI.</summary>
public static class SwarmHeadingSequenceValidation
{
    public static void Validate(SequenceConfig sequence)
    {
        sequence.ValidateLoop();
        var saved = new HashSet<string>(StringComparer.Ordinal);
        var steps = sequence.sequences ?? Array.Empty<SequenceItem>();
        for (int index = 0; index < steps.Length; index++)
        {
            if (sequence.loop && index == sequence.loopStartIndex) saved.Clear();
            var step = steps[index];
            if (step?.parameters == null || !step.parameters.TryGetValue("configFile", out object file) || file == null) continue;
            var document = JObject.Parse(ExperimentConfigFiles.ReadAllText(file.ToString(), "experiment-validation"));
            bool phased = document["preStimulus"] != null || document["stimulus"] != null || document["postStimulus"] != null;
            if (!phased) ValidatePayload(document, saved, sequence.randomise);
            else foreach (string phase in new[] { "preStimulus", "stimulus", "postStimulus" })
            {
                if (!(document[phase] is JObject payload) || !((bool?)payload["enabled"] ?? phase == "stimulus")) continue;
                ValidatePayload(payload, saved, sequence.randomise);
            }
        }
    }

    private static void ValidatePayload(JObject payload, HashSet<string> saved, bool randomise)
    {
        var swarm = payload["swarm"] as JObject;
        string id = (string)(swarm?["headingReferenceId"] ?? payload["headingReferenceId"]);
        if (!string.IsNullOrWhiteSpace(id))
        {
            if (randomise) throw new ArgumentException("Named heading references require randomise=false so the measurement runs first.");
            if (!saved.Contains(id)) throw new ArgumentException("headingReferenceId '" + id + "' must be saved by an earlier block in this cycle.");
        }
        var heading = payload["headingReference"]?.ToObject<HeadingReferenceConfig>();
        heading?.Validate();
        if (heading?.enabled == true && !string.IsNullOrWhiteSpace(heading.saveAs)) saved.Add(heading.saveAs);
    }
}
