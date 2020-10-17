using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkMessages
{
    public enum Commands
    {
        PLAYER_UPDATE,
        SERVER_UPDATE,
        HANDSHAKE,
        PLAYER_INPUT,
        SPAWNED_PLAYERS,
        NEW_PLAYER,
        DROPPED_PLAYER
    }

    [System.Serializable]
    public class NetworkHeader
    {
        public Commands cmd;
    }

    [System.Serializable]
    public class HandshakeMsg : NetworkHeader
    {
        public NetworkObjects.NetworkPlayer player;
        public HandshakeMsg()  // Constructor
        {     
            cmd = Commands.HANDSHAKE;
            player = new NetworkObjects.NetworkPlayer();
        }
    }
    
    [System.Serializable]
    public class PlayerUpdateMsg : NetworkHeader 
    { 
        public NetworkObjects.NetworkPlayer player;
        public PlayerUpdateMsg() // Constructor
        {      
            cmd = Commands.PLAYER_UPDATE;
            player = new NetworkObjects.NetworkPlayer();
        }
    };

    public class PlayerInputMsg : NetworkHeader 
    {
        public Input myInput;
        public PlayerInputMsg()
        {
            cmd = Commands.PLAYER_INPUT;
            myInput = new Input();
        }
    }
    [System.Serializable]
    public class  ServerUpdateMsg : NetworkHeader
    {
        public List<NetworkObjects.NetworkPlayer> players;
        public ServerUpdateMsg()  // Constructor
        {     
            cmd = Commands.SERVER_UPDATE;
            players = new List<NetworkObjects.NetworkPlayer>();
        }
    }

    [System.Serializable]
    public class SpawnedPlayersList : NetworkHeader
    {
        public List<NetworkObjects.NetworkPlayer> players;
        public SpawnedPlayersList()
        {
            cmd = Commands.SPAWNED_PLAYERS;
            players = new List<NetworkObjects.NetworkPlayer>();
        }
    }

    [System.Serializable]
    public class NewPlayerMsg : NetworkHeader 
    { 
        public NetworkObjects.NetworkPlayer player;
        public NewPlayerMsg() // Constructor
        {      
            cmd = Commands.NEW_PLAYER;
            player = new NetworkObjects.NetworkPlayer();
        }
    };

    [System.Serializable]
    public class DroppedPlayersList : NetworkHeader
    {
        public List<string> droppedPlayers;
        public DroppedPlayersList()
        {
            cmd = Commands.DROPPED_PLAYER;
            droppedPlayers = new List<string>();
        }
    }
} 

namespace NetworkObjects
{
    [System.Serializable]
    public class NetworkObject
    {
        public string id;
    }
    [System.Serializable]
    public class NetworkPlayer : NetworkObject
    {
        public Color cubeColor;
        public Vector3 cubPos;
        public float heartBeat;
        public NetworkPlayer()
        {
            cubeColor = new Color();
            cubPos = new Vector3(0,0,0);
        }
    }
}
