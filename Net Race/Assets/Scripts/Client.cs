﻿using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;
using System.Collections.Generic;

public class Client : MonoBehaviour
{
    Socket socket;
    IPEndPoint serverEndpoint;
    private bool serverConnected = false;
    private bool mapLoaded = false;
    private bool spawnOnMainThread = true;

    private int? mapToLoad = null; // This flag will store which map to load on the main thread

    public GameObject prefabMap1;
    public GameObject prefabMap2;

    public GameObject localPlayerPrefab;
    public GameObject remotePlayerPrefab;

    private GameObject instantiatedPrefab;

    private List<PlayerInfo> players;

    private string playerID;
    private string playerName;

    private readonly object playersLock = new object();

    void Start()
    {
        players = new List<PlayerInfo>();
        playerID = GenerateRandomPlayerID();
        playerName = GenerateRandomPlayerName();
        serverEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.ReceiveTimeout = 1000;

        Thread receiveThread = new Thread(Receive);
        receiveThread.Start();

        InvokeRepeating(nameof(CheckForServer), 1, 1);
        InvokeRepeating(nameof(SendMapRequest), 1, 5);
        InvokeRepeating(nameof(SendPositionAndRotation), 1, 0.1f);
        InvokeRepeating(nameof(SendPlayerInfo), 2f, 1);
    }

    void Update()
    {
        if (spawnOnMainThread && instantiatedPrefab == null &&mapLoaded == true)
        {
            instantiatedPrefab = Instantiate(localPlayerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
            spawnOnMainThread = false;
        }

        if (mapToLoad.HasValue)
        {
            InstantiateMap(mapToLoad.Value);
            mapLoaded = true;
            mapToLoad = null; // Reset after loading
        }
    }
    void SendPositionAndRotation()
    {
        if (instantiatedPrefab == null)
        {
            Debug.LogError("Instantiated prefab is null, cannot send position and rotation.");
            return;
        }

        GameObject player = instantiatedPrefab.transform.Find("PlayerGO")?.gameObject;

        if (player == null)
        {
            Debug.LogError("PlayerGO is not found in the instantiated prefab.");
            return;
        }

        Vector3 position = player.transform.position;
        Quaternion rotation = player.transform.rotation;

        string message = $"ID:{playerID} Position:{position.x}.{position.y}.{position.z} Rotation:{rotation.eulerAngles.x}.{rotation.eulerAngles.y}.{rotation.eulerAngles.z}";

        byte[] data = Encoding.ASCII.GetBytes(message);
        socket.SendTo(data, serverEndpoint);
        Debug.Log($"Sent position and rotation with ID: {message}");
    }
    private string GenerateRandomPlayerID()
    {
        return $"Player_{Random.Range(1000, 99999)}";
    }
    private string GenerateRandomPlayerName()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        int nameLength = Random.Range(5, 10);
        StringBuilder nameBuilder = new StringBuilder();

        for (int i = 0; i < nameLength; i++)
        {
            char randomChar = chars[Random.Range(0, chars.Length)];
            nameBuilder.Append(randomChar);
        }

        return nameBuilder.ToString();
    }

