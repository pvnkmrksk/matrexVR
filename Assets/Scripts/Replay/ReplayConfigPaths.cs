using System.IO;
using UnityEngine;

/// <summary>
/// Resolves JSON and asset paths from an active replay session folder when replay is running.
/// </summary>
public static class ReplayConfigPaths
{
    public static string ResolveJson(string fileName)
    {
        if (ReplaySessionContext.IsActive && ReplaySessionContext.Archive != null)
        {
            if (ReplaySessionContext.Archive.TryResolveConfigPath(fileName, out string path))
                return path;
        }
        return Path.Combine(Application.streamingAssetsPath, fileName);
    }

    public static string ResolveAsset(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return relativePath;

        if (ReplaySessionContext.IsActive && ReplaySessionContext.Archive != null)
        {
            if (ReplaySessionContext.Archive.TryResolveAssetPath(relativePath, out string path))
                return path;
        }
        return Path.Combine(Application.streamingAssetsPath, relativePath);
    }
}
