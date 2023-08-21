using UnityEngine;

public class LocustMover : MonoBehaviour
{
    public float speed = 0.1f;

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self); // Move based on local space
    }
}
