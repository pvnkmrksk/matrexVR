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
    private float[] possibleAngularSpeeds = { -15f, 0f, 15f }; // left, still, right
    private float lastSpeedChangeTime = 0f;
    private bool stripesGenerated = false;
    private float stripeStartTime = 0;


    void Start()
    {
        // Set the background color of all cameras to gray at the start
        foreach (Camera camera in Camera.allCameras)
        {
            camera.backgroundColor = Color.gray;
        }
    }

    void Update()
    {
        if (!stripesGenerated && Input.GetKeyDown(KeyCode.Space))
        {
            GenerateStripesNow();
            stripesGenerated = true;

            // Set the background color of all cameras to white when stripes are generated
            foreach (Camera camera in Camera.allCameras)
            {
                camera.backgroundColor = Color.white;
            }

            stripeStartTime = Time.time;
        }

        if (stripesGenerated)
        {
            if (Time.time - lastSpeedChangeTime > 10) // If one minute has passed
            {
                lastSpeedChangeTime = Time.time;
                angularSpeed = possibleAngularSpeeds[Random.Range(0, possibleAngularSpeeds.Length)]; // Choose a new random speed
            }

            float radius = (1 / (2 * Mathf.Tan(stripeSize * Mathf.Deg2Rad) / 2));
            float angleStep = 360f / numberOfObjects;
            for (int i = 0; i < numberOfObjects; i++)
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

    void GenerateStripesNow()
    {
        float radius = (1 / (2 * Mathf.Tan(stripeSize * Mathf.Deg2Rad) / 2));
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

    public float GetAngularSpeed()
    {
        return angularSpeed;
    }
    public float GetStripeStartTime()
    {
        return Time.time - stripeStartTime;
    }
}
