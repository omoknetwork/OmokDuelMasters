using UnityEngine;

public class HeadManager : MonoBehaviour
{
    private bool isLookAt = false;
    
    private HeadBingBing[] allHeads;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        
        // ★ 최신 유니티 권장 방식: FindObjectsByType 사용 (성능 낭비를 막기 위해 정렬 안 함으로 설정)
        allHeads = FindObjectsByType<HeadBingBing>(FindObjectsSortMode.None);
    }

    void Update()
    {
        // 스페이스바를 누르는 순간: 고개 돌리기 켜기
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isLookAt = true;
        }
        // 스페이스바에서 손을 떼는 순간: 고개 돌리기 끄고 원래대로 복구
        else if (Input.GetKeyUp(KeyCode.Space))
        {
            isLookAt = false;
            
            // 모든 머리의 Y축 각도를 0으로 초기화
            for (int i = 0; i < allHeads.Length; i++)
            {
                if (allHeads[i] != null)
                {
                    allHeads[i].angleY = 0f; 
                }
            }
        }

        // isLookAt이 true일 때(누르고 있는 동안)만 매 프레임 카메라를 쳐다보게 합니다.
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
        }
    }
}
