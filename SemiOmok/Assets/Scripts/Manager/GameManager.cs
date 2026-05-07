/**
 * [수정 내역 - 팀 공유용]
 * 1. isGameStarted 플래그 도입: 멀티플레이 시 모든 준비(매칭+코인토스)가 끝날 때까지 착수 금지 로직 추가
 * 2. PlaceStone 예외 처리: 게임 시작 전이나 패널이 켜져 있을 때 클릭 방지 강화
 * 3. 모드 자동 감지: Photon 룸 참여 여부에 따라 싱글/멀티 모드 및 로컬 플레이어 색상 초기화 자동화
 * 4. 게임 종료 딜레이: 승리/패배 조건 달성 시 즉시 결과창을 띄우지 않고 2초 대기 후 엔딩 출력
 * 5. 오목 달성 시 빨간 줄 표시 기능 추가: CheckWin 함수를 수정하여 달성된 돌들의 좌표 구하기
 * 6. 빨간 줄 버그 픽스: BoardManager의 그리드 정보를 활용하여 완벽한 위치 계산 및 선긋기
 * 7. 체력 감소 관련 연출 추가: 분필 사운드, 피격 연출(비네팅, 강제 카메라 응시) 인스펙터 변수화
 */
using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PhotonView))]
public class GameManager : MonoBehaviour
{
    public enum Player { None, Black, White }
    public enum GameMode { Local, MultiPlay }

    [Header("Board Settings")]
    public int boardSize = 15;


    public static GameManager Instance { get; private set; }

    private Player[,] board;

    [Header("Game State")]
    public GameMode currentMode = GameMode.Local;
    public Player currentPlayer = Player.Black;
    public bool isGameOver = false;
    public bool isGameStarted = false;

    public Player localPlayer = Player.None;

    [Header("Health Settings (Chalks)")]
    [Tooltip("최대 체력 (기본 5)")]
    public int maxHealth = 5;
    private int currentHealth;

    [Tooltip("분필들이 생성될 Scroll View 안의 Content 오브젝트를 연결하세요.")]
    public Transform healthContentParent;
    [Tooltip("멀쩡한 분필 프리팹")]
    public GameObject normalChalkPrefab;
    [Tooltip("부러진 분필 프리팹")]
    public GameObject brokenChalkPrefab;

    [Header("Health Audio & Visual Settings")]
    [Tooltip("체력 소모 사운드를 재생할 오디오 소스를 연결하세요.")]
    public AudioSource audioSource;
    [Tooltip("체력이 깎일 때 재생할 분필 부러지는 소리 클립을 연결하세요.")]
    public AudioClip damageSoundClip;


    [Tooltip("데미지를 입을 때 켜질 비네팅(또는 피격) 패널을 연결하세요.")]
    public GameObject damageVignettePanel;

    [Tooltip("피격 시 비네팅 효과가 지속되는 시간(초)")]
    public float vignetteDuration = 0.5f;
    [Tooltip("피격 이후 카메라가 강제 정면 응시를 시작하기까지의 대기 시간(초)")]
    public float forcedLookDelay = 1.0f;
    [Tooltip("카메라가 강제로 정면을 응시하는 시간(초)")]
    public float forcedLookDuration = 1.0f;

    private readonly List<GameObject> chalkInstances = new();

    [Header("Win Line Settings")]
    [Tooltip("오목 달성 시 빨간 줄을 그을지 여부")]
    public bool drawWinLine = true;
    [Tooltip("선이 보드판에 묻히지 않도록 돌 위치 기준 위로 띄워주는 높이 값")]
    public float lineYOffset = 0.05f;
    [Tooltip("빨간 줄 선의 두께")]
    public float lineWidth = 0.2f;

    [Header("Turn Timer Settings")]
    [Tooltip("턴당 제한 시간 (초)")]
    public float turnTimeLimit = 20f;
    private float remainingTurnTime;
    [Tooltip("시간을 표시할 UI Text (TMPro)")]
    public TMPro.TextMeshProUGUI timerText;
    [Tooltip("타이머의 배경 오브젝트 (내 차례일 때만 활성화)")]
    public GameObject timerBackground;

