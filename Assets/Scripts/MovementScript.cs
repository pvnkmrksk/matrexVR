using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementScript : MonoBehaviour
{
    public Vector3 direction;
    public float speed;

    void Update()
    {
        //transform.position += direction * speed * Time.deltaTime;
        transform.position += transform.forward * speed * Time.deltaTime;
    }
}

