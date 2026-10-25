using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Keep saved experiment references working after the legacy config archive move.</summary>
public static class ExperimentConfigFiles
{
    public static string ReadAllText(string reference,string role="experiment")
    {
        var logger=MasterDataLogger.Instance;
        return logger!=null && logger.ConfigArchive!=null
            ? logger.ConfigArchive.ReadText(reference,role,UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
            : File.ReadAllText(Resolve(reference));
    }
    public static byte[] ReadAllBytes(string reference,string role)
    {
        var logger=MasterDataLogger.Instance;
        return logger!=null && logger.ConfigArchive!=null
            ? logger.ConfigArchive.ReadBytes(reference,role,UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
            : File.ReadAllBytes(Resolve(reference));
    }
    public static string Resolve(string reference)
    {
        string current = Path.Combine(RuntimePaths.ConfigRoot, reference);
        if (File.Exists(current) || Path.IsPathRooted(reference)) return current;
        string archived = Path.Combine(RuntimePaths.ConfigRoot, "Archive", "Legacy", reference);
        return File.Exists(archived) ? archived : current;
    }

    // Use a copy so the recorded sequence still contains exactly the user's references.
    public static Dictionary<string, object> ResolveReferences(Dictionary<string, object> parameters)
    {
        if (parameters == null) return null;
        var resolved = new Dictionary<string, object>(parameters);
        foreach (string key in new[] { "configFile", "design" })
            if (resolved.TryGetValue(key, out object value) && value != null)
            {
                string reference = value.ToString();
                string path = Resolve(reference);
                if (path != Path.Combine(RuntimePaths.ConfigRoot, reference))
                    resolved[key] = Path.GetRelativePath(RuntimePaths.ConfigRoot, path);
            }
        return resolved;
    }
}
