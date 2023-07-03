using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class activateDisplays : MonoBehaviour
{
    void Start()
{
    // Check if additional displays are available and activate them.
    for (int i = 0; i < Display.displays.Length; i++)
    {
        Display.displays[i].Activate();
    }
}
}
