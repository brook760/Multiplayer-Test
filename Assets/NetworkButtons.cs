using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkButtons : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button exitButton;

    [Header("UI Panels")]
    [SerializeField] private GameObject MenuPanel;
    [SerializeField] private TMP_Text statusText;

    private void Start()
    {
        if(hostButton != null) hostButton.onClick.AddListener(StartHostGame);
        if(clientButton != null) clientButton.onClick.AddListener(StartClientGame);
        if (exitButton != null) exitButton.onClick.AddListener(HandleExitOrDisconnect);
    }

    private void HandleExitOrDisconnect()
    {
        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
        {
            UpdateStatus("Shutting down connection...");
            NetworkManager.Singleton.Shutdown();

            // Explicitly force the menu to show back up after shutting down the network
            ShowMenu();
            clientButton.interactable = true;
            UpdateExitButtonText("Exit Game");
        }
        // Case 2: If we are just sitting on the main menu, close the actual application
        else
        {
            Debug.Log("Exiting Application...");
            Application.Quit();
        }
    }

    private void OnEnable()
    {
        if(NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }
    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }
    private void StartHostGame()
    {
        UpdateStatus("[Multiplayer] Starting as HOST...");
        NetworkManager.Singleton.StartHost();
        HideMenu();
    }
    private void StartClientGame()
    {
       UpdateStatus("[Multiplayer] Connecting as CLIENT...");
        clientButton.interactable = false; // Prevent multiple clicks while connecting
        NetworkManager.Singleton.StartClient();
    }
    private void HandleClientConnected(ulong clientId)
    {
        if(NetworkManager.Singleton.LocalClientId == clientId)
        {
            UpdateStatus("[Multiplayer] Successfully connected as CLIENT!");
            HideMenu(); // Hide menu after successful connection
        }
        else
        {
            Debug.Log($"[Multiplayer] Client {clientId} connected.");
        }
    }
    private void HandleClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            UpdateStatus("[Multiplayer] Disconnected from server.");
            ShowMenu(); ; // Show menu again if we were the client
            clientButton.interactable = true; // Re-enable the client button
        }
        else
        {
            Debug.Log($"[Multiplayer] Client {clientId} disconnected.");
        }
    }
    private void HideMenu() { if (MenuPanel != null) MenuPanel.SetActive(false); }
    private void ShowMenu() { if (MenuPanel != null) MenuPanel.SetActive(true); }
    private void UpdateStatus(string message)
    {
        Debug.Log($"[Multiplayer Status] {message}");
        if (statusText != null) statusText.text = message;
    }
    private void UpdateExitButtonText(string v)
    {
        if (exitButton != null)
        {
            Text btnText = exitButton.GetComponentInChildren<Text>();
            if (btnText != null) btnText.text = v;
        }
    }
    private void OnDestroy()
    {
        // Good practice: clean up listeners when the object is destroyed to prevent memory leaks
        if (hostButton != null) hostButton.onClick.RemoveListener(StartHostGame);
        if (clientButton != null) clientButton.onClick.RemoveListener(StartClientGame);
    }
}
