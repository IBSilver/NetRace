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

    public GameObject prefab;
    public GameObject prefabMap1;

    void Start()
    {
        serverEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.ReceiveTimeout = 1000;

        Thread receiveThread = new Thread(Receive);
        receiveThread.Start();

        // Initially, do not ping the server; only check once the connection is confirmed and start checking for server every second
        InvokeRepeating(nameof(CheckForServer), 1, 1);
    }

    void Update()
    {
        if (serverConnected && Input.GetKeyDown(KeyCode.E) && mapLoaded)
        {
            SendSpawnRequest();
        }

        if (serverConnected && !mapLoaded)
        {
            SendMapRequest();
        }

        // Spawn the prefab if we received the "Spawn Cube" command from the server
        if (spawnOnMainThread)
        {
            if (prefab != null)
            {
                Instantiate(prefab, Vector3.zero, Quaternion.identity);
                // Reset the flag to prevent multiple instantiations
                spawnOnMainThread = false;
            }
            else
            {
                Debug.LogError("Prefab is not assigned!");
            }
        }
    }

    void SendMapRequest()
    {
        try
        {
            string message = "MapRequest";
            byte[] data = Encoding.ASCII.GetBytes(message);

            // Send the spawn request to the server
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

            // Send the spawn request to the server
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
                        string message = Encoding.ASCII.GetString(data, 0, recv);
                        if (message == "SpawnReceived")
                        {
                            Debug.Log("Received spawn response from server");
                            // Trigger prefab spawning
                            spawnOnMainThread = true;
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
                // Skip the receive if not connected to the server and wait a bit before checking again
                Thread.Sleep(100);
            }
        }
    }

    // Function to check if the server is reachable
    void CheckForServer()
    {
        try
        {
            // Send a ping to the server
            if (!serverConnected)
            {
                string message = "Ping";
                byte[] data = Encoding.ASCII.GetBytes(message);

                socket.SendTo(data, serverEndpoint);
                Debug.Log("Ping sent to server");

                // Wait for server response
                ReceiveResponse();
            }
        }
        catch (SocketException ex)
        {
            // Catch socket errors when server is unavailable
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
            string response = Encoding.ASCII.GetString(data, 0, recv);
            if (response == "Pong")
            {
                serverConnected = true;
                Debug.Log("Server connected: Pong received");
            }
            else if (response == "SpawnReceived")
            {
                Debug.Log("Server acknowledged spawn request");
            }
            else
            {
                if (response == "0")
                {
                    InstantiateMap(0);
                    mapLoaded = true;
                }
                else if (response == "1")
                {
                    InstantiateMap(1);
                    mapLoaded = true;
                }
            }
        }
        catch (SocketException ex)
        {
            // If we get an error or no response, keep trying
            Debug.LogWarning($"Response not received: {ex.Message}");
        }
    }

    void InstantiateMap(int mapId)
    {
        switch (mapId)
        {
            case 0:
                //DO
                break;
            case 1:
                //DO
                break;
        }
    }
}

