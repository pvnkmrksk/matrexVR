using System;
using System.Linq;
using Newtonsoft.Json.Linq;

/// <summary>Reconstructs recorded condition decisions without rerunning random trial selection.</summary>
public static class ReplayStimulusState
{
    public static JObject Step(JObject config, ReplaySessionArchive.Sample sample, out string warning)
    {
        warning = null;
        if (!(config?["steps"] is JArray steps)) return config;
        JObject step = steps.OfType<JObject>().FirstOrDefault(s => (string)s["name"] == sample.stepName);
        if (step != null) return step;
        if (config["intertrial"] is JObject interval && (string)interval["name"] == sample.stepName) return interval;
        if (sample.stepName == null || !sample.stepName.StartsWith("AdaptiveDecision_", StringComparison.Ordinal))
        { warning = "Saved dynamic step unavailable: " + sample.stepName; return null; }
        var template = steps.OfType<JObject>().FirstOrDefault();
        if (template == null)
        { warning = "Adaptive trial has no saved template; original object geometry cannot be inferred."; return null; }
        step = (JObject)template.DeepClone(); step["name"] = sample.stepName;
        var objects = step["objects"] as JArray ?? new JArray();
        if (sample.stepName.Contains("_noBar_")) { step["objects"] = new JArray(); return step; }
        if (sample.stepName.Contains("_singleBlack0deg_"))
        {
            if (!(objects.FirstOrDefault() is JObject first))
            { warning = "Adaptive single-bar trial has no saved object template."; return null; }
            var bar = (JObject)first.DeepClone();
            float radius = objects.OfType<JObject>().Select(o => (float?)o["polar"]?["radius"] ?? 0).FirstOrDefault(r => r > 0);
            bar["polar"] = new JObject { ["radius"] = radius > 0 ? radius : 60, ["angle"] = 0, ["height"] = (float?)bar["polar"]?["height"] ?? 0 };
            bar["color"] = Color(0); step["objects"] = new JArray(bar); return step;
        }
        if (objects.Count < 2)
        { warning = "Adaptive pair trial has fewer than two saved object templates."; return null; }
        if (sample.stepName.Contains("_twoBlack_"))
        { foreach (var item in objects.Take(2).OfType<JObject>()) item["color"] = Color(0); return step; }
        bool? blackLeft = sample.blackSideAtTrialStart == "left" ? true : sample.blackSideAtTrialStart == "right" ? false :
            sample.stepName.EndsWith("_blackL", StringComparison.Ordinal) ? true : sample.stepName.EndsWith("_blackR", StringComparison.Ordinal) ? false : (bool?)null;
        if (!blackLeft.HasValue || !sample.grayAtTrialStart.HasValue)
        { warning = "Adaptive black/gray onset evidence is incomplete; saved template colors are approximate."; return step; }
        int left = ((float?)objects[0]["polar"]?["angle"] ?? 0) <= ((float?)objects[1]["polar"]?["angle"] ?? 0) ? 0 : 1;
        objects[blackLeft.Value ? left : 1 - left]["color"] = Color(0);
        objects[blackLeft.Value ? 1 - left : left]["color"] = Color(sample.grayAtTrialStart.Value);
        return step;
    }
    private static JArray Color(float value) => new JArray(value, value, value, 1);
}
