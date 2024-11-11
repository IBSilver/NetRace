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

    void Start()
    {
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
                else if (message == "Spawn")
                {
                    byte[] response = Encoding.ASCII.GetBytes("SpawnReceived");
                    socket.SendTo(response, remoteEndPoint);
                    // Call the method to spawn the player
                    SpawnPlayer();
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
        if (playerPrefab != null)
        {
            Vector3 spawnPosition = Vector3.zero;
            Quaternion spawnRotation = Quaternion.identity;

            Instantiate(playerPrefab, spawnPosition, spawnRotation);

            Debug.Log("Player spawned on the server.");
        }
        else
        {
            Debug.LogError("Player prefab is not assigned!");
        }
    }
}
