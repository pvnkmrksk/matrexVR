using UnityEngine;

public class ClosedLoop : MonoBehaviour
{
    [SerializeField, Range(0, 10)]
    private float xGain = 1.0f;

    [SerializeField, Range(0, 10)]
    private float yGain = 1.0f;

    [SerializeField, Range(0, 10)]
    private float zGain = 1.0f;

    [SerializeField, Range(0, 10)]
    private float rollGain = 1.0f;

    [SerializeField, Range(0, 10)]
    private float pitchGain = 1.0f;

    [SerializeField, Range(0, 10)]
    private float yawGain = 1.0f;

    private bool closedLoopOrientation = false;
    private bool closedLoopPosition = false;

    private Camera mainCamera;
    private ZmqListener _zmqListener;

    private void Awake()
    {
        _zmqListener = GetComponent<ZmqListener>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera is missing in the scene");
            return;
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            closedLoopOrientation = !closedLoopOrientation;
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            closedLoopPosition = !closedLoopPosition;
        }

        if (_zmqListener.pose != null)
        {
            if (closedLoopPosition)
            {
                transform.position +=
                    mainCamera.transform.forward * _zmqListener.pose.position.y * zGain;
                transform.position +=
                    mainCamera.transform.right * _zmqListener.pose.position.x * xGain;
                transform.position +=
                    mainCamera.transform.up * _zmqListener.pose.position.z * yGain;
            }

            if (closedLoopOrientation)
            {
                transform.Rotate(
                    new Vector3(
                        _zmqListener.pose.rotation.x * rollGain,
                        _zmqListener.pose.rotation.y * yawGain,
                        _zmqListener.pose.rotation.z * pitchGain
                    )
                );
            }
        }
    }
}
