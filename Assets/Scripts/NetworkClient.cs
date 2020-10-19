using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;

    // ID Set by the server for this Client
    public string controlledClientID;

    private Dictionary<string, GameObject> playerLookUpTable = new Dictionary<string, GameObject>();

    // Player controlled by THIS Client
    public GameObject controlledPlayer; 
    
    // Prefab for Player Model
    public GameObject playerPrefab;

    // Updated messaged of controlled player on this client
    PlayerUpdateMsg controlledPlayerUpdateMSG = new PlayerUpdateMsg();

    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);
    }
    
    void SendToServer(string message)
    {
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect()
    {
        Debug.Log("We are now connected to the server");
        InvokeRepeating("HeartBeat", 0.1f, 0.008f);
    }

    void OnData(DataStreamReader stream)
    {
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd)
        {
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received Set clients ID");
                SetupClient(hsMsg);
                break;
            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Player update message received!");
                break;
            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                UpdateAllPlayers(suMsg);
                Debug.Log("Server update message received!");
                break;
            case Commands.SPAWNED_PLAYERS:
                SpawnedPlayersList spawnedPlayers = JsonUtility.FromJson<SpawnedPlayersList>(recMsg);
                SpawnPlayers(spawnedPlayers);
                Debug.Log("Spawned all Players from Server");
                break;
            case Commands.NEW_PLAYER:
                NewPlayerMsg newPlayer = JsonUtility.FromJson<NewPlayerMsg>(recMsg);
                SpawnNewPlayer(newPlayer);
                Debug.Log("Spawned new Player from Server");
                break;
            case Commands.DROPPED_PLAYER:
                DroppedPlayersList droppedPlayers = JsonUtility.FromJson<DroppedPlayersList>(recMsg);
                DestroyDroppedPlayers(droppedPlayers);
                Debug.Log("Dropped dead players from the Server");
                break;
            default:
                Debug.Log("Unrecognized message received!");
                break;
        }
    }

    void Disconnect()
    {
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect()
    {
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }   
    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
    }
    /**
        Client Messages Functions to Server
    */

    // Client sends controlled player's current statistics to server
    void HeartBeat()
    {
        controlledPlayerUpdateMSG.player.cubPos = controlledPlayer.transform.position;
        //controlledPlayerUpdateMSG.player.cubeColor = controlledPlayer.GetComponent<Renderer>().material.color;
        SendToServer(JsonUtility.ToJson(controlledPlayerUpdateMSG));
    }
    /**
        Client Response Functions from server messages
    */

    // Response function to setup controlled players ID     
    void SetupClient(HandshakeMsg hsMsg)
    { 
        controlledPlayer = Instantiate(playerPrefab);
        controlledPlayer.GetComponent<PlayerController>().clientControlled = true;
        controlledPlayer.GetComponent<PlayerController>().networkClient = this;
        controlledPlayerUpdateMSG.player.id = hsMsg.player.id;
        controlledClientID = hsMsg.player.id;
    }
    // Updates all players in client instance
    void UpdateAllPlayers(ServerUpdateMsg serverUpdateMsg)
    {
        for (int i = 0; i < serverUpdateMsg.players.Count; i++)
        {
            if(playerLookUpTable.ContainsKey(serverUpdateMsg.players[i].id))
            {
                playerLookUpTable[serverUpdateMsg.players[i].id].transform.position = serverUpdateMsg.players[i].cubPos;
                playerLookUpTable[serverUpdateMsg.players[i].id].GetComponent<Renderer>().material.color = serverUpdateMsg.players[i].cubeColor;
            } 
            else if (controlledPlayerUpdateMSG.player.id == serverUpdateMsg.players[i].id)
            {
                controlledPlayer.gameObject.GetComponent<Renderer>().material.color = serverUpdateMsg.players[i].cubeColor;
                controlledPlayerUpdateMSG.player.cubeColor = serverUpdateMsg.players[i].cubeColor;
            }           
        }
    }
    // Spawns all players from the sever
    void SpawnPlayers(SpawnedPlayersList spawnMsg)
    {
        for (int i = 0; i < spawnMsg.players.Count; i++)
        {
            GameObject player = Instantiate(playerPrefab);
            playerLookUpTable[spawnMsg.players[i].id] = player;
            player.transform.position = spawnMsg.players[i].cubPos;
            player.GetComponent<PlayerController>().clientControlled = false;
            player.GetComponent<PlayerController>().networkClient = this;
        }
    }
    // Spawns new player from the server
    void SpawnNewPlayer(NewPlayerMsg newPlayerMsg)
    {
        GameObject player = Instantiate(playerPrefab);
        playerLookUpTable[newPlayerMsg.player.id] = player;
        player.GetComponent<PlayerController>().clientControlled = false;
        player.GetComponent<PlayerController>().networkClient = this;
    }

    // Destroy Dropped players 
    void DestroyDroppedPlayers(DroppedPlayersList dropList)
    {
        foreach (var playerID in dropList.droppedPlayers)
        {
            if(playerLookUpTable.ContainsKey(playerID))
            {
                Destroy(playerLookUpTable[playerID]);
                playerLookUpTable.Remove(playerID);
            }
        }
    }

}