using UnityEngine;

public class CameraCollisionDetector : MonoBehaviour
{
    [Tooltip("게임의 전체 상태를 관리하는 GameManager를 연결하세요.")]
    public GameManager gameManager;

    private void Start()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 태그가 'Sam'인 오브젝트와 닿았을 때
        if (other.CompareTag("Sam"))
        {
            if (gameManager != null)
            {
                // 스페이스바를 떼서 앞을 바라보고 있는 상태일 때 체력 감소
                if (!gameManager.isSpaceHeld)
                {
                    Debug.Log("[CameraCollisionDetector] 선생님에게 발각됨! 데미지 요청.");
                    gameManager.TakeDamage(1);
                }
            }
            else
            {
                Debug.LogWarning("[CameraCollisionDetector] GameManager를 찾을 수 없어 데미지를 처리할 수 없습니다.");
            }
        }
    }
}