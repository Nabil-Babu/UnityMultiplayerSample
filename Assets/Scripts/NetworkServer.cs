using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using NetworkMessages;
using System;
using System.Text;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections;
    private Dictionary<string, NetworkObjects.NetworkPlayer> clientLookUpTable = new Dictionary<string, NetworkObjects.NetworkPlayer>();

    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);

        InvokeRepeating("SendAllClientStats", 0.1f, 0.166f);
        InvokeRepeating("ChangeColors", 2.0f, 1.0f);
    }

    void SendToClient(string message, NetworkConnection c)
    {
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }
    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void OnConnect(NetworkConnection c)
    {
        Debug.Log("Accepted a connection");
        // Send a handshake message to Set ID
        HandshakeMsg m = new HandshakeMsg();
        m.player.id = c.InternalId.ToString();
        Assert.IsTrue(c.IsCreated); 
        SendToClient(JsonUtility.ToJson(m),c);

        // Send List of Players to new Client
        SpawnedPlayersList playersInServer = new SpawnedPlayersList();
        foreach (var client in clientLookUpTable)
        {
            playersInServer.players.Add(client.Value);
        }
        Assert.IsTrue(c.IsCreated);
        SendToClient(JsonUtility.ToJson(playersInServer),c);

        // Send new Client to All existing Players
        NewPlayerMsg newPlayer = new NewPlayerMsg();
        newPlayer.player.id = c.InternalId.ToString();
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated);
            SendToClient(JsonUtility.ToJson(newPlayer), m_Connections[i]);
        }

        m_Connections.Add(c);
        clientLookUpTable[c.InternalId.ToString()] = new NetworkObjects.NetworkPlayer();
        clientLookUpTable[c.InternalId.ToString()].heartBeat = Time.time;       
    }

    void OnData(DataStreamReader stream, int i)
    {
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd)
        {
            case Commands.HANDSHAKE:
            HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
            Debug.Log("Handshake message received!");
            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            Debug.Log("Player update message received!");
            UpdateClientStats(puMsg);
            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            Debug.Log("Server update message received!");
            break;
            default:
            Debug.Log("SERVER ERROR: Unrecognized message received!");
            break;
        }
    }

    void OnDisconnect(int i)
    {
        Debug.Log("Client disconnected from server");
        m_Connections[i] = default(NetworkConnection);
    }

    void Update ()
    {
        m_Driver.ScheduleUpdate().Complete();

        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {

                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }

        // AcceptNewConnections
        NetworkConnection c = m_Driver.Accept();
        while (c  != default(NetworkConnection))
        {            
            OnConnect(c);

            // Check if there is another new connection
            c = m_Driver.Accept();
        }


        // Read Incoming Messages
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated);
            
            NetworkEvent.Type cmd;
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            while (cmd != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream, i);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
        }

        // Check for dead Clients
        CheckHeartBeats();
    }

    void UpdateClientStats(PlayerUpdateMsg puMsg)
    {
        if (clientLookUpTable.ContainsKey(puMsg.player.id))
        {
            clientLookUpTable[puMsg.player.id].id = puMsg.player.id;
            clientLookUpTable[puMsg.player.id].cubPos = puMsg.player.cubPos;
            clientLookUpTable[puMsg.player.id].cubeColor = puMsg.player.cubeColor;
            clientLookUpTable[puMsg.player.id].heartBeat = Time.time;
        }
    }

    void SendAllClientStats()
    {
        ServerUpdateMsg m = new ServerUpdateMsg();
        foreach (var client in clientLookUpTable)
        {
            m.players.Add(client.Value);
        }
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated); 
            SendToClient(JsonUtility.ToJson(m), m_Connections[i]);
        }
    }

    void CheckHeartBeats()
    {
        List<string> theDead = new List<string>();
        // Check all clients Heartbeat
        foreach (var heart in clientLookUpTable)
        {
            if(Time.time - heart.Value.heartBeat >= 5.0f)
            {
                theDead.Add(heart.Key);
            }
        }
        // Check if dead exists
        if(theDead.Count > 0)
        {
            // Clear Look Up Table of Dead clients
            for (int i = 0; i < theDead.Count; i++)
            {
                clientLookUpTable.Remove(theDead[i]);
            }

            DroppedPlayersList dropList = new DroppedPlayersList();
            dropList.droppedPlayers = theDead;

            // Send drop list to all clients except dropped ones
            for (int i = 0; i < m_Connections.Length; i++)
            {   
                if(!theDead.Contains(m_Connections[i].InternalId.ToString()))
                {
                    Assert.IsTrue(m_Connections.IsCreated);
                    SendToClient(JsonUtility.ToJson(dropList), m_Connections[i]);
                }
            }
        }
    }

    void ChangeColors()
    {
        foreach (var player in clientLookUpTable)
        {
            player.Value.cubeColor = new Color(UnityEngine.Random.Range(0.0f,1.0f), UnityEngine.Random.Range(0.0f,1.0f), UnityEngine.Random.Range(0.0f,1.0f));
        }
    }
}