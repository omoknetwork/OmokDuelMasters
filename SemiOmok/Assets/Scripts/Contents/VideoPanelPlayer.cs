using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;


public class VideoPanelPlayer : MonoBehaviour
{
    [Header("UI")]
    public GameObject videoPanel;

    [Header("Main Video Settings")]
    public VideoPlayer videoPlayer;
    [Tooltip("승리 시 재생할 메인 영상 클립")]
    public VideoClip winClip;

    [Tooltip("패배 시 재생할 메인 영상 클립")]
    public VideoClip loseClip;


    [Header("Reaction Video Settings (New)")]
    [Tooltip("캐릭터 반응 등을 틀어줄 두 번째 비디오 플레이어를 연결하세요.")]
    public VideoPlayer reactionVideoPlayer;


    [Tooltip("승리 시 재생할 웃는 영상")]
    public VideoClip winReactionClip;
    [Tooltip("오목으로 패배 시 재생할 우는 영상")]
    public VideoClip loseGomokuReactionClip;
    [Tooltip("체력 고갈(선생님 발각) 패배 시 재생할 혼나는 영상")]
    public VideoClip loseCaughtReactionClip;

    private bool isMainPrepared = false;
    private bool isReactionPrepared = false;
    private Vector3 originalPanelScale = Vector3.one;

    [Header("UI Settings")]
    public GameObject resultBox; // 영상을 틀어줄 결과 텍스트 패널
    [Tooltip("승리/패배의 이유를 띄워줄 TextMeshPro 텍스트를 연결하세요.")]
    public TextMeshProUGUI reasonText; // 이유 표시용 텍스트
    private void Awake()
    {
        // [NET][FIX] 오브젝트를 끄면 Prepare가 작동하지 않으므로, 스케일을 0으로 만들어 활성 상태를 유지하며 숨깁니다.
        if (videoPanel != null)
        {
            originalPanelScale = videoPanel.transform.localScale;
            // [NET][FIX] 초기 스케일이 0이라면 강제로 1로 설정하여 안 보이는 버그 방지
            if (originalPanelScale == Vector3.zero) originalPanelScale = Vector3.one;

            videoPanel.transform.localScale = Vector3.zero;
            videoPanel.SetActive(true);
        }

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;

            // 메인 영상이 무한 반복되지 않고 딱 1회만 실행되게 합니다.
            videoPlayer.isLooping = false;
            // [Senior Fix] 프레임 드랍 시 건너뛰기를 허용하여 재생 속도가 느려지는 현상 방지
            videoPlayer.skipOnDrop = true;

            // 영상이 끝났을 때(1회 컷) 실행할 이벤트 연결

            videoPlayer.loopPointReached += OnVideoFinished;

            // [추가] 비디오 준비 완료 이벤트 연결
            videoPlayer.prepareCompleted += (vp) => isMainPrepared = true;
        }

