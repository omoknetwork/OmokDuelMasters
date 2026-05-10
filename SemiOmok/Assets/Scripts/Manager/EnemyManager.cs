/**
 * [수정 내역 - 팀 공유용]
 * 1. 멀티플레이 동기화: 마스터 클라이언트만 타이밍(Random.Range)을 결정하고 RPC로 모든 클라이언트에 통보
 * 2. 시퀀스 동기화: LookAtBoard 및 ReturnToOriginal 상태를 RPC로 동기화하여 싱크 어긋남 방지
 * 3. 안정성 강화: PhotonView가 없는 환경에서도 에러 없이 로컬 모드로 작동하도록 예외 처리 추가
 */
using System.Collections;
using Photon.Pun;
using UnityEngine;

public class EnemyManager : MonoBehaviourPun
{
    [Header("게임 매니저 연결")]
    [Tooltip("첫 착수 타이밍을 알기 위해 GameManager를 연결하세요.")]
    public GameManager gameManager;

    [Header("랜덤 대기 시간 설정 (회전 전)")]
    public float minRandomTime = 2f;
    public float maxRandomTime = 5f;

    [Header("회전 유지 시간 설정 (원상복귀 전)")]
    public float returnDelay = 3f;

    [Header("경고 색상 변환 설정")]
    [Tooltip("색상이 변할 선생님의 머티리얼(Material)을 직접 연결하세요.")]
    public Material teacherMaterial;
    public Color normalColor = Color.white;
    public Color dangerColor = Color.red;

    [Header("Rotation Settings")]
    [SerializeField] private Vector3 originalEulerRotation = new Vector3(-90f, 0f, 0f);
    private Quaternion originalRotation;
    private bool hasStartedSequence = false;
    private bool isGimmickActive = false; // [NET] 기믹 진행 중 주기적 동기화 방지용

    // URP 등에서 Base Map 색상에 접근하기 위한 프로퍼티 ID
    private readonly int baseColorId = Shader.PropertyToID("_BaseColor");

    // [OPTIMIZE] 가비지 생성을 줄이기 위해 캐싱된 WaitForSeconds 사용
    private readonly WaitForSeconds waitOneSecond = new(1f);
    private readonly WaitForSeconds waitFiveSeconds = new(5f);

    private void Awake()
    {
        // [FIX] 다른 매니저들이 Start()에서 호출하기 전에 미리 회전값을 계산해둡니다.
        originalRotation = Quaternion.Euler(originalEulerRotation);
    }

    void Start()
    {
        // 초기 Base Map 색상을 하얀색(normalColor)으로 맞춰줍니다.
        if (teacherMaterial != null)
        {
            teacherMaterial.SetColor(baseColorId, normalColor);
        }

        // GameManager를 못 찾았다면 자동으로 찾아봅니다.
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        // 게임 시작 시 동작하지 않고, 돌이 놓일 때 이벤트를 듣도록 대기합니다.
        if (gameManager != null)
        {
            gameManager.OnStonePlaced -= HandleFirstStonePlaced; // 중복 방지
            gameManager.OnStonePlaced += HandleFirstStonePlaced;
        }
        else
        {
            Debug.LogWarning("[EnemyManager] GameManager를 찾을 수 없습니다. 돌이 놓일 때까지 대기합니다.");
            // 0.5초마다 GameManager를 찾아보는 코루틴 시작
            StartCoroutine(WaitAndFindGameManager());
        }
    }

