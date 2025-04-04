using System;
using System.Data;
using System.Data.SqlTypes;
using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LibData;

// ReceiveFrom();
class Program
{
    static void Main(string[] args)
    {
        ServerUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}


class ServerUDP
{
    private static int messageIdCounter = 1;
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    // TODO: [Read the JSON file and return the list of DNSRecords]

    //DNS records
    public static DNSRecord[]? dNSRecords;

    // Load DNS records
    private static void LoadDNSRecords()
    {
        string dnsDataFile = @"DNSRecords.json";
        string dnsDataContent = File.ReadAllText(dnsDataFile);
        dNSRecords = JsonSerializer.Deserialize<DNSRecord[]>(dnsDataContent);
        Console.WriteLine($"Loaded {dNSRecords?.Length} DNS records");
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


    public static void start()
    {


        // TODO: [Create a socket and endpoints and bind it to the server IP address and port number]

        // ip and endpoint setup
        IPAddress serverIP = IPAddress.Parse(setting.ServerIPAddress);
        IPEndPoint serverEndPoint = new IPEndPoint(serverIP, setting.ServerPortNumber);

        // socket
        using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(serverEndPoint);

        Console.WriteLine($"Server started on {serverEndPoint}");

        // get DNS records
        LoadDNSRecords();



        // TODO:[Receive and print a received Message from the client]
        try
        {
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            bool CommunicationsActive = true;
            while (CommunicationsActive)
            {
                Message recieved = ReceiveMessage(socket, ref clientEndPoint);
                Console.WriteLine($"Recieved {recieved.MsgType} from {clientEndPoint}");

                // switch to give a diffrent response for each message type
                switch (recieved.MsgType)
                {
                    // Welcome msg
                    case MessageType.Hello:
                        Message Welcome = new Message
                        {
                            MsgId = messageIdCounter++,
                            MsgType = MessageType.Welcome,
                            Content = $"Welcome from server"
                        };
                        SendMessage(socket, clientEndPoint, Welcome);
                        break;

                    case MessageType.DNSLookup:
                        {
                            // Isolate the domain name from the message content
                            string domainName = recieved.Content?.ToString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(domainName))
                            {
                                // Find matching DNS records
                                var matchedRecords = dNSRecords?.Where(r => r.Name.Equals(domainName, StringComparison.OrdinalIgnoreCase)).ToArray();

                                if (matchedRecords != null && matchedRecords.Length > 0)
                                {
                                    // Create DNS lookup reply message
                                    Message dnsReply = new Message
                                    {
                                        MsgId = recieved.MsgId,
                                        MsgType = MessageType.DNSLookupReply,
                                        Content = matchedRecords
                                    };
                                    SendMessage(socket, clientEndPoint, dnsReply);
                                    Console.WriteLine($"{recieved.MsgId}: Sent DNSLookupReply to {clientEndPoint} for {domainName}");
                                }
                                else
                                {
                                    // Send an error message if no record found
                                    Message errorMsg = new Message
                                    {
                                        MsgId = recieved.MsgId,
                                        MsgType = MessageType.Error,
                                        Content = "Domain not found"
                                    };
                                    SendMessage(socket, clientEndPoint, errorMsg);
                                    Console.WriteLine($"{recieved.MsgId}: No record found for {domainName}");
                                }

                                Message ack = ReceiveMessage(socket, ref clientEndPoint); // Receive Ack from client
                                Console.WriteLine($"{recieved.MsgId}: Response: {ack.Content} from {clientEndPoint}");
                            }
                            else
                            {
                                // Send an error message for invalid request format
                                Message errorMsg = new Message
                                {
                                    MsgId = recieved.MsgId,
                                    MsgType = MessageType.Error,
                                    Content = "Domain not found"
                                };
                                SendMessage(socket, clientEndPoint, errorMsg);
                                Console.WriteLine($"{recieved.MsgId}: Invalid DNS lookup request format");

                            }
                            break;
                        }
                    case MessageType.End:
                        // ending communications
                        Message End = new Message
                        {
                            MsgId = messageIdCounter++,
                            MsgType = MessageType.End,
                            Content = "End of DNSLooku"
                        };
                        SendMessage(socket, clientEndPoint, End);
                        Console.WriteLine($"{messageIdCounter}:Ending communication with {clientEndPoint}");
                        break;

                    // Unknown msg
                    default:
                        Console.WriteLine($"Unknown message: {recieved.MsgType}");
                        break;
                }

            }




        }

        finally
        {
            socket.Close();
        }




        // TODO:[Receive and print Hello]



        // TODO:[Send Welcome to the client]


        // TODO:[Receive and print DNSLookup]


        // TODO:[Query the DNSRecord in Json file]

        // TODO:[If found Send DNSLookupReply containing the DNSRecord]



        // TODO:[If not found Send Error]


        // TODO:[Receive Ack about correct DNSLookupReply from the client]


        // TODO:[If no further requests receieved send End to the client]

    }
}