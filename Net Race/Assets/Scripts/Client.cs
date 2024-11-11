using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;

public class Client : MonoBehaviour
{
    Socket socket;
    bool connected = false;

    void Start()
    {
        StartClient();
    }

    public void StartClient()
    {
        // Loop the send attempts until connected
        Thread sendThread = new Thread(SendHandshake);
        sendThread.IsBackground = true;
        sendThread.Start();
    }

    void SendHandshake()
    {
        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        string handshake = "Hello World";
        byte[] data = Encoding.ASCII.GetBytes(handshake);

        while (!connected)
        {
            try
            {
                // Send the handshake message to server
                socket.SendTo(data, serverEndpoint);
                Debug.Log("Handshake sent, awaiting response...");

                // Start listening for the server's response
                Receive();
            }
            catch (SocketException e)
            {
                Debug.Log($"Error sending handshake: {e.Message}");
                // Wait before retrying
                Thread.Sleep(1000);
            }
        }
    }

    void Receive()
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        EndPoint remoteEndPoint = (EndPoint)sender;
        byte[] data = new byte[1024];

        while (true)
        {
            int recv = socket.ReceiveFrom(data, ref remoteEndPoint);
            Debug.Log($"Message received from {remoteEndPoint}: {Encoding.ASCII.GetString(data, 0, recv)}");
            // Mark connection as established
            connected = true;
        }
    }
}
