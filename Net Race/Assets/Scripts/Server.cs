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
    public IPEndPoint ip;

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

    private bool instantiatePlayerFlag = false;
    private string playerInfoMessage = "";

    private Queue<(string playerID, Vector3 newPosition, Quaternion newRotation)> positionUpdateQueue = new Queue<(string, Vector3, Quaternion)>();
    private IPEndPoint ipAux = null;

    void Start()
    {
        players = new List<PlayerInfo>();
        map = "Lobby";
        serverEndPoint = new IPEndPoint(IPAddress.Any, 9050);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(serverEndPoint);

        Thread receiveThread = new Thread(Receive);
        receiveThread.Start();
        InvokeRepeating(nameof(SendPositionAndRotation), 1, 0.1f);

    }

    void Update()
    {
        // Process position updates from the queue
        while (positionUpdateQueue.Count > 0)
        {
            var update = positionUpdateQueue.Dequeue();
            string playerID = update.playerID;
            Vector3 newPosition = update.newPosition;
            Quaternion newRotation = update.newRotation;

            PlayerInfo player = players.Find(p => p.playerID == playerID);

            if (player != null)
            {
                GameObject playerGO = player.playerGO;

                if (playerGO != null)
                {
                    playerGO.transform.position = newPosition;
                    playerGO.transform.rotation = newRotation;

                    Debug.Log($"Updated player {player.playerName} (ID: {playerID}) to position {newPosition} and rotation {newRotation.eulerAngles}");
                }
            }
            else
            {
                Debug.LogWarning($"Player with ID {playerID} not found for position update.");
            }
        }

        // If the flag is set, instantiate the player and reset the flag
        if (instantiatePlayerFlag)
        {
            HandlePlayerInfo(playerInfoMessage);
            instantiatePlayerFlag = false; // Reset the flag
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

                if (message.StartsWith("PlayerInfo:"))
                {
                    playerInfoMessage = message;
                    instantiatePlayerFlag = true;
                    ipAux = remoteEndPoint as IPEndPoint;
                }
                else if (message == "Ping")
                {
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
                else if (message.StartsWith("ID:"))
                {
                    ParsePositionWithID(message);
                }
            }
            catch (SocketException ex)
            {
                Debug.LogError($"Error receiving data: {ex.Message}");
            }
        }
    }

    void SendPositionAndRotation()
    {
        foreach (var player in players)
        {
            Vector3 position = player.playerGO.transform.position;
            Quaternion rotation = player.playerGO.transform.rotation;

            string message = $"ID:{player.playerID} Position:{position.x}.{position.y}.{position.z} Rotation:{rotation.eulerAngles.x}.{rotation.eulerAngles.y}.{rotation.eulerAngles.z}";

            byte[] data = Encoding.ASCII.GetBytes(message);
            socket.SendTo(data, player.ip);

        }
    }

    void UpdatePlayerPosition(string playerID, Vector3 newPosition, Quaternion newRotation)
    {
        // Enqueue the update to be processed on the main thread
        lock (positionUpdateQueue)
        {
            positionUpdateQueue.Enqueue((playerID, newPosition, newRotation));
        }
    }
    void HandlePlayerInfo(string message)
    {
        string[] parts = message.Split(':');
        if (parts.Length == 3 && parts[0] == "PlayerInfo")
        {
            string playerID = parts[1];
            string playerName = parts[2];

            if (players.Exists(player => player.playerID == playerID))
            {
                Debug.LogWarning($"Player with ID {playerID} already exists. Ignoring.");
                return;
            }

            GameObject newPlayerGO = Instantiate(playerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
            AddPlayer(playerID, newPlayerGO, playerName);

            Debug.Log($"Added player: {playerName} with ID: {playerID}");
        }
        else
        {
            Debug.LogError($"Invalid PlayerInfo message: {message}");
        }
    }
    void ParsePositionWithID(string message)
    {
        // Split by space to get Position and Rotation parts
        string[] parts = message.Split(' ');

        if (parts.Length >= 2)
        {

            string idPart = parts[0].Replace("ID:", "");
            string positionString = parts[1].Replace("Position:", "");

          
            string[] positionValues = positionString.Split('.');

            if (positionValues.Length == 3 &&
                float.TryParse(positionValues[0], out float x) &&
                float.TryParse(positionValues[1], out float y) &&
                float.TryParse(positionValues[2], out float z))
            {
                Vector3 newPosition = new Vector3(x, y, z);

                
                string rotationString = parts[2].Replace("Rotation:", "");
                string[] rotationValues = rotationString.Split('.');

                if (rotationValues.Length == 3 &&
                    float.TryParse(rotationValues[0], out float rotX) &&
                    float.TryParse(rotationValues[1], out float rotY) &&
                    float.TryParse(rotationValues[2], out float rotZ))
                {
                    Quaternion newRotation = Quaternion.Euler(rotX, rotY, rotZ);

                    UpdatePlayerPosition(idPart, newPosition, newRotation);
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
    private void AddPlayer(string playerID, GameObject playerGO, string playerName)
    {
        PlayerInfo newPlayer = new PlayerInfo(playerID, playerGO, playerName);
        newPlayer.ip = ipAux;
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
