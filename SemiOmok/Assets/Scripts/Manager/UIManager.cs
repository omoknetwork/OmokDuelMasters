using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;

using UnityEngine.SceneManagement;

using UnityEngine.UI;


public class UIManager : MonoBehaviourPunCallbacks
{
    [Header("Menu UI")]
    [Tooltip("ESC를 눌렀을 때 띄울 메뉴 패널을 연결하세요.")]
    public GameObject menuPanel;
    private bool isMenuOpen = false;

    // ==========================================
    // ★ 4종류의 호버 타겟 및 사운드 연결부
    // ==========================================
    [Header("UI Hover Effects - Chalk")]
    [Tooltip("분필 느낌이 나는 UI 요소들을 여기에 연결하세요.")]
    public Image[] chalkHoverImages;

    public AudioClip chalkHoverSoundClip;

    [Header("UI Hover Effects - Other")]
    [Tooltip("그 외의 일반 UI 요소들을 여기에 연결하세요.")]
    public Image[] otherHoverImages;

    public AudioClip otherHoverSoundClip;

    [Header("UI Hover Effects - Re (Restart)")]
    [Tooltip("Restart 등 다시하기 관련 메뉴 이미지들을 연결하세요.")]
    public Image[] reHoverImages;
    public AudioClip reHoverSoundClip;

    [Header("UI Hover Effects - Title")]
    [Tooltip("Title로 가기, 메인 화면 관련 메뉴 이미지들을 연결하세요.")]
    public Image[] titleHoverImages;
    public AudioClip titleHoverSoundClip;
    // ==========================================

    [Header("Hover Common Settings")]
    public float hoverScaleMultiplier = 1.2f;

    private Vector3 originalScale = Vector3.one;

    [Header("Audio Settings")]
    public AudioSource audioSource;

    [Header("Scene Settings")]
    public string titleSceneName = "Title";

    [Header("Custom Cursor Settings")]
    public GameObject customCursorPrefab;

    public RectTransform canvasTransform;

    public bool hideDefaultCursor = true;
    public Vector3 cursorOffset = Vector3.zero;
    public Vector3 cursorScale = Vector3.one;
    public Vector3 cursorRotation = Vector3.zero;

    private Camera mainCam;
    private RectTransform actualCursor;

    private Canvas parentCanvas;

    private void Start()
    {
        mainCam = Camera.main;

        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
            isMenuOpen = false;
        }

        if (hideDefaultCursor)
        {
            Cursor.visible = false;
        }

        // 현재 씬이 타이틀 화면이 아닐 때만 커스텀 커서를 생성합니다.
        if (SceneManager.GetActiveScene().name != titleSceneName)
        {
            if (customCursorPrefab != null && canvasTransform != null)
            {
                GameObject spawnedCursor = Instantiate(customCursorPrefab, canvasTransform);
                actualCursor = spawnedCursor.GetComponent<RectTransform>();


                actualCursor.anchorMin = new Vector2(0.5f, 0.5f);
                actualCursor.anchorMax = new Vector2(0.5f, 0.5f);
                actualCursor.pivot = new Vector2(0.5f, 0.5f);

                actualCursor.localScale = cursorScale;
                actualCursor.localRotation = Quaternion.Euler(cursorRotation);

                actualCursor.SetAsLastSibling();

                parentCanvas = canvasTransform.GetComponentInParent<Canvas>();
            }
        }

        if (audioSource == null)

            audioSource = GetComponent<AudioSource>();

        // 배열에 등록된 모든 이미지에 각각의 호버 이벤트를 일괄 적용합니다.
        BindHoverEventsForArray(chalkHoverImages, chalkHoverSoundClip);
        BindHoverEventsForArray(otherHoverImages, otherHoverSoundClip);
        BindHoverEventsForArray(reHoverImages, reHoverSoundClip);
        BindHoverEventsForArray(titleHoverImages, titleHoverSoundClip);
    }

    private void Update()
    {
        // ESC 키 입력 감지: 메뉴창 켜기/끄기 (게임 종료 시에는 막음)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // [NET][FIX] 게임 종료 후 결과 영상이 나올 때는 메뉴창을 띄우지 못하게 합니다.
            if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

            ToggleMenu();
        }

        if (actualCursor != null && canvasTransform != null && parentCanvas != null)
        {
            actualCursor.SetAsLastSibling();

            Vector2 mousePos = Input.mousePosition;
            Vector2 localPoint;

            Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasTransform, mousePos, cam, out localPoint))
            {
                actualCursor.localPosition = new Vector3(localPoint.x, localPoint.y, 0f) + cursorOffset;
            }
        }
    }

    // ==========================================
    // ★ 인게임 메뉴 컨트롤 함수
    // ==========================================

    public void ToggleMenu()
    {
        if (menuPanel == null) return;

        isMenuOpen = !isMenuOpen;
        menuPanel.SetActive(isMenuOpen);

        // 메뉴가 열리면 게임 정지
        Time.timeScale = isMenuOpen ? 0f : 1f;

        if (isMenuOpen)
        {
            SetCursorVisible(true);
        }
    }

    public void ResumeGame()
    {
        if (isMenuOpen)
        {
            ToggleMenu();
        }
    }

    // ==========================================
    // ★ 호버 이벤트 및 바인딩 로직
    // ==========================================

    private void BindHoverEventsForArray(Image[] imageArray, AudioClip clip)
    {
        if (imageArray == null) return;

        foreach (Image img in imageArray)
        {
            if (img != null)
            {
                SetupHoverEvents(img.gameObject, clip);
            }
        }
    }

    private void SetupHoverEvents(GameObject targetObj, AudioClip hoverClip)
    {
        EventTrigger trigger = targetObj.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = targetObj.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { OnHoverEnter(targetObj.transform, hoverClip); });
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { OnHoverExit(targetObj.transform); });
        trigger.triggers.Add(exitEntry);
    }

    private void OnHoverEnter(Transform targetTransform, AudioClip clip)
    {
        targetTransform.localScale = originalScale * hoverScaleMultiplier;

        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void OnHoverExit(Transform targetTransform)
    {
        targetTransform.localScale = originalScale;
    }

    // ==========================================

    // [NET][FIX] 방 퇴장 완료 후 씬 전환을 보장하기 위한 콜백
    public override void OnLeftRoom()
    {
        Debug.Log($"[UIManager][NET][FIX] 방 퇴장 완료. {titleSceneName} 씬으로 이동합니다.");
        SceneManager.LoadScene(titleSceneName);
    }

    public void GoToTitle()
    {
        Debug.Log($"[UIManager] GoToTitle 호출됨. 대상 씬: {titleSceneName}");


        Time.timeScale = 1f;

        // [NET][FIX] 방에 있는 경우 즉시 씬을 옮기지 않고, 방을 먼저 나갑니다.
        // 씬 이동은 OnLeftRoom() 콜백에서 처리됩니다.

        if (Photon.Pun.PhotonNetwork.InRoom)
        {
            Debug.Log("[UIManager][NET][FIX] Photon 방 나가는 중... (OnLeftRoom 대기)");
            Photon.Pun.PhotonNetwork.LeaveRoom();
        }
        else
        {
            Debug.Log($"[UIManager] 방에 있지 않으므로 즉시 {titleSceneName} 씬 로드");
            SceneManager.LoadScene(titleSceneName);
        }
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void SetCursorVisible(bool isVisible)
    {
        if (actualCursor != null)
        {
            actualCursor.gameObject.SetActive(isVisible);
        }
    }
}
