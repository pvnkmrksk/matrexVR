using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenerateStripes : MonoBehaviour
{
    public float stripeSize = 10f;
    public int numberOfObjects = 10;
    public GameObject prefab;
    List<GameObject> myObjects = new List<GameObject>();
    public float angularSpeed = 15f;
    void Start()
    {
        float radius = (1/(2*Mathf.Tan(stripeSize * Mathf.Deg2Rad)/2));
        float angleStep = 360f / numberOfObjects;
        for (int i = 0; i < numberOfObjects; i++)
        {
            float angle = (i * angleStep);
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            Vector3 pos = new Vector3(x, 0, z);
            Vector3 direction = Vector3.Normalize(Vector3.zero - pos);
            GameObject newObj = Instantiate(prefab, pos, Quaternion.LookRotation(direction), transform);
            myObjects.Add(newObj);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            angularSpeed = -angularSpeed; // Reverse the direction of rotation
        }
        float radius = (1/(2*Mathf.Tan(stripeSize * Mathf.Deg2Rad)/2));
        float angleStep = 360f / numberOfObjects;
        for (int i = 0; i< numberOfObjects; i++)
        {
            GameObject obj = myObjects[i];
            float angle = (i * angleStep) + (angularSpeed * Time.fixedTime);
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            Vector3 newPosition = new Vector3(x, 0, z);
            obj.transform.position = newPosition;
            Vector3 direction = Vector3.Normalize(Vector3.zero - newPosition);
            obj.transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
