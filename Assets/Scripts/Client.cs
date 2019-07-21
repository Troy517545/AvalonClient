using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

using UnityEngine;
using UnityEngine.Networking;

public class Client : MonoBehaviour
{
    public static Client Instance { private set; get; }
	private const int MAT_USER = 8;
	private const int PORT = 26000;
	private const int WEB_PORT = 26001;
    private const int BYTE_SIZE = 1024;

    private const string SERVER_IP = "127.0.0.1";

	private byte reliableChannel;
    private int connectionId;
	private int hostID;
    private byte error;

	private bool isStarted = false;

	#region Monovehaviour
	void Start()
	{
        Instance = this;
		DontDestroyOnLoad(gameObject);
		Init();
	}

    void Update()
    {
        UpdateMessagePump();
    }
    #endregion

    public void Init()
	{
		NetworkTransport.Init();
		ConnectionConfig cc = new ConnectionConfig();
		reliableChannel = cc.AddChannel(QosType.Reliable);
		HostTopology topo = new HostTopology(cc, MAT_USER);
		hostID = NetworkTransport.AddHost(topo, 0);

#if !UNITY_WEBGL || UNITY_EDITOR
        // Standalone client
        connectionId = NetworkTransport.Connect(hostID, SERVER_IP, PORT, 0, out error);
        Debug.Log("Connecting from standalone");
#else
        // Web client
        connectionId = NetworkTransport.Connect(hostID, SERVER_IP, WEB_PORT, 0, out error);
        Debug.Log("Connecting from Web");

#endif

        Debug.Log(string.Format("Attempting to connect on port {0}...", SERVER_IP));

		isStarted = true;
	}

	public void ShutDown()
	{
		isStarted = false;
		NetworkTransport.Shutdown();

	}

    public void UpdateMessagePump()
    {
        if (!isStarted)
        {
            return;
        }

        int recHostId; // From Web or standalone
        int connectionId; // Which user
        int channelId; // Which lane is the message from

        byte[] recBuffer = new byte[BYTE_SIZE];
        int datasize;

        NetworkEventType type = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, BYTE_SIZE, out datasize, out error);
        switch (type)
        {
            case NetworkEventType.Nothing:
                break;

            case NetworkEventType.ConnectEvent:
                Debug.Log("Connected to the server");
                break;

            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnected from the server");
                break;

            case NetworkEventType.BroadcastEvent:
                Debug.Log(string.Format("Unexpected network event type"));
                break;

            case NetworkEventType.DataEvent:
                BinaryFormatter formatter = new BinaryFormatter();
                MemoryStream ms = new MemoryStream(recBuffer);
                NetMsg msg = (NetMsg)formatter.Deserialize(ms);
                OnData(connectionId, channelId, recHostId, msg);
                break;

            default:
                break;
        }
    }

    #region OnData
    private void OnData(int connectionId, int channelId, int recHostId, NetMsg msg)
    {
        switch (msg.OP)
        {
            case NetOP.None:
                Debug.Log("Unexpected NETOP");
                break;

            case NetOP.OnCreateAccount:

                OnCreateAccount((Net_OnCreateAccount)msg);
                break;

            case NetOP.OnLoginRequest:
                OnLoginAccount((Net_OnLoginRequest)msg);
                break;
            default:
                break;
        }
    }

    private void OnCreateAccount(Net_OnCreateAccount oca)
    {
        LobbyScene.Instance.EnableInputs();
        LobbyScene.Instance.ChangeAuthenticationMessage(oca.Information);
    }

    private void OnLoginAccount(Net_OnLoginRequest olr)
    {
        LobbyScene.Instance.ChangeAuthenticationMessage(olr.Information);

        if (olr.Success != 1)
        {
            LobbyScene.Instance.EnableInputs();
        }
        else
        {

        }
    }
    #endregion

    #region send
    public void SendServer(NetMsg msg)
    {
        byte[] buffer = new byte[BYTE_SIZE];

        BinaryFormatter formatter = new BinaryFormatter();
        MemoryStream ms = new MemoryStream(buffer);
        formatter.Serialize(ms, msg);

        NetworkTransport.Send(hostID, connectionId, reliableChannel, buffer, BYTE_SIZE, out error);
    }

    public void SendCreateAccount(string username, string password, string email)
    {
        Net_CreateAccount ca = new Net_CreateAccount();
        ca.Username = username;
        ca.Password = password;
        ca.Email = email;
        SendServer(ca);
    }

    public void SendLoginRequest(string usernameOrEmail, string password)
    {
        Net_LoginRequest lr = new Net_LoginRequest();
        lr.UsernameOrEmail = usernameOrEmail;
        lr.Password = password;
        SendServer(lr);
    }
    #endregion

    
}
