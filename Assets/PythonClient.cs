using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using PimDeWitte.UnityMainThreadDispatcher;

public class PythonClient : MonoBehaviour
{
    private TcpClient client;
    private NetworkStream stream;
    private byte[] buffer = new byte[1024];

    private Boolean isStarted = false;
    private GameManager gameManager;
    private List<string> aiInstructions = new List<string>();
    private bool hasReceivedAIInstructions = false;

    void Start()
    {
        ConnectToServer("localhost", 12345);
        StartListening();
        StartCoroutine(WaitForGameManager());
    }

    void ConnectToServer(string host, int port)
    {
        try
        {
            client = new TcpClient(host, port);
            stream = client.GetStream();
            Debug.Log("Connected to server");
        }
        catch (Exception e)
        {
            Debug.LogError("Connection error: " + e.Message);
        }
    }

    IEnumerator WaitForGameManager()
    {
        while (gameManager == null)
        {
            gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            yield return null; // Attendre une frame
        }
        Debug.Log("GameManager found");
    }

    public void SendMessageToServer(string message)
    {
        if (client == null || !client.Connected)
        {
            Debug.LogError("Not connected to server");
            return;
        }

        byte[] data = Encoding.UTF8.GetBytes(message);
        stream.Write(data, 0, data.Length);
        stream.Flush();

        // Read response
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        Debug.Log("Received from server: " + response);
    }

    void OnApplicationQuit()
    {
        if (stream != null) stream.Close();
        if (client != null) client.Close();
    }

    public void MovePion(int pionId, int dx, int dy)
    {
        string message = $"MOVE {pionId} {dx} {dy}";
        SendMessageToServer(message);
    }

    public void Build(int bx, int by)
    {
        string message = $"BUILD {bx} {by}";
        SendMessageToServer(message);
    }

    void StartListening()
    {
        if (client == null || !client.Connected)
        {
            Debug.LogError("Not connected to server");
            return;
        }

        stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(OnDataReceived), null);
    }

    void OnDataReceived(IAsyncResult ar)
    {
        try
        {
            int bytesRead = stream.EndRead(ar);

            if (bytesRead > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Debug.Log($"Message received: {message}");

                try
                {
                    // Traitez le message
                    UnityMainThreadDispatcher.Instance().Enqueue(() => ProcessServerMessage(message));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error processing message: {ex.Message}");
                }
                // Continuez à lire
                stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(OnDataReceived), null);
            }
            else
            {
                Debug.LogWarning("No bytes read. Connection might be closed.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in OnDataReceived: {e.Message}");
        }
    }

    void ProcessServerMessage(string message)
    {
        try
        {
            string[] parts = message.Split();
            if (parts.Length == 0)
            {
                Debug.LogWarning("Empty message received.");
                return;
            }

            if (parts[0] == "INIT")
            {
                string playerName1 = parts[1];
                string pawnName11 = parts[2];
                int x11 = int.Parse(parts[3]);
                int z11 = int.Parse(parts[4]);
                string pawnName12 = parts[5];
                int x12 = int.Parse(parts[6]);
                int z12 = int.Parse(parts[7]);
                string playerName2 = parts[8];
                string pawnName21 = parts[9];
                int x21 = int.Parse(parts[10]);
                int z21 = int.Parse(parts[11]);
                string pawnName22 = parts[12];
                int x22 = int.Parse(parts[13]);
                int z22 = int.Parse(parts[14]);


                if (gameManager != null)
                {
                    gameManager.PlacePawnFromServer(playerName1, pawnName11, x11, z11);
                    gameManager.PlacePawnFromServer(playerName1, pawnName12, x12, z12);
                    gameManager.PlacePawnFromServer(playerName2, pawnName21, x21, z21);
                    gameManager.PlacePawnFromServer(playerName2, pawnName22, x22, z22);
                    gameManager.setInitialPlacement(false);
                }
                else
                {
                    Debug.LogError("GameManager not found.");
                }
            }
            else if (parts[0] == "START")
            {
                Debug.Log("Game START received.");
                isStarted = true;
            }
            else if (parts[0] == "END")
            {
                Debug.Log("Game END received.");
            }
            else if (parts[0] == "AI")
            {
                // Traiter les instructions de l'IA
                aiInstructions.Clear();
                aiInstructions.Add(message);
                hasReceivedAIInstructions = true;
            }
            else if (parts[0] == "MOVECOMPLETE")
            {
                gameManager.setMoveCompleted(true);
            }
            else
            {
                Debug.LogWarning($"Unknown message type: {parts[0]}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in ProcessServerMessage: {ex.Message}");
        }
    }

    public bool HasReceivedAIInstructions()
    {
        return hasReceivedAIInstructions;
    }

    public string[] GetAIInstructions()
    {
        hasReceivedAIInstructions = false;
        return aiInstructions.ToArray();
    }

    public Boolean IsStarted()
    {
        return isStarted;
    }
}
