using UnityEngine;

public class ClosedLoop : MonoBehaviour
{
    [SerializeField, Range(0, 1000)]
    private float xGain = 100.0f;

    [SerializeField, Range(0, 1000)]
    private float yGain = 100.0f;

    [SerializeField, Range(0, 1000)]
    private float zGain = 100.0f;

    [SerializeField, Range(0, 1000)]
    private float rollGain = 100.0f;

    [SerializeField, Range(0, 1000)]
    private float pitchGain = 100.0f;

    [SerializeField, Range(0, 1000)]
    private float yawGain = 100.0f;

    private bool closedLoopOrientation = false;
    private bool closedLoopPosition = false;

    [Tooltip("To close the loop on the raw position or on velocity as applying a force or torque")]
    [SerializeField]
    bool momentumClosedLoop = false;

    private Camera mainCamera;
    private ZmqListener _zmqListener;

    private void Awake()
    {
        _zmqListener = GetComponent<ZmqListener>();
        mainCamera = Camera.main;

        Application.targetFrameRate = 60;
        Time.fixedDeltaTime = 1f / 60f;
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

        if (Input.GetKeyDown(KeyCode.M))
        {
            momentumClosedLoop = !momentumClosedLoop;
        }

        if (_zmqListener.pose != null)
        {
            if (closedLoopPosition)
            {
                //if we want to apply a force instead of moving the object directly else we move the object directly

                if (momentumClosedLoop)
                {
                    //apply a force to the object
                    transform.position +=
                        mainCamera.transform.forward * _zmqListener.pose.position.y * zGain;
                    transform.position +=
                        mainCamera.transform.right * _zmqListener.pose.position.x * xGain;
                    transform.position +=
                        mainCamera.transform.up * _zmqListener.pose.position.z * yGain;
                }
                else
                {
                    //move the object directly
                    transform.position = new Vector3(
                        _zmqListener.pose.position.x * xGain,
                        _zmqListener.pose.position.z * zGain,
                        _zmqListener.pose.position.y * yGain
                    );
                }
            }

            if (closedLoopOrientation)
            {
                //if we want to apply a torque instead of rotating the object directly else we rotate the object directly

                if (momentumClosedLoop)
                {
                    //apply a torque to the object


                    transform.Rotate(
                        new Vector3(
                            _zmqListener.pose.rotation.x * rollGain,
                            _zmqListener.pose.rotation.y * yawGain,
                            _zmqListener.pose.rotation.z * pitchGain
                        )
                    );
                }
                else
                {
                    //rotate the object directly
                    transform.rotation = Quaternion.Euler(
                        _zmqListener.pose.rotation.eulerAngles.x * pitchGain,
                        _zmqListener.pose.rotation.eulerAngles.y * yawGain,
                        _zmqListener.pose.rotation.eulerAngles.z * rollGain
                    );
                }
            }

            //reset to zero if the user presses the R key
            if (Input.GetKeyDown(KeyCode.R))
            {
                transform.position = Vector3.zero;
                transform.rotation = Quaternion.identity;
            }
        }
    }
}
