﻿using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibData;

// SendTo();
class Program
{
    static void Main(string[] args)
    {
        ClientUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{

    //TODO: [Deserialize Setting.json]
    private static int messageIdCounter = 1;
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    //DNS records
    public static (string Name, string Type)[] DomainNames = new (string, string)[]
    {
    ("www.outlook.com", "A"),
    ("www.test.com", "A"),
    ("www.sample.com", "A"),
    ("www.mywebsite.com", "A"),
    ("www.customdomain.com", "A"),
    ("example", "A"), // Faulty domain name
    ("Error", "A"), // Faulty domain name
    ("example.com", "MX"),
    ("mail.example.com", "A"),
    ("www.example.com", "A") // Faulty domain type
    };

    // Load DNS records

    public static void start()
    {
        IPAddress serverIP = IPAddress.Parse(setting.ServerIPAddress);
        IPEndPoint serverEndPoint = new IPEndPoint(serverIP, setting.ServerPortNumber);

        IPAddress clientIP = IPAddress.Parse(setting.ClientIPAddress);
        IPEndPoint clientEndPoint = new IPEndPoint(clientIP, setting.ClientPortNumber);

        using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(clientEndPoint);


        try
        {
            Message helloMsg = new Message
            {
                MsgId = messageIdCounter++,
                MsgType = MessageType.Hello,
                Content = "Hello from client"
            };
            SendMessage(socket, serverEndPoint, helloMsg);
            Console.WriteLine($"{helloMsg.MsgId} SEND: TYPE: {helloMsg.MsgType} CONTENT: {helloMsg.Content}");

            Message reply = ReceiveMessage(socket, ref serverEndPoint);
            Console.WriteLine($"{reply.MsgId} RECEIVED: TYPE: {reply.MsgType} CONTENT: {reply.Content}");

            foreach (var (name, type) in DomainNames)
            {
                var lookupContent = new Dictionary<string, string>
                {
                    { "DomainName", name },
                    { "Type", type }
                };

                Message lookupMsg = new Message
                {
                    MsgId = messageIdCounter++,
                    MsgType = MessageType.DNSLookup,
                    Content = lookupContent
                };

                SendMessage(socket, serverEndPoint, lookupMsg);
                Console.WriteLine($"{lookupMsg.MsgId} SEND: {lookupMsg.MsgType} DOMAIN: {lookupContent["DomainName"]} TYPE: {lookupContent["Type"]}");

                Message response = ReceiveMessage(socket, ref serverEndPoint);
                Console.WriteLine($"{response.MsgId} RECEIVED: TYPE: {response.MsgType} CONTENT: {response.Content}");

                if (response.MsgType == MessageType.DNSLookupReply)
                {
                    Message Ack = new Message
                    {
                        MsgId = response.MsgId,
                        MsgType = MessageType.Ack,
                        Content = response.MsgId
                    };
                    SendMessage(socket, serverEndPoint, Ack);
                    Console.WriteLine($"{response.MsgId} SEND: TYPE: {Ack.MsgType} CONTENT: {response.Content}");
                }
                if (response.MsgType == MessageType.Error)
                {
                    Message Ack = new Message
                    {
                        MsgId = response.MsgId,
                        MsgType = MessageType.Ack,
                        Content = "Recieved records unsuccessfully"
                    };
                    SendMessage(socket, serverEndPoint, Ack);
                    Console.WriteLine($"{response.MsgId} SEND: TYPE: {Ack.MsgType} CONTENT: {response.Content}");
                }


            }

            Message ClientendAck = new Message
            {
                MsgId = messageIdCounter++,
                MsgType = MessageType.End,
                Content = "End of DNSLookup"
            };

            SendMessage(socket, serverEndPoint, ClientendAck);
            Console.WriteLine($"{ClientendAck.MsgId} SEND: TYPE: {ClientendAck.MsgType} CONTENT: {ClientendAck.Content}");

            // Wait for server to signal end of communication
            Message endMsg = ReceiveMessage(socket, ref serverEndPoint);
            if (endMsg.MsgType == MessageType.End)
            {
                Console.WriteLine($"{endMsg.MsgId} RECEIVED: TYPE: {endMsg.MsgType} CONTENT: {endMsg.Content}");
                Console.WriteLine("Server has ended communication. Closing client.");
            }
        }
        finally
        {
            socket.Close();
            Console.WriteLine("Client shut down.");
        }
    }

    private static void SendMessage(Socket socket, IPEndPoint endPoint, Message message)
    {
        string json = JsonSerializer.Serialize(message);
        byte[] buffer = Encoding.ASCII.GetBytes(json);
        socket.SendTo(buffer, endPoint);
    }

    private static Message ReceiveMessage(Socket socket, ref IPEndPoint endPoint)
    {
        byte[] buffer = new byte[1024];
        EndPoint tempEndPoint = new IPEndPoint(IPAddress.Any, 0);
        int bytesRead = socket.ReceiveFrom(buffer, ref tempEndPoint);
        endPoint = (IPEndPoint)tempEndPoint;

        string receivedJson = Encoding.ASCII.GetString(buffer, 0, bytesRead);
        return JsonSerializer.Deserialize<Message>(receivedJson)!;
    }
}







//TODO: [Send Acknowledgment to Server]

//TODO: [Receive and print End from server]