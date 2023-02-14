using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenerateStripes : MonoBehaviour
{
    public float radius = 1f;
    public int numberOfObjects = 10;
    public GameObject prefab;
    public float rotationSpeed = 10f;
    public float initAngle = 0f;

    void Start()
    {
        float angleStep = 360f / numberOfObjects;
        for (int i = 0; i < numberOfObjects; i++)
        {
            float angle = (i * angleStep) + initAngle;
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            Vector3 pos = new Vector3(x, 0, z);
            Instantiate(prefab, pos, Quaternion.identity, transform);
        }
    }

    // Update is called once per frame
    void Update()
    {
    }
}
