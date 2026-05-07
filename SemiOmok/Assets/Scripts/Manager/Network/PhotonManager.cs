using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Photon 네트워크 테스트용 매니저
/// </summary>
public class PhotonManager : MonoBehaviourPunCallbacks
{
    [Header("Photon Settings")]
    [SerializeField] private string gameVersion = "0.1";
    [SerializeField] private bool connectOnStart = true;
    [SerializeField] private bool autoJoinLobbyOnConnected = true;

    public static PhotonManager Instance { get; private set; }

    private bool isConnecting = false;
    private bool isJoiningLobby = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (connectOnStart)
        {
            ConnectToPhoton();
        }
    }

    /// <summary>
    /// SCRUM-26: Photon 연결 (ConnectToPhoton)
    /// </summary>
    public void ConnectToPhoton()
    {
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("[SCRUM-26] 이미 포톤 서버에 연결되어 있습니다.");
            return;
        }

        if (isConnecting)
        {
            Debug.Log("[SCRUM-26] 현재 포톤 연결이 진행 중입니다...");
            return;
        }

        isConnecting = true;

        PhotonNetwork.GameVersion = gameVersion;

        // [FIX] PlayerPrefs를 사용하여 닉네임을 영구 저장하고 재접속 시에도 동일하게 유지
        string savedNickName = PlayerPrefs.GetString("UserNickName", "");
        if (string.IsNullOrEmpty(savedNickName))
        {
            savedNickName = $"Player_{Random.Range(1000, 9999)}";
            PlayerPrefs.SetString("UserNickName", savedNickName);
            PlayerPrefs.Save();
        }

        PhotonNetwork.NickName = savedNickName;

        PhotonNetwork.AutomaticallySyncScene = true;

        Debug.Log($"[SCRUM-26] Photon 연결 시작 | NickName: {PhotonNetwork.NickName} | Version: {gameVersion}");

        if (!PhotonNetwork.ConnectUsingSettings())
        {
            Debug.LogError("[SCRUM-26] Photon 연결 시도 실패: ConnectUsingSettings가 false를 반환했습니다.");
            isConnecting = false;
        }
    }

    /// <summary>
    /// SCRUM-27: 로비 입장
    /// </summary>
    public void JoinLobby()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("[SCRUM-27] 아직 Master Server 연결 준비가 되지 않았습니다. (State: " + PhotonNetwork.NetworkClientState + ")");
            return;
        }

        if (PhotonNetwork.InLobby || isJoiningLobby)
        {
            Debug.Log("[SCRUM-27] 이미 로비에 있거나 로비 입장 진행 중입니다. (InLobby: " + PhotonNetwork.InLobby + ", isJoiningLobby: " + isJoiningLobby + ")");
            return;
        }

        isJoiningLobby = true;

        Debug.Log("[SCRUM-27] 로비 입장 시도...");
        PhotonNetwork.JoinLobby();
    }

    /// <summary>
    /// Photon 마스터 서버 연결 성공
    /// </summary>
    public override void OnConnectedToMaster()
    {
        isConnecting = false;
        Debug.Log("[SCRUM-26] Photon 마스터 서버 연결 성공");

        if (autoJoinLobbyOnConnected)
        {
            JoinLobby();
        }
    }

    /// <summary>
    /// SCRUM-27: 로비 입장 성공
    /// </summary>
    public override void OnJoinedLobby()
    {
        isJoiningLobby = false;
        Debug.Log("[SCRUM-27] 로비 입장 성공");
    }

    /// <summary>
    /// 로비 이탈
    /// </summary>
    public override void OnLeftLobby()
    {
        isJoiningLobby = false;
        Debug.Log("[SCRUM-27] 로비에서 나갔습니다.");
    }

    /// <summary>
    /// Photon 연결 해제
    /// </summary>
    public override void OnDisconnected(DisconnectCause cause)
    {
        isConnecting = false;
        isJoiningLobby = false;
        Debug.LogWarning($"[SCRUM-26] Photon 연결 해제 | Cause: {cause}");
    }

    /// <summary>
    /// 연결 상태 확인용
    /// </summary>
    public bool IsConnected()
    {
        return PhotonNetwork.IsConnectedAndReady;
    }

    /// <summary>
    /// 로비 입장 상태 확인용
    /// </summary>
    public bool IsInLobby()
    {
        return PhotonNetwork.InLobby;
    }

    /// <summary>
    /// 현재 닉네임 확인용
    /// </summary>
    public string GetNickName()
    {
        return PhotonNetwork.NickName;
    }
}