        if (reactionVideoPlayer != null)
        {
            reactionVideoPlayer.playOnAwake = false;
            // 리액션 비디오는 유니티 에디터 인스펙터의 Loop 설정 켬/끔 사항을 따르게 둡니다.

            // [추가] 리액션 비디오 준비 완료 이벤트 연결
            reactionVideoPlayer.prepareCompleted += (vp) => isReactionPrepared = true;
        }
    }

    public void PrepareResultVideo(bool isWin, string reason)
    {
        Debug.Log($"[VideoTrace] {Time.time:F2}s : PrepareResultVideo 호출 (승리여부: {isWin}, 사유: {reason})");
        isMainPrepared = false;
        isReactionPrepared = false;

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = isWin ? winClip : loseClip;
            videoPlayer.Prepare();
        }

        if (reactionVideoPlayer != null)
        {
            reactionVideoPlayer.Stop();
            if (isWin) reactionVideoPlayer.clip = winReactionClip;
            else
            {
                if (reason.Contains("발각") || reason.Contains("선생님") || reason.Contains("체력"))
                    reactionVideoPlayer.clip = loseCaughtReactionClip;
                else
                    reactionVideoPlayer.clip = loseGomokuReactionClip;
            }
            reactionVideoPlayer.Prepare();
        }
    }

    /// <summary>
    /// 승리/패배 여부와 이유에 따라 두 개의 영상(메인, 리액션)을 플레이합니다.
    /// </summary>
    /// <param name="isWin">true면 승리, false면 패배</param>
    /// <param name="reason">승패의 이유</param>
    public void PlayResultVideo(bool isWin, string reason = "")
    {
        // 결과 패널 보이기 (스케일 복구)
        if (videoPanel != null)
            videoPanel.transform.localScale = originalPanelScale;

        if (resultBox != null)
            resultBox.SetActive(true);

        // 이유 텍스트 적용
        if (reasonText != null)
            reasonText.text = reason;

        // [최적화] 코루틴을 통해 준비 상태 확인 후 재생
        StartCoroutine(PlayVideoRoutine());
    }

    private IEnumerator PlayVideoRoutine()
    {
        float startTime = Time.time;
        // [NET][FIX] 엔진의 준비 상태(isPrepared)를 직접 확인하고 타임아웃을 0.1초로 최소화하여 즉시 재생을 보장합니다.
        float timeout = 0.1f;

        while (timeout > 0)
        {
            bool mainReady = (videoPlayer == null) || videoPlayer.isPrepared || isMainPrepared;
            bool reactionReady = (reactionVideoPlayer == null) || reactionVideoPlayer.isPrepared || isReactionPrepared;

            if (mainReady && reactionReady)
            {
                Debug.Log($"[VideoTrace] {Time.time:F2}s : 영상 준비 완료 확인 (대기시간: {(Time.time - startTime):F4}s)");
                break;

            }

            timeout -= Time.deltaTime;
            yield return null;
        }

        if (timeout <= 0) Debug.LogWarning($"[VideoTrace] {Time.time:F2}s : 영상 준비 타임아웃 발생 (강제 재생)");

        // [NET][FIX] 영상이 슬로우 모션으로 재생되는 것을 방지하기 위해 타임스케일을 복구합니다.
        if (Time.timeScale < 1f) Time.timeScale = 1f;

        Debug.Log($"[VideoTrace] {Time.time:F2}s : 실제 Play() 호출");
        if (videoPlayer != null) videoPlayer.Play();
        if (reactionVideoPlayer != null) reactionVideoPlayer.Play();
    }

    /// <summary>
    /// 기존 호환성을 위해 남겨둔 기본 영상 재생 함수
    /// </summary>
    public void PlayVideo()
    {
        if (videoPanel != null)
        {
            // [NET][FIX] 숨겨진 패널의 스케일을 복구하여 화면에 표시합니다.
            videoPanel.transform.localScale = originalPanelScale;
            videoPanel.SetActive(true);
        }
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.Play();
        }
    }

    public void SkipVideo()
    {
        if (videoPlayer != null) videoPlayer.Stop();
        if (reactionVideoPlayer != null) reactionVideoPlayer.Stop();
        // [NET][FIX] 패널을 끄는 대신 스케일을 0으로 만들어 백그라운드 준비 상태를 유지합니다.
        if (videoPanel != null) videoPanel.transform.localScale = Vector3.zero;
    }

    /// <summary>
    /// 메인 영상 1회 재생이 완전히 끝났을 때 자동으로 호출됩니다.
    /// </summary>
    private void OnVideoFinished(VideoPlayer vp)
    {
        // 필요시 리액션 등 다른 로직 갱신에 사용할 수 있음
    }

    // ★ 타이틀로 돌아가기 버튼에 연결할 함수
    public void GoToTitle()
    {
        // 타임스케일 복구
        Time.timeScale = 1f;

        // UIManager의 GoToTitle을 호출하여 안전한 방 퇴장 및 씬 전환 수행
        UIManager ui = FindAnyObjectByType<UIManager>();
        if (ui != null)
        {
            ui.GoToTitle();
        }
        else
        {
            // UIManager를 찾을 수 없는 경우 직접 씬 로드 (비상용)
            SceneManager.LoadScene("Title");
        }
    }

    // ★ 재시작 버튼의 OnClick() 이벤트 등에 연결할 재시작 함수
    public void RestartScene()
    {
        // 타임스케일이 멈춰있을 수 있으니 원래대로 돌려놓습니다.
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}