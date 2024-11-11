using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;

public class Server : MonoBehaviour
{
    Socket socket;

    void Start()
    {
        StartServer();
    }

    public void StartServer()
    {
        Debug.Log("Starting UDP Server...");

        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, 9050);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(ipep);

        Thread receiveThread = new Thread(Receive);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void Receive()
    {
        byte[] data = new byte[1024];
        Debug.Log("Waiting for client...");

        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        EndPoint remote = (EndPoint)sender;

        while (true)
        {
            int recv = socket.ReceiveFrom(data, ref remote);
            Debug.Log($"Message received from {remote}: {Encoding.ASCII.GetString(data, 0, recv)}");

            // Respond to the client
            Thread sendThread = new Thread(() => Send(remote));
            sendThread.IsBackground = true;
            sendThread.Start();
        }
    }

    void Send(EndPoint remote)
    {
        string message = "Ping";
        byte[] data = Encoding.ASCII.GetBytes(message);
        socket.SendTo(data, remote);
        Debug.Log($"Sent 'Ping' to {remote}");
    }
}