    [Header("Optional Managers")]
    public AIManager aiManager;
    [Tooltip("보드의 위치를 정확히 가져오기 위해 BoardManager를 연결하세요.")]
    public BoardManager boardManager;

    [Header("UI References")]
    public GameObject coinTossPanel;
    public GameObject matchingPanel;
    public VideoPanelPlayer videoPanelPlayer;
    public UIManager uiManager;

    [Header("Camera Rotation Settings")]
    public Transform targetCamera;
    public float pressedXAngle = 0f;
    public float releasedXAngle = 90f;
    public float pressedYAngle = 0f;
    public float releasedYAngle = 0f;
    public float rotationSpeed = 5f;

    public bool isSpaceHeld = false;
    public bool isForcedLookingForward = false;

    private Coroutine forcedLookCoroutine;
    private Coroutine vignetteCoroutine;

    public event Action<int, int, Player> OnStonePlaced;
    public event Action<Player> OnTurnChanged;
    public event Action<Player> OnGameOver;

    private readonly int[][] directions = new int[][]
    {
        new int[] { 1, 0 },
        new int[] { 0, 1 },
        new int[] { 1, 1 },
        new int[] { 1, -1 }
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;

        string sceneName = SceneManager.GetActiveScene().name;
        bool isMultiplayerScene = sceneName.Contains("Multi", StringComparison.OrdinalIgnoreCase);

        if (PhotonNetwork.InRoom && isMultiplayerScene)
        {
            currentMode = GameMode.MultiPlay;
        }
        else
        {
            currentMode = GameMode.Local;
            isGameStarted = false;

            localPlayer = Player.None;
        }
    }

    private void Start()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        bool isMultiplayerScene = sceneName.Contains("Multi", StringComparison.OrdinalIgnoreCase);

