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
    public GameObject StartMap;

    private GameObject currentPlayer;
    private GameObject playerObject;
    private GameObject currentMap;

    public GameObject LobbyTrigger;
    public float activationRadius = 8f;

    private List<PlayerInfo> players;

    private bool instantiatePlayerFlag = false;
    private string playerInfoMessage = "";

    //Had to use this to solve an error with the Instatiate in a thread different from the main
    private Queue<(string playerID, Vector3 newPosition, Quaternion newRotation)> positionUpdateQueue = new Queue<(string, Vector3, Quaternion)>();
    private Queue<string> playerActivityQueue = new Queue<string>();

    private IPEndPoint ipAux = null;

    //Player timeout variables
    private Dictionary<string, float> playerLastActivity = new Dictionary<string, float>();
    // Timeout in seconds
    private float timeoutThreshold = 10f;

    void Start()
    {
        players = new List<PlayerInfo>();
        map = "Lobby";
        serverEndPoint = new IPEndPoint(IPAddress.Any, 9050);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(serverEndPoint);

        Thread receiveThread = new Thread(Receive);
        receiveThread.Start();
        InvokeRepeating(nameof(SendPositionAndRotation), 1, 0.016f);
        InvokeRepeating(nameof(CheckPlayerTimeouts), 1f, 1f);
        InvokeRepeating(nameof(CheckLobbyTriggerConditions), 1f, 2f);

        InvokeRepeating(nameof(SendNames), 1f, 10f);
    }

    void Update()
    {
        // Process position updates
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
            //else
            //{
            //    Debug.LogWarning($"Player with ID {playerID} not found for position update.");
            //}
        }

        // Process player activity updates
        lock (playerActivityQueue)
        {
            while (playerActivityQueue.Count > 0)
            {
                string playerID = playerActivityQueue.Dequeue();
                playerLastActivity[playerID] = Time.time; // Update the last activity timestamp safely on the main thread
            }
        }

        // Handle instantiating player objects
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
                else if (message.StartsWith("PlayerNameUpdate:"))
                {
                    ChangePlayerName(message);
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
        foreach (var player1 in players)
        {
            foreach (var player in players)
            {
                //Best would be to not send the data from a player to the same player but it will be left like this until a good solution is found --- TODO!!!
                Vector3 position = player.playerGO.transform.position;
                Quaternion rotation = player.playerGO.transform.rotation;

                string message = $"ID:{player.playerID} Position:{position.x}.{position.y}.{position.z} Rotation:{rotation.eulerAngles.x}.{rotation.eulerAngles.y}.{rotation.eulerAngles.z}";

                byte[] data = Encoding.ASCII.GetBytes(message);
                socket.SendTo(data, player1.ip);
            }
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

                    UpdatePlayerActivity(idPart);
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

    //Player timeout functions
    void CheckPlayerTimeouts()
    {
        float currentTime = Time.time;

        foreach (var player in players)
        {
            if (playerLastActivity.TryGetValue(player.playerID, out float lastActivityTime))
            {
                if (currentTime - lastActivityTime > timeoutThreshold)
                {
                    Debug.Log($"Player {player.playerName} (ID: {player.playerID}) is inactive for {timeoutThreshold} seconds.");
                    RemovePlayerCompletely(player.playerID);
                }
            }
            else
            {
                Debug.LogWarning($"No activity record found for player {player.playerName} (ID: {player.playerID}).");
            }
        }
    }

    public void UpdatePlayerActivity(string playerID)
    {
        lock (playerActivityQueue)
        {
            playerActivityQueue.Enqueue(playerID); // Queue the update for the main thread
        }
    }
    //Deleting players
    private void RemovePlayerCompletely(string playerID)
    {
        PlayerInfo playerToRemove = players.Find(player => player.playerID == playerID);

        if (playerToRemove != null)
        {
            if (playerToRemove.playerGO != null)
            {
                Destroy(playerToRemove.playerGO);
                Debug.Log($"Destroyed game object for player {playerToRemove.playerName} (ID: {playerID}).");
            }

            players.Remove(playerToRemove);
            Debug.Log($"Player {playerToRemove.playerName} (ID: {playerID}) removed from the server.");

            if (playerLastActivity.ContainsKey(playerID))
            {
                playerLastActivity.Remove(playerID);
                Debug.Log($"Removed activity tracking for player {playerToRemove.playerName} (ID: {playerID}).");
            }

            BroadcastPlayerRemoval(playerID);
        }
        else
        {
            Debug.LogWarning($"Attempted to remove player with ID {playerID}, but they were not found.");
        }
    }

    //Notifying other players about the removal
    private void BroadcastPlayerRemoval(string playerID)
    {
        string message = $"PlayerRemoved:{playerID}";
        byte[] data = Encoding.ASCII.GetBytes(message);

        foreach (var player in players)
        {
            socket.SendTo(data, player.ip);
        }

        Debug.Log($"Notified all players about the removal of player with ID: {playerID}.");
    }

    //Change player name in server and broadcast it to all clients
    private void ChangePlayerName(string message)
    {
        string[] parts = message.Split(':');
        if (parts.Length == 3)
        {
            string playerID = parts[1];
            string newPlayerName = parts[2];

            PlayerInfo player = players.Find(p => p.playerID == playerID);

            if (player != null)
            {
                player.playerName = newPlayerName;

                Debug.Log($"Player name for {playerID} updated to {newPlayerName}");

                // Notify all players about the name change
                string nameUpdateMessage = $"PlayerNameUpdate:{playerID}:{newPlayerName}";
                byte[] data = Encoding.ASCII.GetBytes(nameUpdateMessage);

                // Broadcast the updated name to all players
                foreach (var p in players)
                {
                    socket.SendTo(data, p.ip);
                }
            }
            else
            {
                Debug.LogWarning($"Player with ID {playerID} not found for name change.");
            }
        }
        else
        {
            Debug.LogError($"Invalid message format for PlayerNameUpdate: {message}");
        }
    }

    //LobbyToMap
    void CheckLobbyTriggerConditions()
    {
        if (map == "Lobby")
        {
            List<PlayerInfo> playersInRange = new List<PlayerInfo>();

            int totalPlayers = players.Count;

            // Check if each player is within the radius of the LobbyTrigger
            foreach (var player in players)
            {
                if (Vector3.Distance(player.playerGO.transform.position, LobbyTrigger.transform.position) <= activationRadius)
                {
                    playersInRange.Add(player);
                }
            }

            // Condition: Exactly 2 players are in range and all players must be inside the radius
            if (playersInRange.Count >= 2 && playersInRange.Count == totalPlayers)
            {
                // Trigger the map change and notify all players
                TriggerMapChange("FirstMap");
            }
        }
    }
    void TriggerMapChange(string mapName)
    {
        // Broadcast message to all players to change the map
        string message = $"{mapName}";
        byte[] data = Encoding.ASCII.GetBytes(message);
        map = mapName;

        foreach (var player in players)
        {
            socket.SendTo(data, player.ip);  // Send the map change message to each player
        }

        if (mapName == "FirstMap")
        {
            //Set red Trigger to false
            LobbyTrigger.SetActive(false);
            // Destroy StartMap if it exists
            if (StartMap != null)
            {
                Destroy(StartMap);
                StartMap = null;
                Debug.Log("StartMap GameObject destroyed.");
            }

            if (currentMap == null)
            {
                if (prefabMap2 != null)
                {
                    currentMap = Instantiate(prefabMap2, new Vector3(0, -16.5f, -86f), Quaternion.identity);
                    Debug.Log("Instantiated prefabMap2.");
                }
                else
                {
                    Debug.LogError("prefabMap2 is not assigned in the inspector.");
                }
            }
        }
    }

    //Send all names to all clients
    void SendNames()
    {
        foreach (var player in players)
        {
            string playerID = player.playerID;
            string playerName = player.playerName;
            string namesMessage = $"PlayerNameUpdate:{playerID}:{playerName}";
            ChangePlayerName(namesMessage);
        }
    }
}
