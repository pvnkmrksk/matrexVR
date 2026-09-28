using UnityEngine;

// Explicit asset references keep selectable prefabs available in standalone builds.
public class KannadiPrefabCatalog : ScriptableObject
{
    public GameObject[] prefabs;
    public string[] names;
    public GameObject Find(string prefabName)
    {
        if (prefabs != null)
            for (int i = 0; i < prefabs.Length; i++)
                if (prefabs[i] != null && ((names != null && i < names.Length && names[i] == prefabName) || prefabs[i].name == prefabName)) return prefabs[i];
        return null;
    }
}