    void SendPlayerInfo()
    {
        if (serverConnected)
        {
            string message = $"PlayerInfo:{playerID}:{playerName}";
            byte[] data = Encoding.ASCII.GetBytes(message);
            socket.SendTo(data, serverEndpoint);
            Debug.Log($"Sent player info: {message}");
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

                        if (message.StartsWith("PlayerInfo:"))
                        {
                            string[] parts = message.Split(':');
                            if (parts.Length == 3)
                            {
                                string playerID = parts[1];
                                string playerName = parts[2];

                                // Check if the player already exists
                                if (!players.Exists(p => p.playerID == playerID))
                                {
                                    GameObject newPlayerGO = Instantiate(remotePlayerPrefab, Vector3.zero, Quaternion.identity);
                                    AddPlayer(playerID, newPlayerGO, playerName);
                                }
                            }
                        }
                        else if (message == "Lobby")
                        {
                            mapToLoad = 0;
                        }
                        else if (message == "FirstMap")
                        {
                            mapToLoad = 1;
                        }
                        else if (message.StartsWith("ID:"))
                        {
                            HandlePlayers(message);
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
    void HandlePlayers(string message)
    {
        // Split the message by space to extract ID, Position, and Rotation
        string[] parts = message.Split(' ');
        if (parts.Length >= 3)
        {
            // Extract player ID
            string idPart = parts[0].Replace("ID:", "");

            // Skip if the ID matches the local client's player ID
            if (idPart == playerID)
                return;

            // Extract position
            string positionString = parts[1].Replace("Position:", "");
            string[] positionValues = positionString.Split('.');
            if (positionValues.Length == 3 &&
                float.TryParse(positionValues[0], out float x) &&
                float.TryParse(positionValues[1], out float y) &&
                float.TryParse(positionValues[2], out float z))
            {
                Vector3 newPosition = new Vector3(x, y, z);

                // Extract rotation
                string rotationString = parts[2].Replace("Rotation:", "");
                string[] rotationValues = rotationString.Split('.');
                if (rotationValues.Length == 3 &&
                    float.TryParse(rotationValues[0], out float rotX) &&
                    float.TryParse(rotationValues[1], out float rotY) &&
                    float.TryParse(rotationValues[2], out float rotZ))
                {
                    Quaternion newRotation = Quaternion.Euler(rotX, rotY, rotZ);

                    // Check if the player already exists
                    lock (playersLock)
                    {
                        PlayerInfo existingPlayer = players.Find(p => p.playerID == idPart);
                        if (existingPlayer != null)
                        {
                            // Update position and rotation if the player already exists
                            if (existingPlayer.playerGO != null)
                            {
                                existingPlayer.playerGO.transform.position = newPosition;
                                existingPlayer.playerGO.transform.rotation = newRotation;

                                Debug.Log($"Updated player {existingPlayer.playerName} (ID: {idPart}) to position {newPosition} and rotation {newRotation.eulerAngles}");
                            }
                        }
                        else
                        {
                            // Create a new player
                            GameObject newPlayerGO = Instantiate(remotePlayerPrefab, newPosition, newRotation);
                            PlayerInfo newPlayer = new PlayerInfo(idPart, newPlayerGO, "RemotePlayer");
                            players.Add(newPlayer);

                            Debug.Log($"Instantiated new player with ID: {idPart}");
                        }
                    }
                }
                else
                {
                    Debug.LogError($"Invalid rotation data: {rotationString}");
                }
            }
            else
            {
                Debug.LogError($"Invalid position data: {positionString}");
            }
        }
        else
        {
            Debug.LogError($"Invalid message format for position update: {message}");
        }
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
                if (prefabMap1 != null)
                {
                    Instantiate(prefabMap1);
                    Destroy(prefabMap2);
                }
                break;
            case 1:
                if (prefabMap2 != null)
                {
                    Instantiate(prefabMap2);
                    Destroy(prefabMap1); 
                }
                break;
        }
    }
    void AddPlayer(string playerID, GameObject playerGO, string playerName)
    {
        lock (playersLock)
        {
            PlayerInfo newPlayer = new PlayerInfo(playerID, playerGO, playerName);
            players.Add(newPlayer);
            Debug.Log($"Player {playerName} with ID {playerID} added.");
        }
    }

    void RemovePlayer(string playerID)
    {
        lock (playersLock)
        {
            PlayerInfo playerToRemove = players.Find(player => player.playerID == playerID);
            if (playerToRemove != null)
            {
                players.Remove(playerToRemove);
                Debug.Log($"Player {playerToRemove.playerName} with ID {playerID} removed.");
            }
            else
            {
                Debug.LogWarning($"Player with ID {playerID} not found.");
            }
        }
    }
}
