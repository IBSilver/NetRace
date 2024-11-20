using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Threading;
using System.Collections;
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

    //Maps, made this way so they can be deleted easily
    private GameObject map1;
    private GameObject map2;

    private List<PlayerInfo> players;

    //UI
    public GameObject playerNameUI;
    public InputField playerNameInputField;
    public InputField serverIPInputField;
    public Button confirmNameButton;

    private string playerID;
    private string playerName;
    private string map = "Lobby";

    private readonly object playersLock = new object();

    //Used to avoid error when Instantiating prefab not in the main thread
    private readonly Queue<System.Action> mainThreadActions = new Queue<System.Action>();

    private readonly object actionsLock = new object();

    void Start()
    {
        prefabMap2.transform.position = new Vector3(0, -16.5f, -67f);
        prefabMap2.transform.rotation = Quaternion.Euler(0, 0, 0);
        players = new List<PlayerInfo>();
        playerID = GenerateRandomPlayerID();
        playerName = GenerateRandomPlayerName();

        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.ReceiveTimeout = 1000;

        Thread receiveThread = new Thread(Receive);
        receiveThread.Start();

        confirmNameButton.onClick.AddListener(SetServerIPAndName);

        InvokeRepeating(nameof(CheckForServer), 1, 1);
        InvokeRepeating(nameof(SendMapRequest), 1, 5);
        InvokeRepeating(nameof(SendPositionAndRotation), 1, 0.016f);
        InvokeRepeating(nameof(SendPlayerInfo), 2f, 1);
    }

    void Update()
    {
        ProcessMainThreadActions();

        if (spawnOnMainThread && instantiatedPrefab == null && mapLoaded == true)
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

        //Check if a player has fallen to teleport him/her back
        CheckPlayerAltitude();
    }
    //Function used to avoid error when Instantiating prefab not in the main thread
    void EnqueueMainThreadAction(System.Action action)
    {
        lock (actionsLock)
        {
            mainThreadActions.Enqueue(action);
        }
    }
    //Function used to avoid error when Instantiating prefab not in the main thread
    void ProcessMainThreadActions()
    {
        lock (actionsLock)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue()?.Invoke();
            }
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

        string message = $"ID:{playerID} P:{position.x}.{position.y}.{position.z} R:{rotation.eulerAngles.x}.{rotation.eulerAngles.y}.{rotation.eulerAngles.z}";

        byte[] data = Encoding.ASCII.GetBytes(message);
        socket.SendTo(data, serverEndpoint);
        Debug.Log($"Sent position and rotation with ID: {message}");
    }
    private string GenerateRandomPlayerID()
    {
        return $"{Random.Range(1000, 99999)}";
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
                                string receivedPlayerID = parts[1];
                                string receivedPlayerName = parts[2];

                                EnqueueMainThreadAction(() =>
                                {
                                    if (!players.Exists(p => p.playerID == receivedPlayerID))
                                    {
                                        GameObject newPlayerGO = Instantiate(remotePlayerPrefab, Vector3.zero, Quaternion.identity);
                                        AddPlayer(receivedPlayerID, newPlayerGO, receivedPlayerName);
                                    }
                                });
                            }
                        }
                        else if (message == "Lobby")
                        {
                            EnqueueMainThreadAction(() => mapToLoad = 0);
                            map = message;
                        }
                        else if (message == "FirstMap")
                        {
                            EnqueueMainThreadAction(() => mapToLoad = 1);
                            map = message;
                        }
                        else if (message.StartsWith("ID:"))
                        {
                            EnqueueMainThreadAction(() => HandlePlayers(message));
                        }
                        else if (message.StartsWith("PlayerRemoved:"))
                        {
                            string removedPlayerID = message.Replace("PlayerRemoved:", "").Trim();

                            EnqueueMainThreadAction(() =>
                            {
                                RemovePlayerCompletely(removedPlayerID);
                            });
                        }
                        else if (message.StartsWith("PlayerNameUpdate:"))
                        {
                            ChangePlayerName(message);
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
    //Change player name
    void ChangePlayerName(string message)
    {
        string[] parts = message.Split(':');
        if (parts.Length == 3)
        {
            string playerID = parts[1];
            string newPlayerName = parts[2];

            //To avoid error since its not in the main thread
            EnqueueMainThreadAction(() =>
            {
                PlayerInfo player = players.Find(p => p.playerID == playerID);

                if (player != null)
                {
                    player.playerName = newPlayerName;
                    Debug.Log($"Player name for {playerID} updated to {newPlayerName}");

                    if (player.playerGO != null)
                    {
                        player.playerGO.name = newPlayerName;
                        Debug.Log($"GameObject name for {playerID} updated to {newPlayerName}");
                    }
                    else
                    {
                        Debug.LogWarning($"Player GameObject for ID {playerID} not found.");
                    }
                }
                else
                {
                    Debug.LogWarning($"Player with ID {playerID} not found for name change.");
                }
            });
        }
        else
        {
            Debug.LogError($"Invalid message format for PlayerNameUpdate: {message}");
        }
    }

    //Removing player
    void RemovePlayerCompletely(string playerID)
    {
        lock (playersLock)
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
                Debug.Log($"Player {playerToRemove.playerName} (ID: {playerID}) removed from the client.");
            }
            else
            {
                Debug.LogWarning($"Attempted to remove player with ID {playerID}, but they were not found.");
            }
        }
    }
    void HandlePlayers(string message)
    {
        string[] parts = message.Split(' ');
        if (parts.Length >= 3)
        {
            string idPart = parts[0].Replace("ID:", "");

            // Skip if the ID matches the local client's player ID
            if (idPart == playerID)
                return;

            string positionString = parts[1].Replace("P:", "");
            string[] positionValues = positionString.Split('.');
            if (positionValues.Length == 3 &&
                float.TryParse(positionValues[0], out float x) &&
                float.TryParse(positionValues[1], out float y) &&
                float.TryParse(positionValues[2], out float z))
            {
                Vector3 newPosition = new Vector3(x, y, z);

                string rotationString = parts[2].Replace("R:", "");
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
        if (serverEndpoint == null)
        {
            Debug.LogWarning("Server IP not set. Skipping server check.");
            return;
        }

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
        if (mapId == 0)
        {
            if (map1 == null)
            {
                map1 = Instantiate(prefabMap1);
            }
            if (map2 != null)
            {
                Destroy(map2);
                map2 = null; 
            }
        }
        else if (mapId == 1)
        {
            if (map2 == null)
            {
                map2 = Instantiate(prefabMap2);
            }
            if (map1 != null)
            {
                Destroy(map1);
                map1 = null;
            }
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

    //UI
    public void SetPlayerName()
    {
        string newName = playerNameInputField.text;

        if (!string.IsNullOrEmpty(newName))
        {
            playerName = newName;
            Debug.Log($"Player name set to: {playerName}");

            string message = $"PlayerNameUpdate:{playerID}:{playerName}";
            byte[] data = Encoding.ASCII.GetBytes(message);
            socket.SendTo(data, serverEndpoint);

            playerNameUI.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Player name is empty. Please enter a valid name.");
        }
    }

    public void SetServerIPAndName()
    {
        string newIP = serverIPInputField.text;

        if (IPAddress.TryParse(newIP, out IPAddress ipAddress))
        {
            serverEndpoint = new IPEndPoint(ipAddress, 9050);
            confirmNameButton.interactable = false;
            StartCoroutine(WaitForServerAndSetName());
        }
    }

    private IEnumerator WaitForServerAndSetName()
    {
        while (!serverConnected)
        {
            yield return new WaitForSeconds(0.5f);
        }

        SetPlayerName();
    }

    //Was a huge headache, could teleport the player back because of the CharacterController problem, took hours to determine the cause
    void CheckPlayerAltitude()
    {
        if (instantiatedPrefab != null)
        {
            Transform playerGOTransform = instantiatedPrefab.transform.Find("PlayerGO");

            if (playerGOTransform != null && playerGOTransform.transform.position.y < -100)
            {
                CharacterController charController = playerGOTransform.GetComponent<CharacterController>();

                if (charController != null)
                {
                    charController.enabled = false;

                    playerGOTransform.position = new Vector3(0, 1, -23);


                    charController.enabled = true;
                }
                else
                {
                    playerGOTransform.position = new Vector3(0, 1, -23);
                    Debug.Log($"PlayerGO moved to: {playerGOTransform.position}");
                }
            }
        }
    }
}
