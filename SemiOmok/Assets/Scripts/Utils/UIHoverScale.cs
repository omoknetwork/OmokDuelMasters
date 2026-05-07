using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Scale Settings")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float animationSpeed = 10f;

    [Header("Click Events")]
    public UnityEvent onClick;

    [Header("Sound Settings")]
    [SerializeField] private AudioClip hoverSound;
    private AudioSource audioSource;

    private Vector3 originalScale;
    private Vector3 targetScale;

    private void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;

        // 사운드 재생을 위한 AudioSource 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void Update()
    {
        // 부드럽게 스케일 변경 (Lerp)
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * hoverScale;

        // 마우스를 올렸을 때만 소리 재생
        if (hoverSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hoverSound);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 클릭 시 이벤트 실행
        onClick?.Invoke();
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 스케일 초기화 (버그 방지)
        transform.localScale = originalScale;
        targetScale = originalScale;
    }
}
