/**
 * [수정 내역 - 팀 공유용]
 * 1. 결과 동기화: 멀티플레이 시 마스터 클라이언트의 결과값을 파라미터로 전달받아 모든 클라이언트가 동일한 애니메이션 결과를 보게 함
 * 2. StartToss : forcedResult 인자를 받는 메서드 구현으로 네트워크 패킷 연동
 */
using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class CoinToss : MonoBehaviour
{
    [Header("Coin Manager Reference")]
    public CoinManager coinManager; // 매니저에게 결과 보고용

    [Header("Toss Animation Settings")]
    public float tossHeight = 300f;
    public float tossDuration = 2f;
    public float flipSpeed = 5f;

    [Header("Coin Sprites (50% Chance)")]
    public Sprite frontSprite; // 앞면 이미지 (승리)
    public Sprite backSprite;  // 뒷면 이미지 (패배)

    [Header("Audio Settings")]
    public AudioSource audioSource; // 오디오 소스
    public AudioClip tossSound;     // 동전 돌아가는 소리 클립

    private RectTransform rectTransform;
    private Image coinImage;
    private Vector2 originalPosition;
    private bool isTossing = false;
    private bool isInitialized = false;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        rectTransform = GetComponent<RectTransform>();
        coinImage = GetComponent<Image>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        originalPosition = rectTransform.anchoredPosition;
        isInitialized = true;
    }

    private void Start()
    {
        Initialize();
    }

    public void StartToss(int forcedResult = -1)
    {
        Initialize();

        if (!isTossing)
        {
            StartCoroutine(TossRoutine(forcedResult));
        }
    }

    private IEnumerator TossRoutine(int forcedResult)
    {
        isTossing = true;
        float elapsed = 0f;

        // ★ 동전 던지기 시작 시 원샷으로 소리 재생
        if (audioSource != null && tossSound != null)
        {
            audioSource.PlayOneShot(tossSound);
        }

        int result = (forcedResult == -1) ? Random.Range(0, 2) : forcedResult;
        coinImage.color = Color.white;

        while (elapsed < tossDuration)
        {
            elapsed += Time.deltaTime;
            float timePercent = elapsed / tossDuration;

            // 높이 계산
            float heightOffset = Mathf.Sin(timePercent * Mathf.PI) * tossHeight;
            rectTransform.anchoredPosition = originalPosition + new Vector2(0, heightOffset);

            // 회전 연출: 크기가 양수/음수를 오감
            float rawScaleY = Mathf.Cos(timePercent * Mathf.PI * flipSpeed * 2f);

            // 절댓값(Abs)을 사용하여 이미지가 상하반전(거꾸로)되는 것을 막음
            rectTransform.localScale = new Vector3(1f, Mathf.Abs(rawScaleY), 1f);

            // rawScaleY가 양수일 땐 앞면, 음수일 땐 뒷면을 실시간으로 띄워줍니다.
            if (rawScaleY >= 0)
            {
                coinImage.sprite = frontSprite;
            }
            else
            {
                coinImage.sprite = backSprite;
            }

            yield return null;
        }

        // 애니메이션이 끝나면 원래 위치와 결과 이미지로 셋팅
        rectTransform.anchoredPosition = originalPosition;
        rectTransform.localScale = Vector3.one;
        coinImage.sprite = (result == 0) ? frontSprite : backSprite;

        // 매니저에게 결과 하달
        if (coinManager != null)
        {
            if (result == 0) coinManager.TossResultWin();
            else coinManager.TossResultLose();
        }
        else
        {
            Debug.LogWarning("CoinToss에 CoinManager가 연결되지 않았습니다.");
        }

        isTossing = false;
    }
}


