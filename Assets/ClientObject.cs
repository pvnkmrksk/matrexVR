using System.Collections.Concurrent;
using System.Threading;
using NetMQ;
using UnityEngine;
using NetMQ.Sockets;
using System.Collections;
using System.Collections.Generic;

using Newtonsoft.Json;

// a new variable containing gain factors for each axis
public class Data
{
    public float x;
    public float y;
    public float z;
    public float roll;
    public float pitch;
    public float yaw;
}

public class NetMqListener
{
    // new serializefield to hold the ipaddress of the server
    [SerializeField]
    public string ipAddress = "localhost:9872";

    // [SerializeField] public string ipAddress = "10.126.18.11:9872";
    [SerializeField]
    private int highWaterMark = 1;
    private readonly Thread _listenerWorker;

    private bool _listenerCancelled;

    public delegate void MessageDelegate(string message);

    private readonly MessageDelegate _messageDelegate;

    private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();

    private void ListenerWork()
    {
        AsyncIO.ForceDotNet.Force();
        using (var subSocket = new SubscriberSocket())
        {
            subSocket.Options.ReceiveHighWatermark = highWaterMark;
            subSocket.Connect("tcp://" + ipAddress);

            subSocket.Subscribe("");
            while (!_listenerCancelled)
            {
                string frameString;
                if (!subSocket.TryReceiveFrameString(out frameString))
                    continue;
                // Debug.Log(frameString);
                _messageQueue.Enqueue(frameString);
            }
            subSocket.Close();
        }
        NetMQConfig.Cleanup();
    }

    public void Update()
    {
        while (!_messageQueue.IsEmpty)
        {
            string message;
            if (_messageQueue.TryDequeue(out message))
            {
                _messageDelegate(message);
            }
            else
            {
                break;
            }
        }
    }

    public NetMqListener(MessageDelegate messageDelegate)
    {
        _messageDelegate = messageDelegate;
        _listenerWorker = new Thread(ListenerWork);
    }

    public void Start()
    {
        _listenerCancelled = false;
        _listenerWorker.Start();
    }

    public void Stop()
    {
        _listenerCancelled = true;
        _listenerWorker.Join();
    }
}

public class ClientObject : MonoBehaviour
{
    //init unity editor variables for gain factors
    [SerializeField]
    private float xGain = 1.0f;

    [SerializeField]
    private float yGain = 1.0f;

    [SerializeField]
    private float zGain = 1.0f;

    [SerializeField]
    private float rollGain = 1.0f;

    [SerializeField]
    private float pitchGain = 1.0f;

    [SerializeField]
    private float yawGain = 1.0f;

    [SerializeField]
    private bool closedLoop = false;

    //

    private NetMqListener _netMqListener;

    private void HandleMessage(string message)
    {
        // Debug.Log("Text here");

        Data data = JsonConvert.DeserializeObject<Data>(message);

        if (closedLoop){
            // use the x y z from data to move the camera in the direction Camera.main.transform.forward
            transform.position += Camera.main.transform.forward * data.y * yGain;
            transform.position += Camera.main.transform.right * data.x * xGain;
            transform.position += Camera.main.transform.up * data.z * zGain;

            // transform.position += new Vector3(data.x * xGain, data.z * yGain, data.y * zGain);

            // rotate the camera by amount of degrees from data
            // transform.Rotate(new Vector3(data.roll * gain, data.pitch * gain, data.yaw * gain));
            transform.Rotate(
                new Vector3(data.roll * rollGain, data.yaw * yawGain, data.pitch * pitchGain)
            // new Vector3(data.roll * rollGain, data.pitch * pitchGain, data.yaw * yawGain)
            );
            // use the roll pitch yaw to rotate the camera by amount of degrees from data

            // transform.Rotate(new Vector3(data.roll, data.pitch, data.yaw));

            // transform using the data
            // transform.position = new Vector3(data.x * xGain, data.z * yGain, data.y * zGain);
        }
    }

    public void Start()
    {
        _netMqListener = new NetMqListener(HandleMessage);
        _netMqListener.Start();
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.O))
        {
            closedLoop = false
        }
        if (Input.GetKey(KeyCode.P))
        {
            closedLoop = true
        }
        _netMqListener.Update();
    }

    private void OnDestroy()
    {
        _netMqListener.Stop();
    }
}
