using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System;
using Newtonsoft.Json;

/// <summary>
/// Simple ZMQ sender that broadcasts VR pose data every frame.
/// Attach to MainController or any persistent GameObject.
/// </summary>
public class ZmqSender : MonoBehaviour
{
    [Header("ZMQ Settings")]
    [SerializeField] private string bindAddress = "127.0.0.1";
    [SerializeField] private int port = 9999;
    [SerializeField] private bool isEnabled = true;

    private PublisherSocket publisher;
    private MainController mainController;
    private OptomotorSceneController optomotorController;
    
    // Track previous frame for deltas
    private Dictionary<GameObject, PoseData> previousPose = new Dictionary<GameObject, PoseData>();
    private PoseData previousDrumPose = null;

    private class PoseData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 sensorPos;
        public Vector3 sensorRot;
    }

    [Serializable]
    private class Message
    {
        public float timestamp;
        public string sceneName;
        public int trial;
        public int sequence;
        public string sequenceJsonName;
        public List<VRData> vrData = new List<VRData>();
        public DrumData drumData;
    }

    [Serializable]
    private class VRData
    {
        public string vrName;
        public float[] position = new float[3];
        public float[] rotation = new float[3];
        public float[] sensorPosition = new float[3];
        public float[] sensorRotation = new float[3];
        public float[] positionDelta = new float[3];
        public float[] rotationDelta = new float[3];
        public float gain;
        public float dcOffset;
        public float rotationCallRate; // The exact rotation rate (deg/s) sent to Unity Rotate() - gain * (yaw_rad - dcOffset_rad) * Rad2Deg
        public float rotationCallThisFrame; // The exact rotation (degrees) applied this frame - rotationCallRate * Time.deltaTime
    }

    [Serializable]
    private class DrumData
    {
        public float[] position = new float[3];
        public float[] rotation = new float[3];
        public float[] rotationDelta = new float[3];
    }
    
    private static float[] Vec3ToArray(Vector3 v)
    {
        return new float[] { v.x, v.y, v.z };
    }

    void Start()
    {
        if (!isEnabled) return;

        mainController = FindObjectOfType<MainController>();

        try
        {
            publisher = new PublisherSocket();
            publisher.Options.Linger = TimeSpan.Zero;
            publisher.Bind($"tcp://{bindAddress}:{port}");
            System.Threading.Thread.Sleep(100);
            Debug.Log($"ZmqSender: Bound to tcp://{bindAddress}:{port}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZmqSender: Failed to bind: {ex.Message}");
            isEnabled = false;
        }
    }

    void LateUpdate()
    {
        if (!isEnabled || publisher == null) return;

        ClosedLoop[] closedLoops = FindObjectsOfType<ClosedLoop>();
        if (closedLoops.Length == 0) return;

        if (optomotorController == null)
            optomotorController = FindObjectOfType<OptomotorSceneController>();

        Message msg = new Message
        {
            timestamp = Time.time,
            sceneName = SceneManager.GetActiveScene().name,
            trial = mainController != null ? mainController.currentTrial : 0,
            sequence = mainController != null ? mainController.currentStep : 0,
            sequenceJsonName = GetSequenceJsonName(),
            vrData = new List<VRData>(),
            drumData = null
        };

        // Collect VR data
        foreach (ClosedLoop cl in closedLoops)
        {
            GameObject vr = cl.gameObject;
            ZmqListener zmq = vr.GetComponent<ZmqListener>();
            if (zmq == null) continue;

            Vector3 pos = vr.transform.position;
            Quaternion rot = vr.transform.rotation;
            Vector3 sensPos = zmq.position;
            Vector3 sensRot = zmq.rawRotation;

            Vector3 posDelta = Vector3.zero;
            Vector3 rotDelta = Vector3.zero;

            if (previousPose.TryGetValue(vr, out PoseData prev))
            {
                posDelta = pos - prev.position;
                Quaternion rotDiff = rot * Quaternion.Inverse(prev.rotation);
                rotDelta = rotDiff.eulerAngles;
                if (rotDelta.x > 180f) rotDelta.x -= 360f;
                if (rotDelta.y > 180f) rotDelta.y -= 360f;
                if (rotDelta.z > 180f) rotDelta.z -= 360f;
            }

            // Get the exact rotation call values from ClosedLoop
            float rotationCallRate = cl.GetLastYawOutput(); // Rotation rate in deg/s (gain * (yaw_rad - dcOffset_rad) * Rad2Deg)
            float rotationCallThisFrame = rotationCallRate * Time.deltaTime; // Actual rotation applied this frame in degrees

            msg.vrData.Add(new VRData
            {
                vrName = vr.name,
                position = Vec3ToArray(pos),
                rotation = Vec3ToArray(rot.eulerAngles),
                sensorPosition = Vec3ToArray(sensPos),
                sensorRotation = Vec3ToArray(sensRot),
                positionDelta = Vec3ToArray(posDelta),
                rotationDelta = Vec3ToArray(rotDelta),
                gain = cl.GetYawGain(),
                dcOffset = cl.GetYawDCOffset(),
                rotationCallRate = rotationCallRate,
                rotationCallThisFrame = rotationCallThisFrame
            });

            previousPose[vr] = new PoseData
            {
                position = pos,
                rotation = rot,
                sensorPos = sensPos,
                sensorRot = sensRot
            };
        }

        // Add drum data if in optomotor scene
        if (optomotorController != null)
        {
            Transform drum = optomotorController.GetDrumTransform();
            if (drum != null)
            {
                Vector3 drumPos = drum.position;
                Quaternion drumRot = drum.rotation;
                Vector3 drumRotDelta = Vector3.zero;

                if (previousDrumPose != null)
                {
                    Quaternion diff = drumRot * Quaternion.Inverse(previousDrumPose.rotation);
                    drumRotDelta = diff.eulerAngles;
                    if (drumRotDelta.x > 180f) drumRotDelta.x -= 360f;
                    if (drumRotDelta.y > 180f) drumRotDelta.y -= 360f;
                    if (drumRotDelta.z > 180f) drumRotDelta.z -= 360f;
                }

                msg.drumData = new DrumData
                {
                    position = Vec3ToArray(drumPos),
                    rotation = Vec3ToArray(drumRot.eulerAngles),
                    rotationDelta = Vec3ToArray(drumRotDelta)
                };

                previousDrumPose = new PoseData
                {
                    position = drumPos,
                    rotation = drumRot,
                    sensorPos = Vector3.zero,
                    sensorRot = Vector3.zero
                };
            }
        }

        // Send
        try
        {
            string json = JsonConvert.SerializeObject(msg, Formatting.None);
            publisher.SendMoreFrame("VR_POSE");
            publisher.SendFrame(json);
        }
        catch (Exception ex)
        {
            if (Time.frameCount % 300 == 0)
                Debug.LogWarning($"ZmqSender: Send error: {ex.Message}");
        }
    }

    private string GetSequenceJsonName()
    {
        if (mainController == null) return "";
        SequenceStep step = mainController.GetCurrentSequenceStep();
        if (step?.parameters?.ContainsKey("configFile") == true)
            return step.parameters["configFile"].ToString();
        return "sequenceConfig.json";
    }

    void OnDestroy()
    {
        publisher?.Close();
        publisher?.Dispose();
    }
}
