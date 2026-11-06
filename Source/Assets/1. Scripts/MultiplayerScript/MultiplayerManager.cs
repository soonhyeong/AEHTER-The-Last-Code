using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiplayerManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button startGameButton;

    [SerializeField] private TMP_InputField joinCodeInputField;

    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text statusText;

    private ISession currentSession;
    private bool initialized;

    private async void Start()
    {
        SetButtonsInteractable(false);

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(false);
        }

        await InitializeUnityServicesAsync();
    }

    private async Task InitializeUnityServicesAsync()
    {
        try
        {
            statusText.text = "Initializing Unity Services...";

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            initialized = true;

            statusText.text =
                $"Ready\nPlayer ID: {AuthenticationService.Instance.PlayerId}";

            SetButtonsInteractable(true);
        }
        catch (Exception exception)
        {
            initialized = false;

            statusText.text =
                $"Initialization Failed\n{exception.Message}";

            Debug.LogException(exception);
        }
    }

    public async void CreateRoom()
    {
        if (!initialized)
        {
            statusText.text = "Unity Services are not ready.";
            return;
        }

        try
        {
            SetButtonsInteractable(false);

            statusText.text = "Creating Room...";

            SessionOptions options = new SessionOptions
            {
                // Maximum number of players including Host
                MaxPlayers = 2,

                // Private room (Join Code only)
                IsPrivate = true
            }.WithRelayNetwork();

            currentSession =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            string joinCode = currentSession.Code;

            joinCodeText.text = $"Join Code: {joinCode}";

            statusText.text =
                "Room Created\nShare the Join Code with your friend.";

            createRoomButton.interactable = false;
            joinRoomButton.interactable = false;
            joinCodeInputField.interactable = false;

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(true);
                startGameButton.interactable = true;
            }

            Debug.Log($"Room Created: {currentSession.Id}");
            Debug.Log($"Join Code: {joinCode}");
        }
        catch (Exception exception)
        {
            statusText.text =
                $"Failed to Create Room\n{exception.Message}";

            Debug.LogException(exception);

            SetButtonsInteractable(true);
        }
    }

    public async void JoinRoom()
    {
        if (!initialized)
        {
            statusText.text = "Unity Services are not ready.";
            return;
        }

        string joinCode =
            joinCodeInputField.text.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            statusText.text = "Please enter a Join Code.";
            return;
        }

        try
        {
            SetButtonsInteractable(false);

            statusText.text = "Joining Room...";

            currentSession =
                await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);

            statusText.text =
                "Successfully Joined\nWaiting for the Host to start the game...";

            joinCodeText.text = $"Joined Code: {joinCode}";

            createRoomButton.interactable = false;
            joinRoomButton.interactable = false;
            joinCodeInputField.interactable = false;

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(false);
            }

            Debug.Log($"Joined Room: {currentSession.Id}");
        }
        catch (Exception exception)
        {
            statusText.text =
                $"Failed to Join Room\n{exception.Message}";

            Debug.LogException(exception);

            SetButtonsInteractable(true);
        }
    }

    public void StartGame()
    {
        if (NetworkManager.Singleton == null)
        {
            statusText.text = "Network Manager not found.";
            return;
        }

        if (!NetworkManager.Singleton.IsHost)
        {
            statusText.text = "Only the Host can start the game.";
            return;
        }

        statusText.text = "Loading Game...";

        NetworkManager.Singleton.SceneManager.LoadScene(
            "GameScene",
            LoadSceneMode.Single
        );
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (createRoomButton != null)
        {
            createRoomButton.interactable = interactable;
        }

        if (joinRoomButton != null)
        {
            joinRoomButton.interactable = interactable;
        }

        if (joinCodeInputField != null)
        {
            joinCodeInputField.interactable = interactable;
        }
    }

    private async void OnApplicationQuit()
    {
        await LeaveSessionAsync();
    }

    private async Task LeaveSessionAsync()
    {
        if (currentSession == null)
        {
            return;
        }

        try
        {
            await currentSession.LeaveAsync();
            currentSession = null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to Leave Session: {exception.Message}");
        }
    }
}