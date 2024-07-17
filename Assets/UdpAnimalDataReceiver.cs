using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class UdpAnimalDataReceiver : MonoBehaviour
{
    [SerializeField] private int port = 8080;
    [SerializeField] private bool useSpecificIP = false;
    [SerializeField] private string specificIP = "";

    private UdpClient client;
    private CancellationTokenSource cancellationTokenSource;

    public float[] AnimalData { get; private set; } = new float[25];

    private void Start()
    {
        AnimalData = new float[25];
        StartReceiving();
    }

    private void StartReceiving()
    {
        try
        {
            cancellationTokenSource = new CancellationTokenSource();

            if (useSpecificIP && !string.IsNullOrEmpty(specificIP))
            {
                client = new UdpClient(new IPEndPoint(IPAddress.Parse(specificIP), port));
                Debug.Log($"Listening on specific IP: {specificIP}, Port: {port}");
            }
            else
            {
                client = new UdpClient(port);
                client.EnableBroadcast = true;
                Debug.Log($"Listening for broadcast on Port: {port}");
            }

            ReceiveData();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start UDP client: {e.Message}");
        }
    }

    private async void ReceiveData()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (!cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                byte[] receivedData = await Task.Run(() => 
                {
                    return client.Receive(ref remoteEndPoint);
                });

                ParseData(receivedData);
                Debug.Log($"Received data from {remoteEndPoint.Address}:{remoteEndPoint.Port}");
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.Interrupted)
                {
                    // Socket was closed, exit gracefully
                    break;
                }
                Debug.LogError($"Socket error: {e.Message}");
            }
            catch (ObjectDisposedException)
            {
                // UdpClient was disposed, exit gracefully
                break;
            }
            catch (Exception e)
            {
                Debug.LogError($"UDP Receive Error: {e.Message}");
            }
        }
    }

    private void ParseData(byte[] data)
    {
        string[] values = System.Text.Encoding.UTF8.GetString(data).Split(',');
        
        if (values.Length >= 25)
        {
            for (int i = 1; i < 25; i++)
            {
                if (float.TryParse(values[i], out float result))
                {
                    AnimalData[i] = result;
                }
                else
                {
                    Debug.LogWarning($"Failed to parse value at index {i}: {values[i]}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"Received data has incorrect format. Expected 25 values, got {values.Length}");
        }
    }

    private void OnDisable()
    {
        StopReceiving();
    }

    private void StopReceiving()
    {
        cancellationTokenSource?.Cancel();
        client?.Close();
        client = null;
    }
}