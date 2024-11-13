using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;

public class Client : MonoBehaviour
{
    Socket socket;
    IPEndPoint serverEndpoint;
    private bool serverConnected = false;
    private bool mapLoaded = false;
    private bool spawnOnMainThread = false;
    private bool auxSpawn = false;

    private int? mapToLoad = null; // This flag will store which map to load on the main thread

    public GameObject prefab;
    public GameObject prefabMap1;
    public GameObject prefabMap2;

    private GameObject instantiatedPrefab;

    void Start()
    {
        serverEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.ReceiveTimeout = 1000;

        Thread receiveThread = new Thread(Receive);
        receiveThread.Start();

        InvokeRepeating(nameof(CheckForServer), 1, 1);
        InvokeRepeating(nameof(SendMapRequest), 1, 5);
        InvokeRepeating(nameof(SendPositionAndRotation), 1, 0.1f);
    }

    void Update()
    {
        if (serverConnected && mapLoaded && !spawnOnMainThread && !auxSpawn)
        {
            SendSpawnRequest();
            auxSpawn = true;
        }

        if (spawnOnMainThread)
        {
            if (prefab != null && auxSpawn)
            {
                // Instantiate prefab and store reference to instantiatedPrefab
                instantiatedPrefab = Instantiate(prefab, new Vector3(0, 1, 0), Quaternion.identity);
                auxSpawn = false;
            }
            else if (auxSpawn)
            {
                Debug.LogError("Prefab is not assigned!");
            }
        }

        // Check if a map needs to be loaded
        if (mapToLoad.HasValue)
        {
            InstantiateMap(mapToLoad.Value);
            mapLoaded = true;
            mapToLoad = null; // Reset after loading
        }
    }

    void SendMapRequest()
    {
        if (mapLoaded || !serverConnected) return;

        try
        {
            string message = "MapRequest";
            byte[] data = Encoding.ASCII.GetBytes(message);
            socket.SendTo(data, serverEndpoint);
            Debug.Log("Map request sent to server");
        }
        catch (SocketException ex)
        {
            Debug.LogError($"SocketException on Send: {ex.Message}");
        }
    }

    void SendSpawnRequest()
    {
        try
        {
            string message = "Spawn";
            byte[] data = Encoding.ASCII.GetBytes(message);
            socket.SendTo(data, serverEndpoint);
            Debug.Log("Spawn request sent to server");
        }
        catch (SocketException ex)
        {
            Debug.LogError($"SocketException on Send: {ex.Message}");
        }
    }

    void Receive()
    {
        byte[] data = new byte[1024];

        while (true)
        {
            if (serverConnected)
            {
                try
                {
                    int recv = socket.Receive(data);

                    if (recv > 0)
                    {
                        string message = Encoding.ASCII.GetString(data, 0, recv).Trim();
                        Debug.Log($"Received message: {message}");

                        if (message == "SpawnReceived")
                        {
                            spawnOnMainThread = true;
                        }
                        else if (message == "Lobby")
                        {
                            mapToLoad = 0;
                        }
                        else if (message == "FirstMap")
                        {
                            mapToLoad = 1;
                        }
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.TimedOut)
                    {
                        Debug.LogError($"Error receiving data: {ex.Message}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Unexpected error in Receive: {ex.Message}");
                }
            }
            else
            {
                Thread.Sleep(100);
            }
        }
    }

    void SendPositionAndRotation()
    {
        GameObject player;

        if (instantiatedPrefab == null)
        {
            Debug.LogError("Instantiated prefab is null, cannot send position and rotation.");
            return;
        }
        else
        {
            player = instantiatedPrefab.transform.Find("PlayerGO")?.gameObject;
        }

        Vector3 position = player.transform.position;
        Quaternion rotation = player.transform.rotation;

        string message = $"Position:{position.x}.{position.y}.{position.z} Rotation:{rotation.eulerAngles.x}.{rotation.eulerAngles.y}.{rotation.eulerAngles.z}";

        byte[] data = Encoding.ASCII.GetBytes(message);
        socket.SendTo(data, serverEndpoint);
        Debug.Log($"Sent position and rotation: {message}");
    }

    void CheckForServer()
    {
        try
        {
            if (!serverConnected)
            {
                string message = "Ping";
                byte[] data = Encoding.ASCII.GetBytes(message);
                socket.SendTo(data, serverEndpoint);
                Debug.Log("Ping sent to server");
                ReceiveResponse();
            }
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode != SocketError.HostUnreachable)
            {
                Debug.LogWarning($"Error pinging server: {ex.Message}");
            }
        }
    }

    void ReceiveResponse()
    {
        byte[] data = new byte[1024];
        try
        {
            int recv = socket.Receive(data);
            string response = Encoding.ASCII.GetString(data, 0, recv).Trim();
            Debug.Log("Response received: " + response);

            if (response == "Pong")
            {
                serverConnected = true;
                Debug.Log("Server connected: Pong received");
            }
        }
        catch (SocketException ex)
        {
            Debug.LogWarning($"Response not received: {ex.Message}");
        }
    }

    void InstantiateMap(int mapId)
    {
        switch (mapId)
        {
            case 0:
                Instantiate(prefabMap1);
                if (prefabMap1 != null)
                {
                    Destroy(prefabMap2);
                }
                break;
            case 1:
                Instantiate(prefabMap2);
                if (prefabMap2 != null)
                {
                    Destroy(prefabMap2);
                }
                break;
        }
    }
}
