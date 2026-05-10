/**
 * [수정 내역 - 팀 공유용]
 * 1. RoomManager 연동: 타이틀 버튼 클릭 시 RoomManager의 싱글/멀티 시작 함수 호출
 * 2. 동적 버튼 설정: Inspector에서 수동 연결 없이도 버튼 컴포넌트 자동 추가 및 클릭/호버 이벤트 바인딩
 * 3. 클릭 영역 확보: 투명 이미지가 없는 오브젝트도 클릭 가능하도록 자동 보정 기능 추가
 * 4. 룰 패널(Rule Panel) 연동: 3번째 버튼 클릭 시 룰 패널 활성화, 뒤로가기 버튼용 함수 추가
 * 5. 게임 종료 함수 추가: QuitGame() 
 * 6. 커서 전용 독립 Overlay 캔버스 자동 생성 및 Raycast 차단 문제 해결
 * 7. 타임라인(PlayableDirector) 2개(정방향/역방향용) 분리 적용 
 */
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

public class TitleUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("설명서를 보여줄 룰 패널을 연결하세요. (기본: 비활성화)")]
    public GameObject rulePanel;

    [Header("Timeline Control")]
    [Tooltip("패널을 열 때 재생할 타임라인(PlayableDirector)을 연결하세요.")]
    public PlayableDirector forwardTimeline;
    [Tooltip("패널을 닫을 때 재생할 두 번째 타임라인(PlayableDirector)을 연결하세요.")]
    public PlayableDirector backwardTimeline;

    [Header("UI Buttons")]
    [Tooltip("마우스를 올렸을 때 커질 버튼들을 연결하세요.")]
    public GameObject[] titleButtons;

    [Header("Hover Settings")]
    public float hoverScaleMultiplier = 1.2f;
    private Vector3 originalScale = Vector3.one;

    [Header("Audio Settings")]
    [Tooltip("사운드를 재생할 오디오 소스(Audio Source)를 연결하세요.")]
    public AudioSource audioSource;
    [Tooltip("버튼에 마우스를 올렸을 때 한 번(OneShot) 재생할 클립을 연결하세요.")]
    public AudioClip hoverSoundClip;

    [Header("Custom Cursor Settings")]
    [Tooltip("마우스 커서를 대신할 프리팹(Prefab)을 넣으세요. (UI Image 권장)")]
    public GameObject customCursorPrefab;

    public bool hideDefaultCursor = true;

    [Tooltip("커서 위치 미세 조정용")]
    public Vector3 cursorOffset = Vector3.zero;
    public Vector3 cursorScale = Vector3.one;
    public Vector3 cursorRotation = Vector3.zero;

    private RectTransform actualCursor;
    private RectTransform cursorCanvasRect;

    private void Start()
    {
        if (forwardTimeline != null)
        {
            forwardTimeline.playOnAwake = false;
        }
        if (backwardTimeline != null)
        {
            backwardTimeline.playOnAwake = false;
        }

        if (PhotonManager.Instance != null)
        {
            if (Photon.Pun.PhotonNetwork.IsConnected == false)
            {
                Debug.Log("[TitleUI] Photon is not connected. Attempting to connect...");
                PhotonManager.Instance.ConnectToPhoton();
            }
            else if (Photon.Pun.PhotonNetwork.InLobby == false && Photon.Pun.PhotonNetwork.InRoom == false)
            {
                Debug.Log("[TitleUI] Connected to Master but not in Lobby. Joining Lobby...");
                PhotonManager.Instance.JoinLobby();
            }
        }

        if (rulePanel != null)
            rulePanel.SetActive(false);

        if (hideDefaultCursor)
        {
            Cursor.visible = false;
        }

        if (customCursorPrefab != null)
        {
            GameObject cursorVirtualCanvasObj = new GameObject("Global_CursorCanvas");
            Canvas cCanvas = cursorVirtualCanvasObj.AddComponent<Canvas>();
            cCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cCanvas.sortingOrder = 32767;

            cursorVirtualCanvasObj.AddComponent<CanvasScaler>();
            GraphicRaycaster gr = cursorVirtualCanvasObj.AddComponent<GraphicRaycaster>();
            gr.enabled = false;

            cursorCanvasRect = cursorVirtualCanvasObj.GetComponent<RectTransform>();
            GameObject spawnedCursor = Instantiate(customCursorPrefab, cursorCanvasRect);
            actualCursor = spawnedCursor.GetComponent<RectTransform>();

            actualCursor.anchorMin = new Vector2(0.5f, 0.5f);
            actualCursor.anchorMax = new Vector2(0.5f, 0.5f);
            actualCursor.pivot = new Vector2(0.5f, 0.5f);

            actualCursor.localScale = cursorScale;
            actualCursor.localRotation = Quaternion.Euler(cursorRotation);

            Graphic[] cursorGraphics = actualCursor.GetComponentsInChildren<Graphic>();
            foreach (Graphic g in cursorGraphics)
            {
                g.raycastTarget = false;
            }
        }

        foreach (GameObject btn in titleButtons)
        {
            if (btn != null)
            {
                SetupButtonEvents(btn);
            }
        }
    }

    private void Update()
    {
        if (actualCursor != null && cursorCanvasRect != null)
        {
            Vector2 mousePos = Input.mousePosition;
            Vector2 localPoint;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(cursorCanvasRect, mousePos, null, out localPoint))
            {
                actualCursor.localPosition = new Vector3(localPoint.x, localPoint.y, 0f) + cursorOffset;
            }
        }
    }

    private void SetupButtonEvents(GameObject btnObj)
    {
        Button btn = btnObj.GetComponent<Button>();
        if (btn == null)
        {
            btn = btnObj.AddComponent<Button>();
            if (btnObj.GetComponent<UnityEngine.UI.Image>() == null)
            {
                var img = btnObj.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0, 0, 0, 0);
            }
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnButtonClick(btnObj));

        EventTrigger trigger = btnObj.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = btnObj.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { OnHoverEnter(btnObj.transform); });
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { OnHoverExit(btnObj.transform); });
        trigger.triggers.Add(exitEntry);
    }

    private void OnButtonClick(GameObject btnObj)
    {
        Debug.Log($"[TitleUI] Button Clicked: {btnObj.name}");

        if (titleButtons.Length > 0 && btnObj == titleButtons[0])
        {
            if (Assets.Scripts.Manager.Network.RoomManager.Instance != null)
            {
                Debug.Log("[TitleUI] AI Start");
                Assets.Scripts.Manager.Network.RoomManager.Instance.StartSinglePlayer();
            }
        }
        else if (titleButtons.Length > 1 && btnObj == titleButtons[1])
        {
            if (Assets.Scripts.Manager.Network.RoomManager.Instance != null)
            {
                Debug.Log("[TitleUI] Match Start");
                Assets.Scripts.Manager.Network.RoomManager.Instance.StartMatch();
            }
        }
        else if (titleButtons.Length > 2 && btnObj == titleButtons[2])
        {
            Debug.Log("[TitleUI] Open Rules Panel");
            if (rulePanel != null)
                rulePanel.SetActive(true);
        }
        else if (titleButtons.Length > 3 && btnObj == titleButtons[3])
        {
            Debug.Log("[TitleUI] 4th Button Clicked (Inspector Link Only)");
        }
    }

    public void CloseRulePanel()
    {
        if (rulePanel != null)
        {
            rulePanel.SetActive(false);
        }
    }

    public void QuitGame()
    {
        Debug.Log("[TitleUI] 게임을 종료합니다.");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnHoverEnter(Transform btnTransform)
    {
        btnTransform.localScale = originalScale * hoverScaleMultiplier;

        if (audioSource != null && hoverSoundClip != null)
        {
            audioSource.PlayOneShot(hoverSoundClip);
        }
    }

    private void OnHoverExit(Transform btnTransform)
    {
        btnTransform.localScale = originalScale;
    }

    // ===============================================
    // ★ 2개의 타임라인 제어
    // ===============================================

    /// <summary>
    /// 첫 번째 타임라인을 강제로 처음부터 재생합니다. (주로 열 때 사용)
    /// </summary>
    public void PlayTimelineForward()
    {
        if (forwardTimeline != null)
        {
            forwardTimeline.Stop();
            forwardTimeline.time = 0;
            forwardTimeline.Evaluate();
            forwardTimeline.Play();
        }
    }

    /// <summary>
    /// 두 번째 타임라인을 강제로 처음부터 재생합니다. (주로 닫을 때 사용)
    /// </summary>
    public void PlayTimelineBackward()
    {
        if (backwardTimeline != null)
        {
            backwardTimeline.Stop();
            backwardTimeline.time = 0;
            backwardTimeline.Evaluate();
            backwardTimeline.Play();
        }
    }
}
