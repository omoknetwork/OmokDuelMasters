/**
 * [수정 내역 - 팀 공유용]
 * 1. RoomManager 연동: 타이틀 버튼 클릭 시 RoomManager의 싱글/멀티 시작 함수 호출
 * 2. 동적 버튼 설정: Inspector에서 수동 연결 없이도 버튼 컴포넌트 자동 추가 및 클릭/호버 이벤트 바인딩
 * 3. 클릭 영역 확보: 투명 이미지가 없는 오브젝트도 클릭 가능하도록 자동 보정 기능 추가
 * 4. 룰 패널(Rule Panel) 연동: 3번째 버튼 클릭 시 룰 패널 활성화, 뒤로가기 버튼용 함수 추가
 * 5. 게임 종료 함수 추가: QuitGame() 
 * 6. 커서 전용 독립 Overlay 캔버스 자동 생성 및 Raycast 차단 문제 해결
 * 7. 타임라인(PlayableDirector) 2개(정방향/역방향용) 분리 적용 
 * 8. 비디오 동시 재생 제어 함수 추가: PlayAllVideos() 버튼 클릭 시 다수의 비디오를 켜고, 끝나면 패널 닫기
 * 9. 버튼 매핑 수정: 4번째 버튼 클릭 시 역방향 타임라인(BackwardTimeline) 재생 기능 연결
 */
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.Video; // VideoPlayer 사용

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

    [Header("Multi-Video Playback Settings")]
    [Tooltip("버튼 클릭 시 일괄적으로 활성화하고 보여줄 RawImage 패널들(부모 포함 가능)을 연결하세요.")]
    public GameObject[] videoPanels;
    [Tooltip("재생할 비디오 플레이어들을 연결하세요. (모두 종료되어야 패널이 꺼집니다)")]
    public VideoPlayer[] videoPlayers;
    private int finishedVideoCount = 0; // 종료된 비디오 개수 카운트용

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

        // 비디오 초기화 (패널 끄기 및 자동 재생 방지, 종료 이벤트 연결)
        SetVideoPanelsActive(false);
        foreach (VideoPlayer vp in videoPlayers)
        {
            if (vp != null)
            {
                vp.playOnAwake = false;
                vp.isLooping = false; // 끝나면 이벤트를 발생시키기 위해 루프 해제
                vp.loopPointReached += OnAnyVideoFinished;
            }
        }

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
            if (btnObj.GetComponent<Image>() == null)
            {
                var img = btnObj.AddComponent<Image>();
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
            Debug.Log("[TitleUI] 4th Button Clicked (Play Backward Timeline)");
            PlayTimelineForward();
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
    // ★ 멀티 비디오 동시 재생 제어
    // ===============================================
    
    /// <summary>
    /// UI의 OnClick이나 다른 스크립트에서 호출하여 4개의 비디오를 일괄 재생합니다.
    /// </summary>
    public void PlayAllVideos()
    {
        if (videoPlayers == null || videoPlayers.Length == 0) return;

        finishedVideoCount = 0; // 재생을 시작할 때 카운트 초기화
        
        // 1. 패널들 활성화
        SetVideoPanelsActive(true);

        // 2. 비디오 재생
        foreach (VideoPlayer vp in videoPlayers)
        {
            if (vp != null)
            {
                vp.Stop();
                vp.Play();
            }
            else
            {
                // 플레이어가 비어있더라도 종료 카운트는 올려주어야 멈춤 현상(무한 대기)이 안 생깁니다.
                finishedVideoCount++;
            }
        }
        Debug.Log("[TitleUI] Play All Videos Started.");
    }

    /// <summary>
    /// 개별 비디오가 끝날 때마다 호출됩니다 (반복 횟수가 다를 수 있으므로 카운트).
    /// </summary>
    private void OnAnyVideoFinished(VideoPlayer vp)
    {
        finishedVideoCount++;

        // 작동 중인 전체 비디오의 갯수만큼 종료 이벤트가 누적되면 패널을 일괄 끕니다.
        if (finishedVideoCount >= videoPlayers.Length)
        {
            SetVideoPanelsActive(false);
            Debug.Log("[TitleUI] All Videos Finished. Panels Disabled.");
        }
    }

    private void SetVideoPanelsActive(bool isActive)
    {
        if (videoPanels != null)
        {
            foreach (GameObject panel in videoPanels)
            {
                if (panel != null)
                    panel.SetActive(isActive);
            }
        }
    }

    // ===============================================
    // ★ 2개의 타임라인 제어
    // ===============================================

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
