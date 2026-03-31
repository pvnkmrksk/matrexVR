using UnityEngine;

public class DirectionalMovement : MonoBehaviour
{
    [SerializeField] private float speed = 2f;
    [SerializeField] private float directionAngle = 0f; // Direction angle in degrees (on the XZ plane)

    void Start()
    {
        UpdateRotation();
    }

    void Update()
    {
        MoveForward();
    }

    private void MoveForward()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void UpdateRotation()
    {
        float angleInRadians = directionAngle * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(angleInRadians), 0f, Mathf.Sin(angleInRadians));
        transform.rotation = Quaternion.LookRotation(direction);
    }

    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0f, newSpeed); // Ensure non-negative speed
    }

    public float GetSpeed()
    {
        return speed;
    }

    public void SetDirection(float newAngle)
    {
        directionAngle = newAngle % 360f; // Normalize angle to 0-360 range
        UpdateRotation();
    }

    public float GetDirection()
    {
        return directionAngle;
    }
}