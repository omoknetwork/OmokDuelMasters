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

    private Quaternion originalRotation;
    private bool hasStartedSequence = false;

    // URP 등에서 Base Map 색상에 접근하기 위한 프로퍼티 ID
    private readonly int baseColorId = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        // 시작할 때의 초기 회전값을 저장해 둡니다.
        originalRotation = transform.rotation;

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
            gameManager.OnStonePlaced += HandleFirstStonePlaced;
        }
        else
        {
            // 매니저가 없다면 그냥 바로 시작
            StartCoroutine(EnemySequence());
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
        // 무한루프를 돌며 시퀀스를 반복합니다.
        while (true)
        {
            // 1. 마스터 클라이언트만 타이밍(Random.Range)을 결정합니다.
            // (PhotonView가 있고 네트워크에 연결된 경우에만 동기화 로직 작동)
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && photonView != null && !PhotonNetwork.IsMasterClient)
            {
                // 마스터가 아닐 경우 루프를 돌지 않고 대기 (RPC를 통해 동작을 수신함)
                yield return new WaitForSeconds(1f);
                continue;
            }

            float randomWaitTime = Random.Range(minRandomTime, maxRandomTime);

            // 2. 모든 클라이언트에 시퀀스 시작(경고 및 돌아보기) 알림
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && photonView != null)
            {
                photonView.RPC("RPC_LookAtBoard", RpcTarget.All, randomWaitTime);
            }
            else
            {
                // 로컬 모드 혹은 PhotonView가 없는 경우 각자 실행 (이전 코드 방식)
                yield return StartCoroutine(LookAtBoardRoutine(randomWaitTime));
            }

            // 3. 시퀀스 유지 시간 대기 (랜덤 시간 + 회전 유지 시간)
            yield return new WaitForSeconds(randomWaitTime + returnDelay);

            // 4. 원상 복귀 알림
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && photonView != null)
            {
                photonView.RPC("RPC_ReturnToOriginal", RpcTarget.All);
            }
            else
            {
                ReturnToOriginal();
            }

            // 5. 5초 대기(휴식) 후 시퀀스 재시작
            yield return new WaitForSeconds(5f);
        }
    }

    [PunRPC]
    private void RPC_LookAtBoard(float randomWaitTime)
    {
        // 마스터는 이미 EnemySequence에서 대기 중이므로 루틴만 실행 (All로 보냈으므로 자신도 실행)
        StartCoroutine(LookAtBoardRoutine(randomWaitTime));
    }

    [PunRPC]
    private void RPC_ReturnToOriginal()
    {
        ReturnToOriginal();
    }

    private IEnumerator LookAtBoardRoutine(float randomWaitTime)
    {
        // 지정된 시간 동안 기다리면서 매 프레임 머티리얼 색상을 붉게 물들입니다.
        float elapsedTime = 0f;
        while (elapsedTime < randomWaitTime)
        {
            elapsedTime += Time.deltaTime;
            float lerpFactor = elapsedTime / randomWaitTime;

            if (teacherMaterial != null)
            {
                Color lerpedColor = Color.Lerp(normalColor, dangerColor, lerpFactor);
                teacherMaterial.SetColor(baseColorId, lerpedColor);
            }

            yield return null;
        }

        // 돌아보기 (이전 코드와 동일하게 Z축 180도로 복구)
        transform.rotation = originalRotation * Quaternion.Euler(0, 0, 180f);
    }

    private void ReturnToOriginal()
    {
        // 원래 방향으로 원상 복귀 및 색상을 다시 원래대로 즉시 초기화
        transform.rotation = originalRotation;
        if (teacherMaterial != null)
        {
            teacherMaterial.SetColor(baseColorId, normalColor);
        }
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
}
