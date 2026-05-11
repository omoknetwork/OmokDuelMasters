using UnityEngine;

public class HeadManager : MonoBehaviour
{
    private HeadBingBing[] allHeads;
    private Camera mainCam;
    private GameManager gameManager;

    private bool wasLooking = false; // 방금 전까지 고개를 돌리고 있었는지 추적하기 위함

    void Start()
    {
        mainCam = Camera.main;
        allHeads = FindObjectsByType<HeadBingBing>(FindObjectsSortMode.None);
        gameManager = FindFirstObjectByType<GameManager>(); // 화면에 있는 GameManager를 자동으로 찾습니다.
    }

    void Update()
    {
        // 스페이스바가 아니라, GameManager에서 '강제 정면 응시(발각)' 중일 때만 true가 됩니다.
        bool isLookAt = (gameManager != null && gameManager.isForcedLookingForward);

        // 발각 상태: 매 프레임 카메라를 쳐다보게 합니다.
        if (isLookAt && mainCam != null)
        {
            Vector3 cameraPos = mainCam.transform.position;

            for (int i = 0; i < allHeads.Length; i++)
            {
                if (allHeads[i] != null)
                {
                    allHeads[i].LookAtPositionOnlyY(cameraPos);
                }
            }
            wasLooking = true; // 현재 고개를 돌리는 중임을 기록
        }
        // 발각 상태가 해제된 직후: 고개를 다시 원래대로(0도) 복구합니다.
        else if (wasLooking)
        {
            wasLooking = false;
            
            for (int i = 0; i < allHeads.Length; i++)
            {
                if (allHeads[i] != null)
                {
                    allHeads[i].angleY = 0f; 
                }
            }
        }
    }
}
