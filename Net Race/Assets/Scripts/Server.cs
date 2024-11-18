using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;
using System.Collections.Generic;

public class PlayerInfo
{
    public string playerID;
    public GameObject playerGO;
    public string playerName; 

    public PlayerInfo(string id, GameObject go, string name)
    {
        playerID = id;
        playerGO = go;
        playerName = name;
    }
}

public class Server : MonoBehaviour
{
    Socket socket;
    public GameObject playerPrefab;
    private IPEndPoint serverEndPoint;
    private string map;

    public GameObject prefabMap1;
    public GameObject prefabMap2;

    private GameObject currentPlayer;
    private GameObject playerObject;

    private List<PlayerInfo> players;

    // Flags and data for main-thread updates
    private bool spawnRequested = false;
    private bool positionUpdateRequested = false;
    private Vector3 newPosition;
    private Quaternion newRotation;
    private IPEndPoint lastClientEndpoint;

    void Start()
    {
        players = new List<PlayerInfo>();
        map = "Lobby";
        serverEndPoint = new IPEndPoint(IPAddress.Any, 9050);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(serverEndPoint);

        Thread receiveThread = new Thread(Receive);
        receiveThread.Start();
    }

    void Update()
    {
        if (spawnRequested)
        {
            SpawnPlayer();
            spawnRequested = false;
        }

        // Apply position updates on main thread if Player object exists
        if (positionUpdateRequested && playerObject != null)
        {
            playerObject.transform.position = newPosition;
            playerObject.transform.rotation = newRotation;
            Debug.Log($"Updated player position to: {newPosition} and rotation to: {newRotation}");
            positionUpdateRequested = false;
        }
    }

    void Receive()
    {
        byte[] data = new byte[1024];
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        EndPoint remoteEndPoint = (EndPoint)sender;

        while (true)
        {
            try
            {
                int recv = socket.ReceiveFrom(data, ref remoteEndPoint);
                string message = Encoding.ASCII.GetString(data, 0, recv);

                Debug.Log($"Received message: {message} from {remoteEndPoint}");

                lastClientEndpoint = (IPEndPoint)remoteEndPoint;

                if (message == "Ping")
                {
                    // Respond with Pong when ping is received
                    byte[] response = Encoding.ASCII.GetBytes("Pong");
                    socket.SendTo(response, remoteEndPoint);
                    Debug.Log("Sent Pong to client.");
                }
                else if (message == "MapRequest")
                {
                    byte[] response = Encoding.ASCII.GetBytes(map);
                    socket.SendTo(response, remoteEndPoint);
                    Debug.Log($"Sent map: {map}");
                }
                else if (message == "Spawn")
                {
                    byte[] response = Encoding.ASCII.GetBytes("SpawnReceived");
                    socket.SendTo(response, remoteEndPoint);
                    // Set flag to request player spawn on main thread
                    spawnRequested = true;
                }
                else if (message.StartsWith("Position:"))
                {
                    ParsePositionAndRotation(message);
                    positionUpdateRequested = true; // Flag to apply in Update
                }
            }
            catch (SocketException ex)
            {
                Debug.LogError($"Error receiving data: {ex.Message}");
            }
        }
    }

    void SpawnPlayer()
    {
        if (playerPrefab != null && currentPlayer == null) // Ensure only one player is spawned
        {
            Vector3 spawnPosition = new Vector3(0, 1, 0);
            Quaternion spawnRotation = Quaternion.identity;

            currentPlayer = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            playerObject = currentPlayer.transform.Find("Player")?.gameObject;

            if (playerObject != null)
            {
                Debug.Log("Player spawned and Player child found.");
            }
            else
            {
                Debug.LogError("No 'Player' child found in the instantiated prefab.");
            }
        }
        else if (currentPlayer != null)
        {
            Debug.LogWarning("Player already spawned.");
        }
        else
        {
            Debug.LogError("Player prefab is not assigned!");
        }
    }

    void ParsePositionAndRotation(string message)
    {
        string[] parts = message.Split(' ');
        if (parts.Length >= 2)
        {
            string positionString = parts[0].Replace("Position:", "");
            string[] positionValues = positionString.Split('.');

            // Check if we have exactly 3 position values
            if (positionValues.Length == 3)
            {
                if (float.TryParse(positionValues[0], out float x) &&
                    float.TryParse(positionValues[1], out float y) &&
                    float.TryParse(positionValues[2], out float z))
                {
                    newPosition = new Vector3(x, y, z);

                    string rotationString = parts[1].Replace("Rotation:", "");
                    string[] rotationValues = rotationString.Split('.');

                    if (rotationValues.Length == 3 &&
                        float.TryParse(rotationValues[0], out float rotX) &&
                        float.TryParse(rotationValues[1], out float rotY) &&
                        float.TryParse(rotationValues[2], out float rotZ))
                    {
                        newRotation = Quaternion.Euler(rotX, rotY, rotZ);
                    }
                    else
                    {
                        Debug.LogError($"Invalid rotation data: {rotationString}");
                    }
                }
                else
                {
                    Debug.LogError($"Invalid position data values: {positionString}");
                }
            }
            else
            {
                Debug.LogWarning($"Unexpected number of position values. Raw data: {positionString}");
            }
        }
        else
        {
            Debug.LogError($"Invalid message format for position and rotation. Raw message: {message}");
        }
    }

    private void AddPlayer(string playerID, GameObject playerGO, string playerName)
    {
        PlayerInfo newPlayer = new PlayerInfo(playerID, playerGO, playerName);
        players.Add(newPlayer);
        Debug.Log($"Player {playerName} with ID {playerID} added.");
    }
    private void RemovePlayer(string playerID)
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