    private IEnumerator WaitAndFindGameManager()
    {
        while (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager != null)
            {
                gameManager.OnStonePlaced -= HandleFirstStonePlaced;
                gameManager.OnStonePlaced += HandleFirstStonePlaced;
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// 누군가가 첫 번째 돌을 두었을 때 호출됩니다.
    /// </summary>
    private void HandleFirstStonePlaced(int x, int y, GameManager.Player player)
    {
        if (!hasStartedSequence)
        {
            hasStartedSequence = true;

            // 첫 돌이 놓이면 비로소 선생님의 감시 루프가 시작됩니다.
            StartCoroutine(EnemySequence());

            // 이후에는 더 이상 이벤트를 들을 필요가 없으므로 구독 해제
            if (gameManager != null)
            {
                gameManager.OnStonePlaced -= HandleFirstStonePlaced;
            }
        }
    }

    private IEnumerator EnemySequence()
    {
        while (true)
        {
            // 0. 게임 상태 체크 및 대기
            if (gameManager != null)
            {
                // 게임이 아직 시작되지 않았다면 시작될 때까지 기다립니다. (종료하지 않음)
                while (!gameManager.isGameStarted && !gameManager.isGameOver)
                {
                    Debug.Log("[EnemyManager] 게임 시작 대기 중...");
                    yield return waitOneSecond;
                }

                // 게임이 완전히 종료되었다면 기믹 중단
                if (gameManager.isGameOver)
                {
                    StopGimmick();
                    yield break;
                }
            }

            // [NET] 마스터 클라이언트만 타이밍을 주도합니다.
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && photonView != null && !PhotonNetwork.IsMasterClient)
            {
                yield return waitOneSecond;
                continue;
            }

            // 1. 랜덤한 시간 동안 평화롭게 대기 (인스펙터의 Min/Max Random Time 사용)
            float idleTime = Random.Range(minRandomTime, maxRandomTime);
            Debug.Log($"[EnemyManager] 다음 감시까지 대기: {idleTime}초");
            yield return new WaitForSeconds(idleTime);

            // 2. 감시 예고 (짧은 경고 시간 - 1.5초)
            float warningDuration = 1.5f;
            double targetLookTime = PhotonNetwork.Time + (double)warningDuration;

            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && photonView != null)
            {
                photonView.RPC("RPC_LookAtBoard", RpcTarget.All, targetLookTime, warningDuration);
            }
            else
            {
                yield return StartCoroutine(LookAtBoardRoutine(targetLookTime, warningDuration));
            }

            // 3. 선생님이 고개를 돌린 상태로 유지되는 시간 (인스펙터의 Return Delay 사용)
            yield return new WaitForSeconds(warningDuration + returnDelay);

            // 4. 원상 복귀 알림
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && photonView != null)
            {
                photonView.RPC("RPC_ReturnToOriginal", RpcTarget.All);
            }
            else
            {
                ReturnToOriginal();
            }

            // 5. 시퀀스 종료 후 짧은 휴식
            yield return waitFiveSeconds;
        }
    }

    /// <summary>
    /// 선생님의 감시 기믹을 즉시 중단하고 초기 상태로 되돌립니다.
    /// [NET][FIX] 상대방이 퇴장하거나 게임이 완전히 종료되었을 때 호출해야 합니다.
    /// </summary>
    public void StopGimmick()
    {
        StopAllCoroutines();
        hasStartedSequence = false;
        ReturnToOriginal();

        // 다시 첫 돌이 놓일 때를 기다리도록 이벤트 재연결
        if (gameManager != null)
        {
            gameManager.OnStonePlaced -= HandleFirstStonePlaced; // 중복 방지
            gameManager.OnStonePlaced += HandleFirstStonePlaced;
        }

        Debug.Log("[EnemyManager] Gimmick Stopped and Reset.");
    }

    private void SetTeacherColor(Color color)
    {
        if (teacherMaterial == null) return;

        // URP 표준(_BaseColor) 또는 레거시/표준(_Color) 프로퍼티 중 존재하는 곳에 적용합니다.
        if (teacherMaterial.HasProperty("_BaseColor"))
        {
            teacherMaterial.SetColor("_BaseColor", color);
        }
        else if (teacherMaterial.HasProperty("_Color"))
        {
            teacherMaterial.SetColor("_Color", color);
        }
        else
        {
            // 둘 다 없다면 유니티 기본 color 속성 변경을 시도합니다.
            teacherMaterial.color = color;
        }
    }

    private IEnumerator LookAtBoardRoutine(double targetTime, float totalDuration)
    {
        isGimmickActive = true;
        
        // [NET][FIX] 오프라인(싱글) 모드와 온라인 모드 판정
        bool isOffline = !PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InRoom;
        
        Debug.Log($"[EnemyManager] LookAtBoardRoutine 시작 - 모드: {(isOffline ? "싱글" : "멀티")}, 시간: {totalDuration}초");

        if (isOffline)
        {
            // --- 1. 싱글 플레이 (오프라인) ---
            float elapsed = 0f;
            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / totalDuration);
                SetTeacherColor(Color.Lerp(normalColor, dangerColor, t));
                yield return null;
            }
        }
        else
        {
            // --- 2. 멀티 플레이 (온라인) ---
            while (PhotonNetwork.Time < targetTime)
            {
                double remainingTime = targetTime - PhotonNetwork.Time;
                float t = 1.0f - (float)(remainingTime / (double)totalDuration);
                SetTeacherColor(Color.Lerp(normalColor, dangerColor, Mathf.Clamp01(t)));
                yield return null;
            }
        }

        // 정확한 목표 시점에 고개 회전 및 색상 고정
        Vector3 targetRot = new(originalEulerRotation.x, originalEulerRotation.y, 180f);
        transform.rotation = Quaternion.Euler(targetRot);
        SetTeacherColor(dangerColor);


        Debug.Log($"[EnemyManager] 고개 돌림 완료 (Mode: {(PhotonNetwork.InRoom ? "Online" : "Offline")})");
    }

    [PunRPC]
    private void RPC_LookAtBoard(double targetTime, float totalDuration)
    {
        Debug.Log($"[EnemyManager] RPC_LookAtBoard 수신 - Target: {targetTime}, Current: {PhotonNetwork.Time}");
        // 마스터는 이미 EnemySequence에서 대기 중이므로 루틴만 실행
        StartCoroutine(LookAtBoardRoutine(targetTime, totalDuration));
    }

    [PunRPC]
    private void RPC_ReturnToOriginal()
    {
        ReturnToOriginal();
    }

    /// <summary>
    /// [NET][FIX] 새로운 플레이어가 들어왔을 때 현재 선생님의 상태(회전, 색상)를 동기화합니다.
    /// </summary>
    [PunRPC]
    private void RPC_SyncEnemyState(Quaternion currentRotation, Color currentColor)
    {
        if (PhotonNetwork.IsMasterClient) return; // 마스터는 동기화 주체이므로 무시

        transform.rotation = currentRotation;
        if (teacherMaterial != null)
        {
            teacherMaterial.SetColor(baseColorId, currentColor);
        }
    }

    private void ReturnToOriginal()
    {
        // [NET][FIX] 회전 루틴이 진행 중이라면 중단하여 회전값이 꼬이지 않게 합니다.
        StopAllCoroutines();
        isGimmickActive = false; // 동기화 보호 해제

        // 원래 방향으로 원상 복귀 및 색상을 다시 원래대로 즉시 초기화
        transform.rotation = Quaternion.Euler(originalEulerRotation);
        SetTeacherColor(normalColor);


        Debug.Log($"[EnemyManager] 원래대로 복구 완료");
    }

    // ★ 중요: 유니티 에디터 환경에서 플레이를 껐을 때 머티리얼 원본 파일이 빨간색으로 고정되는 것을 막아줍니다.
    private void OnDestroy()
    {
        // 스크립트가 파괴될 때 이벤트 구독 해제 및 색상 원상복구
        if (gameManager != null)
        {
            gameManager.OnStonePlaced -= HandleFirstStonePlaced;
        }

        if (teacherMaterial != null)
        {
            teacherMaterial.SetColor(baseColorId, normalColor);
        }
    }

    private void Update()
    {
        // 마스터 클라이언트인 경우 주기적으로 선생님의 상태를 동기화 (기믹 작동 중에는 제외)
        if (!isGimmickActive && PhotonNetwork.IsConnected && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && photonView != null)
        {
            if (Time.frameCount % 60 == 0) // 약 1초마다 동기화
            {
                Color currentColor = normalColor;
                if (teacherMaterial != null && teacherMaterial.HasProperty(baseColorId))
                {
                    currentColor = teacherMaterial.GetColor(baseColorId);
                }
                photonView.RPC("RPC_SyncEnemyState", RpcTarget.Others, transform.rotation, currentColor);
            }
        }
    }
}
