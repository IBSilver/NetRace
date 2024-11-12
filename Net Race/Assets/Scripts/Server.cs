using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;

public class Server : MonoBehaviour
{
    Socket socket;
    public GameObject playerPrefab;
    private IPEndPoint serverEndPoint;
    private string map;

    public GameObject prefabMap1;
    public GameObject prefabMap2;

    private GameObject currentPlayer; // To store the player instance

    void Start()
    {
        map = "Lobby";
        serverEndPoint = new IPEndPoint(IPAddress.Any, 9050);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(serverEndPoint);

        Thread receiveThread = new Thread(Receive);
        receiveThread.Start();
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
                    // Call the method to spawn the player
                    SpawnPlayer();
                }
                else if (message.StartsWith("Position:"))
                {
                    // Process the position and rotation update
                    HandlePositionAndRotationUpdate(message);
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

            Debug.Log("Player spawned on the server.");
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

    void HandlePositionAndRotationUpdate(string message)
    {
        string[] parts = message.Split(' ');
        if (parts.Length >= 2)
        {
            string positionString = parts[0].Replace("Position:", "");
            string[] positionValues = positionString.Split(',');

            if (positionValues.Length == 3)
            {
                float x = float.Parse(positionValues[0]);
                float y = float.Parse(positionValues[1]);
                float z = float.Parse(positionValues[2]);
                Vector3 position = new Vector3(x, y, z);

                string rotationString = parts[1].Replace("Rotation:", "");
                string[] rotationValues = rotationString.Split(',');

                if (rotationValues.Length == 3)
                {
                    float rotX = float.Parse(rotationValues[0]);
                    float rotY = float.Parse(rotationValues[1]);
                    float rotZ = float.Parse(rotationValues[2]);
                    Quaternion rotation = Quaternion.Euler(rotX, rotY, rotZ);

                    if (currentPlayer != null)
                    {
                        currentPlayer.transform.position = position;
                        currentPlayer.transform.rotation = rotation;
                        Debug.Log($"Updated player position to: {position} and rotation to: {rotation}");
                    }
                    else
                    {
                        Debug.LogWarning("No player instantiated yet.");
                    }
                }
                else
                {
                    Debug.LogError("Invalid rotation data.");
                }
            }
            else
            {
                Debug.LogError("Invalid position data.");
            }
        }
        else
        {
            Debug.LogError("Invalid message format for position and rotation.");
        }
    }
}