        if (PhotonNetwork.InRoom && isMultiplayerScene)
        {
            currentMode = GameMode.MultiPlay;
        }
        else
        {
            currentMode = GameMode.Local;
        }

        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }

        if (damageVignettePanel != null)
        {
            damageVignettePanel.SetActive(false);

        }

        InitializeGame();

        if (currentMode == GameMode.Local && !isMultiplayerScene)
        {
            if (coinTossPanel != null) coinTossPanel.SetActive(true);

            CoinToss coinToss = FindFirstObjectByType<CoinToss>();
            if (coinToss != null)
            {
                coinToss.StartToss();
            }
            else
            {
                isGameStarted = true;
                localPlayer = Player.Black;
                remainingTurnTime = turnTimeLimit; // 타이머 초기화 추가
                OnTurnChanged?.Invoke(currentPlayer);
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            TakeDamage(1);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartScene();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            isSpaceHeld = true;
            if (uiManager != null) uiManager.SetCursorVisible(false);
            if (boardManager != null) boardManager.UpdateMarkerVisibility(false);
        }
        else if (Input.GetKeyUp(KeyCode.Space))
        {
            isSpaceHeld = false;
            if (uiManager != null) uiManager.SetCursorVisible(true);
            if (boardManager != null) boardManager.UpdateMarkerVisibility(true);
        }

        if (targetCamera != null)
        {
            // 스페이스바가 눌려있거나, 강제로 앞을 봐야할 때 정면 응시 모드로 회전
            bool lookForward = isSpaceHeld || isForcedLookingForward;
            float targetAngleX = lookForward ? pressedXAngle : releasedXAngle;
            float targetAngleY = lookForward ? pressedYAngle : releasedYAngle;

            Vector3 currentEuler = targetCamera.rotation.eulerAngles;

            float newX = Mathf.LerpAngle(currentEuler.x, targetAngleX, Time.deltaTime * rotationSpeed);
            float newY = Mathf.LerpAngle(currentEuler.y, targetAngleY, Time.deltaTime * rotationSpeed);

            targetCamera.rotation = Quaternion.Euler(newX, newY, currentEuler.z);
        }

        // --- 턴 타이머 로직 추가 ---
        if (isGameStarted && !isGameOver)
        {
            UpdateTurnTimer();
        }
    }

    private void UpdateTurnTimer()
    {
        remainingTurnTime -= Time.deltaTime;

        // 내 차례인지 확인
        bool isMyTurn = (currentPlayer == localPlayer);

        if (timerText != null)
        {
            // [NET/FIX] 내 차례일 때만 텍스트를 보여주고 업데이트함
            timerText.gameObject.SetActive(isMyTurn);


            if (isMyTurn)
            {
                timerText.text = $"{Mathf.CeilToInt(Mathf.Max(0, remainingTurnTime))}";
                // 3초 이하일 때 빨간색으로 표시
                timerText.color = (remainingTurnTime <= 3f) ? Color.red : Color.black;
            }
        }

        if (timerBackground != null)
        {
            // [NET/FIX] 내 차례일 때만 배경을 보여줌
            timerBackground.SetActive(isMyTurn);
        }

        if (remainingTurnTime <= 0)
        {
            // [NET] 멀티플레이 시 마스터 클라이언트만 타임아웃 권한을 가짐
            if (currentMode == GameMode.MultiPlay)
            {
                if (Photon.Pun.PhotonNetwork.IsMasterClient)
                {
                    remainingTurnTime = turnTimeLimit; // 중복 호출 방지
                    HandleTimeout();
                }
            }
            else
            {
                remainingTurnTime = turnTimeLimit;
                HandleTimeout();
            }
        }
    }

    private void HandleTimeout()
    {
        Debug.Log($"[GameManager] 시간 초과! {currentPlayer}의 랜덤 착수를 진행합니다.");
        PlaceStoneRandomly();
    }

    private void PlaceStoneRandomly()
    {
        List<Vector2Int> emptyCells = new();
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                if (board[x, y] == Player.None && !IsForbidden(x, y, currentPlayer))
                {
                    emptyCells.Add(new Vector2Int(x, y));
                }
            }
        }

        if (emptyCells.Count > 0)
        {
            Vector2Int randomCell = emptyCells[UnityEngine.Random.Range(0, emptyCells.Count)];


            if (currentMode == GameMode.MultiPlay)
            {
                // [NET] 멀티플레이 타임아웃: 마스터 클라이언트가 직접 확인 이벤트를 브로드캐스트
                if (Photon.Pun.PhotonNetwork.IsMasterClient)
                {
                    if (Assets.Scripts.Manager.Network.NetworkTurnManager.Instance != null)
                    {
                        Assets.Scripts.Manager.Network.NetworkTurnManager.Instance.BroadcastConfirmStoneFromMaster(randomCell.x, randomCell.y, (int)currentPlayer);
                    }
                }
            }
            else
            {
                PlaceStone(randomCell.x, randomCell.y, currentPlayer);
            }
        }
    }

    private void RestartScene()
    {
        if (currentMode == GameMode.MultiPlay)
        {
            Debug.LogWarning("[GameManager] 멀티플레이 모드에서는 R키로 씬을 재시작할 수 없습니다.");
            return;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void InitializeGame()
    {
        board = new Player[boardSize, boardSize];
        currentPlayer = Player.Black;
        isGameOver = false;

        isGameStarted = false;

        for (int i = 0; i < boardSize; i++)
            for (int j = 0; j < boardSize; j++)
                board[i, j] = Player.None;

        currentHealth = maxHealth;
        InitializeHealthUI();


        remainingTurnTime = turnTimeLimit; // 초기화 시 타이머 설정
    }

    public void StartGameAfterCoinToss(Player assignedLocalPlayer)
    {
        isGameStarted = true;
        localPlayer = assignedLocalPlayer;

        if (aiManager != null)
        {
            aiManager.aiPlayerColor = (localPlayer == Player.Black) ? Player.White : Player.Black;
        }

        if (coinTossPanel != null)
        {
            coinTossPanel.SetActive(false);
        }

        remainingTurnTime = turnTimeLimit;
        OnTurnChanged?.Invoke(currentPlayer);
    }

    private void InitializeHealthUI()
    {
        if (healthContentParent == null || normalChalkPrefab == null) return;

        foreach (Transform child in healthContentParent)
        {
            Destroy(child.gameObject);
        }
        chalkInstances.Clear();

        for (int i = 0; i < maxHealth; i++)
        {
            GameObject chalk = Instantiate(normalChalkPrefab, healthContentParent);
            chalkInstances.Add(chalk);
        }
    }

    // ==========================================
    // ★ 피격 연출 코루틴 (인스펙터 변수 연동)
    // ==========================================
    private void TriggerDamageEffects()
    {
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        if (forcedLookCoroutine != null) StopCoroutine(forcedLookCoroutine);

        vignetteCoroutine = StartCoroutine(DamageVignetteRoutine());
        forcedLookCoroutine = StartCoroutine(ForcedCameraLookRoutine());
    }

    private IEnumerator DamageVignetteRoutine()
    {
        if (damageVignettePanel != null)
        {
            damageVignettePanel.SetActive(true);
            yield return new WaitForSeconds(vignetteDuration);
            damageVignettePanel.SetActive(false);
        }
    }

    private IEnumerator ForcedCameraLookRoutine()
    {
        yield return new WaitForSeconds(forcedLookDelay);


        isForcedLookingForward = true;


        float elapsed = 0f;
        while (elapsed < forcedLookDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }


        isForcedLookingForward = false;
        forcedLookCoroutine = null;
    }

    public void TakeDamage(int damageAmount)
    {
        if (isGameOver) return;

        for (int d = 0; d < damageAmount; d++)
        {
            if (currentHealth <= 0) break;
            currentHealth--;

            int targetIndex = maxHealth - currentHealth - 1;

            if (audioSource != null && damageSoundClip != null)
            {
                audioSource.PlayOneShot(damageSoundClip);
            }

            // [NET] 상대방에게 내가 피격당했음을 알림
            if (currentMode == GameMode.MultiPlay)
            {
                PhotonView pv = GetComponent<PhotonView>();
                if (pv != null)
                {
                    pv.RPC("RPC_OnPlayerHit", RpcTarget.Others, localPlayer);
                }
            }

            // 피격 효과 애니메이션 실행
            TriggerDamageEffects();

            if (healthContentParent != null && brokenChalkPrefab != null)
            {
                GameObject oldChalk = chalkInstances[targetIndex];
                int siblingIndex = oldChalk.transform.GetSiblingIndex();
                Destroy(oldChalk);

                GameObject newBrokenChalk = Instantiate(brokenChalkPrefab, healthContentParent);
                newBrokenChalk.transform.SetSiblingIndex(siblingIndex);
                chalkInstances[targetIndex] = newBrokenChalk;
            }

            Debug.Log($"체력 감소! 현재 체력: {currentHealth}");

            if (currentHealth <= 0)
            {
                if (currentMode == GameMode.MultiPlay)
                {
                    PhotonView pv = GetComponent<PhotonView>();
                    if (pv != null)
                    {
                        pv.RPC("RPC_GameOverByHealth", RpcTarget.All, localPlayer);
                    }
                }
                else
                {
                    isGameOver = true;
                    string reason = "선생님에게 발각되어 체력이 바닥났습니다...";
                    Player winner = (localPlayer == Player.Black) ? Player.White : Player.Black;

                    StartCoroutine(DelayGameOverRoutine(winner, false, reason));
                }
                break;
            }
        }
    }

    public bool PlaceStone(int x, int y, Player requestPlayer)
    {
        if (currentMode == GameMode.MultiPlay && !isGameStarted) return false;
        if (isGameOver) return false;
        if (currentPlayer != requestPlayer) return false;

        bool isLocalPlayer = (requestPlayer == localPlayer);
        bool isAiPlayer = (aiManager != null && aiManager.enabled && aiManager.aiPlayerColor == requestPlayer);
        bool isRemotePlayer = (currentMode == GameMode.MultiPlay && requestPlayer != localPlayer);

        if (!isLocalPlayer && !isAiPlayer && !isRemotePlayer) return false;
        if (x < 0 || x >= boardSize || y < 0 || y >= boardSize) return false;
        if (board[x, y] != Player.None) return false;

        if (currentPlayer == Player.Black && IsForbidden(x, y, currentPlayer))
        {
            Debug.LogWarning("렌주룰: 흑은 쌍삼 또는 쌍사에 돌을 둘 수 없습니다!");
            return false;
        }

        board[x, y] = currentPlayer;

        OnStonePlaced?.Invoke(x, y, currentPlayer);

        if (CheckWin(x, y, currentPlayer, out int sx, out int sy, out int ex, out int ey))
        {
            isGameOver = true;

            DrawWinningLine(sx, sy, ex, ey);

            bool isWin = (currentPlayer == localPlayer);
            string reason = isWin ? "오목 완성! 당신의 승리입니다!" : "상대방의 오목 완성! 당신의 패배입니다...";

            StartCoroutine(DelayGameOverRoutine(currentPlayer, isWin, reason));

            return true;
        }

        currentPlayer = (currentPlayer == Player.Black) ? Player.White : Player.Black;
        remainingTurnTime = turnTimeLimit; // 턴 전환 시 타이머 리셋
        OnTurnChanged?.Invoke(currentPlayer);

        return true;
    }

    private IEnumerator DelayGameOverRoutine(Player winner, bool isWin, string reason)
    {
        // [NET][FIX] 게임 종료 연출 시작 시 메뉴창이 열려있다면 닫습니다.
        if (uiManager != null) uiManager.ResumeGame();

        // [NET][FIX] 게임이 종료되었으므로 AI 연산을 중단하여 영상 재생 리소스를 확보합니다.
        if (aiManager != null) aiManager.enabled = false;

        Debug.Log($"[VideoTrace] {Time.time:F2}s : DelayGameOverRoutine 시작 (Mode: {currentMode}, Win: {isWin})");

        if (videoPanelPlayer != null)
        {
            // [NET][FIX] 결과 연출 대기 시간 동안 백그라운드에서 비디오 로딩을 미리 시작합니다.
            videoPanelPlayer.PrepareResultVideo(isWin, reason);
        }

        // 선생님에게 발각되었을 때 카메라가 강제 정면 응시 애니메이션 중일 수 있으므로
        // 결과 패널이 뜨기 전 대기시간을 피격 애니메이션을 다 볼 수 있게 넉넉히 가져갑니다. (기본 1초 -> 0.5초로 단축)
        float delay = reason.Contains("발각") ? (forcedLookDelay + forcedLookDuration + 0.5f) : 0.5f;


        Debug.Log($"[VideoTrace] {Time.time:F2}s : 연출 대기 시작 (대기시간: {delay}s)");
        yield return new WaitForSeconds(delay);

        OnGameOver?.Invoke(winner);

        if (videoPanelPlayer != null)
        {
            videoPanelPlayer.PlayResultVideo(isWin, reason);
        }
    }

    private void DrawWinningLine(int startX, int startY, int endX, int endY)
    {
        if (!drawWinLine || boardManager == null)
        {
            if (boardManager == null) Debug.LogWarning("[GameManager] 보드 매니저가 연결되지 않아 승리 선의 위치를 계산할 수 없습니다.");
            return;
        }

        GameObject lineObj = new("WinningRedLine");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();

        lr.material = new Material(Shader.Find("Sprites/Default"));

        lr.startColor = Color.red;
        lr.endColor = Color.red;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.positionCount = 2;

        lr.numCapVertices = 5;
        lr.numCornerVertices = 5;

        System.Reflection.MethodInfo getWorldPosMethod = boardManager.GetType().GetMethod("GetWorldPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (getWorldPosMethod != null)
        {
            Vector3 startWorldPos = (Vector3)getWorldPosMethod.Invoke(boardManager, new object[] { startX, startY, lineYOffset });
            Vector3 endWorldPos = (Vector3)getWorldPosMethod.Invoke(boardManager, new object[] { endX, endY, lineYOffset });

            lr.SetPosition(0, startWorldPos);
            lr.SetPosition(1, endWorldPos);

            Debug.Log($"[GameManager] 빨간 줄 생성 완료! ({startX}, {startY}) => ({endX}, {endY})");
        }
        else
        {
            Debug.LogWarning("[GameManager] BoardManager의 GetWorldPosition 함수를 찾을 수 없습니다.");
        }
    }

    public bool IsForbidden(int x, int y, Player player)
    {
        if (player == Player.White) return false;
        if (x < 0 || x >= boardSize || y < 0 || y >= boardSize) return true;
        if (board[x, y] != Player.None) return false;

        board[x, y] = player;
        bool forbidden = false;

        if (CheckWin(x, y, player, out _, out _, out _, out _))
        {
            board[x, y] = Player.None;
            return false;
        }

        if (GetFourCount(x, y, player) >= 2) forbidden = true;
        else if (GetOpenThreeCount(x, y, player) >= 2) forbidden = true;

        board[x, y] = Player.None;
        return forbidden;
    }

    private bool CheckWin(int x, int y, Player player, out int winStartX, out int winStartY, out int winEndX, out int winEndY)
    {
        winStartX = x; winStartY = y; winEndX = x; winEndY = y;

        foreach (var dir in directions)
        {
            int countFwd = CountStones(x, y, dir[0], dir[1], player);
            int countBwd = CountStones(x, y, -dir[0], -dir[1], player);
            int count = 1 + countFwd + countBwd;

            if ((player == Player.Black && count == 5) || (player == Player.White && count >= 5))
            {
                winStartX = x - (dir[0] * countBwd);
                winStartY = y - (dir[1] * countBwd);
                winEndX = x + (dir[0] * countFwd);
                winEndY = y + (dir[1] * countFwd);
                return true;
            }
        }
        return false;
    }

    private int CountStones(int startX, int startY, int dirX, int dirY, Player player)
    {
        int count = 0;
        int cx = startX + dirX;
        int cy = startY + dirY;

        while (cx >= 0 && cx < boardSize && cy >= 0 && cy < boardSize && board[cx, cy] == player)
        {
            count++;
            cx += dirX;
            cy += dirY;
        }
        return count;
    }

    private int GetFourCount(int x, int y, Player player)
    {
        int fourCount = 0;
        foreach (var dir in directions)
        {
            string line = GetLinePattern(x, y, dir[0], dir[1], player);
            if (line.Contains("11110") || line.Contains("01111") ||
                line.Contains("11101") || line.Contains("11011") || line.Contains("10111"))
            {
                fourCount++;
            }
        }
        return fourCount;
    }

    private int GetOpenThreeCount(int x, int y, Player player)
    {
        int openThreeCount = 0;
        foreach (var dir in directions)
        {
            string line = GetLinePattern(x, y, dir[0], dir[1], player);
            if (line.Contains("011100") || line.Contains("001110") ||
                line.Contains("010110") || line.Contains("011010"))
            {
                openThreeCount++;
            }
        }
        return openThreeCount;
    }

    private string GetLinePattern(int x, int y, int dx, int dy, Player player)
    {
        string pattern = "";
        for (int i = -4; i <= 4; i++)
        {
            int cx = x + (i * dx);
            int cy = y + (i * dy);

            if (cx < 0 || cx >= boardSize || cy < 0 || cy >= boardSize)
                pattern += "2";
            else
            {
                if (board[cx, cy] == player) pattern += "1";
                else if (board[cx, cy] == Player.None) pattern += "0";
                else pattern += "2";
            }
        }
        return pattern;
    }

    public Player GetCellState(int x, int y)
    {
        if (x < 0 || x >= boardSize || y < 0 || y >= boardSize) return Player.None;
        return board[x, y];
    }

    [PunRPC]
    private void RPC_OnPlayerHit(Player hitPlayer)
    {
        Debug.Log($"[GameManager] {hitPlayer}가 선생님에게 들켰습니다!");
    }

    [PunRPC]
    private void RPC_GameOverByHealth(Player loser)
    {
        if (isGameOver) return;
        isGameOver = true;

        bool isWin = (loser != localPlayer);
        Player winner = (loser == Player.Black) ? Player.White : Player.Black;
        string reason = isWin ? "상대방이 발각되어 당신이 승리했습니다!" : "선생님에게 발각되어 체력이 바닥났습니다...";

        Debug.Log($"[GameManager] 체력 고갈 종료 RPC 수신 - Winner: {winner}, IsWin: {isWin}");
        StartCoroutine(DelayGameOverRoutine(winner, isWin, reason));
    }
}
