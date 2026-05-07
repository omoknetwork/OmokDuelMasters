using UnityEngine;

public class HeadBingBing : MonoBehaviour
{
    [Header("머리 오브젝트 (Head)")]
    [Tooltip("고개를 돌릴 3D 머리 모델을 연결하세요.")]
    public Transform targetHead;

    [Header("목 위치 (가상 축 조절)")]
    [Tooltip("머리의 현재 원점(귀 근처)에서 실제 목 부분까지의 거리를 맞춥니다.")]
    public Vector3 neckOffset = new Vector3(0f, -0.2f, 0f);

    [Header("블렌더 시선 보정 조절기 (중요)")]
    [Tooltip("카메라를 쳐다보게 했는데 옆통수나 뒤통수를 보인다면, 이 값을 90, -90, 180 등으로 조절해보세요.")]
    public float facingOffset = 0f; // ★ 블렌더 축 꼬임 해결용 보정값

    [Header("고개 돌리기 각도 (테스트)")]
    [Range(-90f, 90f)] public float angleX = 0f; // 끄덕끄덕
    [Range(-180f, 180f)] public float angleY = 0f; // 도리도리
    [Range(-90f, 90f)] public float angleZ = 0f; // 갸우뚱

    // 초기 상태 저장
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool isInitialized = false;

    void Start()
    {
        if (targetHead != null)
        {
            initialPosition = targetHead.position;
            initialRotation = targetHead.rotation;
            isInitialized = true;
        }
    }

    void Update()
    {
        if (!isInitialized || targetHead == null) return;

        // 1. 목(가상 피봇)의 월드 좌표 계산
        Vector3 neckPivot = initialPosition + (initialRotation * neckOffset);

        // 2. 인스펙터에서 설정한(또는 외부에서 조작한) 각도로 회전 생성
        Quaternion headRotation = Quaternion.Euler(angleX, angleY, angleZ);

        // 3. 목을 중심으로 머리의 새 위치와 회전값 덮어씌우기
        targetHead.position = neckPivot + (headRotation * (initialPosition - neckPivot));
        targetHead.rotation = headRotation * initialRotation;
    }

    /// <summary>
    /// 외부 스크립트에서 이 함수를 호출하면, 타겟 위치(카메라)를 향해 Y축으로만 고개를 완벽히 돌립니다.
    /// </summary>
    public void LookAtPositionOnlyY(Vector3 targetPosition)
    {
        if (!isInitialized || targetHead == null) return;

        // 1. 현재 가상 목의 좌표 계산
        Vector3 neckPivot = initialPosition + (initialRotation * neckOffset);

        // 2. 목에서 목표 지점(카메라)을 향하는 방향 계산 (위아래 높낮이는 무시)
        Vector3 directionToTarget = targetPosition - neckPivot;
        directionToTarget.y = 0f; 

        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            // 3. 블렌더 모델 특성상 축이 꼬이므로, 모델 자체가 아닌 몸통(스크립트 붙은 오브젝트)의 정면을 가져옴
            Vector3 baseForward = transform.forward;
            baseForward.y = 0f;

            // 4. 정면을 기준으로 카메라 방향까지의 각도를 -180도 ~ 180도 사이로 계산
            float targetAngleY = Vector3.SignedAngle(baseForward, directionToTarget, Vector3.up);

            // 5. 사용자가 인스펙터에서 지정한 보정값(facingOffset)을 더해서 최종 각도 적용!
            angleY = targetAngleY + facingOffset;
        }
    }

    private void OnDrawGizmos()
    {
        if (targetHead == null) return;

        Vector3 basePos = Application.isPlaying ? initialPosition : targetHead.position;
        Quaternion baseRot = Application.isPlaying ? initialRotation : targetHead.rotation;

        Vector3 neckPivot = basePos + (baseRot * neckOffset);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(neckPivot, 0.03f); 
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(basePos, neckPivot);
    }
}
