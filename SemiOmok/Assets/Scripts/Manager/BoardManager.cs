/**
 * [수정 내역 - 팀 공유용]
 * 1. 금수 시각화 제한: 흑돌 금수(X) 마커가 흑돌 플레이어 본인에게만 보이도록 수정 (백돌 UX 개선)
 * 2. 턴 동기화 연동: GameManager의 턴 변경 이벤트에 맞춰 실시간으로 금수 위치 업데이트
 */
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Manager.Network;

[RequireComponent(typeof(AudioSource))]
public class BoardManager : MonoBehaviour
{
    [Header("Game Manager Reference")]
    public GameManager gameManager;

    [Header("Stone Prefabs")]
    public GameObject blackStonePrefab;
    public GameObject whiteStonePrefab;
    public GameObject forbiddenMarkPrefab; 
    
    [Header("Last Stone Marker")]
    [Tooltip("마지막으로 둔 돌 위치에 띄울 마커(빨간 점 등) 프리팹")]
    public GameObject lastStoneMarkerPrefab;
    [Tooltip("마커의 위치를 미세 조절합니다. (X, Y, Z)")]
    public Vector3 markerPositionOffset = new(0f, 0.05f, 0f); 
    [Tooltip("마커의 기본 회전값을 조절합니다. (기본 X축 90도)")]
    public Vector3 markerRotationOffset = new(90f, 0f, 0f); 
    private GameObject currentLastMarker; 

    [Header("Board Visual Settings")]
    public float gridSizeX = 0.05f; 
    public float gridSizeZ = 0.05f; 
    public Vector3 boardOrigin = new(-0.35f, 0, -0.35f);
    public Vector3 boardRotation = Vector3.zero; 
    public float spawnHeight = 0.5f;

    [Header("Stone Rotation Settings")]
    public Vector3 stoneRotationOffset = new(90f, 0f, 0f); 

    [Header("Pool Settings")]
    public int defaultPoolSize = 50; 
    private readonly Queue<GameObject> blackStonePool = new();
    private readonly Queue<GameObject> whiteStonePool = new();
    private Transform poolParent;

    [Header("Audio Settings")]
    public AudioSource audioSource;           
    public AudioClip blackStonePlaceSound;    
    public AudioClip whiteStonePlaceSound;    

    [Header("Preview Settings")]
    [Range(0f, 1f)]
    public float previewAlpha = 0.5f;
    private GameObject previewBlack;
    private GameObject previewWhite;

    private readonly List<GameObject> activeForbiddenMarks = new();
    
    private bool isSpaceViewMode = false;
    private bool isHiddenByViewModeDrop = false; 
       
    private void Start()
    {
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        InitializeStonePools(); 

        previewBlack = CreatePreviewObject(blackStonePrefab);
        previewWhite = CreatePreviewObject(whiteStonePrefab);

        if (lastStoneMarkerPrefab != null)
        {
            currentLastMarker = Instantiate(lastStoneMarkerPrefab, this.transform);
            currentLastMarker.SetActive(false);
        }

        if (gameManager != null)
        {
            gameManager.OnTurnChanged += UpdateForbiddenMarks;
            gameManager.OnStonePlaced += SpawnStoneVisual;
        }
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnTurnChanged -= UpdateForbiddenMarks;
            gameManager.OnStonePlaced -= SpawnStoneVisual;
        }
    }

    private void Update()
    {
        if (gameManager != null && gameManager.isGameOver)
        {
            HidePreviews();
            return;
        }

        UpdatePreview();

        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }
    }

    private void InitializeStonePools()
    {
        GameObject parentObj = new("StonePools");
        parentObj.transform.SetParent(this.transform);
        poolParent = parentObj.transform;

        for (int i = 0; i < defaultPoolSize; i++)
        {
            AddStoneToPool(GameManager.Player.Black);
            AddStoneToPool(GameManager.Player.White);
        }
    }

    private void AddStoneToPool(GameManager.Player player)
    {
        GameObject prefab = (player == GameManager.Player.Black) ? blackStonePrefab : whiteStonePrefab;
        if (prefab == null) return;

        GameObject stone = Instantiate(prefab, poolParent);
        stone.SetActive(false); 

        if (player == GameManager.Player.Black)
            blackStonePool.Enqueue(stone);
        else
            whiteStonePool.Enqueue(stone);
    }

    private Vector3 GetWorldPosition(int x, int y, float localHeightOffset = 0f)
    {
        Vector3 localPos = new(x * gridSizeX, localHeightOffset, y * gridSizeZ);
        return boardOrigin + Quaternion.Euler(boardRotation) * localPos;
    }

    private Vector2Int GetGridIndex(Vector3 worldPosition)
    {
        Vector3 diff = worldPosition - boardOrigin;
        Vector3 localDiff = Quaternion.Inverse(Quaternion.Euler(boardRotation)) * diff;
        
        int x = Mathf.RoundToInt(localDiff.x / gridSizeX);
        int z = Mathf.RoundToInt(localDiff.z / gridSizeZ);
        
        return new Vector2Int(x, z);
    }

    private Plane GetBoardPlane()
    {
        Vector3 planeNormal = Quaternion.Euler(boardRotation) * Vector3.up;
        return new Plane(planeNormal, boardOrigin);
    }

    private void UpdateForbiddenMarks(GameManager.Player currentPlayer)
    {
        foreach (GameObject mark in activeForbiddenMarks)
        {
            Destroy(mark);
        }
        activeForbiddenMarks.Clear();

        // [NET][FIX] 금수 표시는 흑돌 턴이면서, 본인이 흑돌일 때만 표시합니다.
        // 백돌 플레이어에게는 흑의 금수 위치를 보여줄 필요가 없으며 UX상 부자연스럽습니다.
        if (currentPlayer == GameManager.Player.Black && gameManager.localPlayer == GameManager.Player.Black)
        {
            for (int x = 0; x < gameManager.boardSize; x++)
            {
                for (int y = 0; y < gameManager.boardSize; y++)
                {
                    if (gameManager.GetCellState(x, y) == GameManager.Player.None &&
                        gameManager.IsForbidden(x, y, currentPlayer))
                    {
                        Vector3 markPos = GetWorldPosition(x, y, 0.01f);

                        if (forbiddenMarkPrefab != null)
                        {
                            GameObject mark = Instantiate(forbiddenMarkPrefab, markPos, Quaternion.Euler(boardRotation), this.transform);
                            activeForbiddenMarks.Add(mark);
                        }
                    }
                }
            }
        }
    }

    private GameObject CreatePreviewObject(GameObject prefab)
    {
        if (prefab == null) return null;

        GameObject previewObj = Instantiate(prefab);
        previewObj.name = prefab.name + "_Preview";

        if (previewObj.TryGetComponent(out Rigidbody rb)) Destroy(rb);
        if (previewObj.TryGetComponent(out Collider col)) Destroy(col);

        Renderer[] renderers = previewObj.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            foreach (Material mat in r.materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    Color color = mat.color;
                    color.a = previewAlpha;
                    mat.color = color;
                }
            }
        }

        previewObj.SetActive(false);
        return previewObj;
    }

    private void UpdatePreview()
    {
        if (isSpaceViewMode || (gameManager != null && (gameManager.isSpaceHeld || gameManager.isForcedLookingForward))) 
        {
            HidePreviews();
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane boardPlane = GetBoardPlane();

        if (boardPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector2Int gridIndex = GetGridIndex(hitPoint);

            int x = gridIndex.x;
            int y = gridIndex.y;

            if (x >= 0 && x < gameManager.boardSize &&
                y >= 0 && y < gameManager.boardSize &&
                gameManager.GetCellState(x, y) == GameManager.Player.None)
            {
                GameManager.Player currentPlayer = gameManager.currentPlayer;

                // [NET][FIX] 멀티플레이 시 내 차례가 아니거나, 매칭/코인토스 중이면 미리보기를 숨깁니다.
                bool isMyTurn = (gameManager.currentMode == GameManager.GameMode.Local) || (currentPlayer == gameManager.localPlayer);
                bool isForbiddenForLocal = (gameManager.localPlayer == GameManager.Player.Black && gameManager.IsForbidden(x, y, currentPlayer));
                
                bool isInputBlocked = (gameManager.matchingPanel != null && gameManager.matchingPanel.activeSelf) || 
                                     (gameManager.coinTossPanel != null && gameManager.coinTossPanel.activeSelf);

                if (!isMyTurn || isForbiddenForLocal || isInputBlocked)
                {
                    HidePreviews();
                    return;
                }

                bool isBlackTurn = (currentPlayer == GameManager.Player.Black);

                if (previewBlack) previewBlack.SetActive(isBlackTurn);
                if (previewWhite) previewWhite.SetActive(!isBlackTurn);

                GameObject activePreview = isBlackTurn ? previewBlack : previewWhite;

                if (activePreview != null)
                {
                    activePreview.transform.SetPositionAndRotation(GetWorldPosition(x, y, 0f), Quaternion.Euler(boardRotation) * Quaternion.Euler(stoneRotationOffset));
                }
                return; 
            }
        }

        HidePreviews();
    }

    private void HidePreviews()
    {
        if (previewBlack) previewBlack.SetActive(false);
        if (previewWhite) previewWhite.SetActive(false);
    }

    private void HandleMouseClick()
    {
        if (isSpaceViewMode || (gameManager != null && (gameManager.isSpaceHeld || gameManager.isForcedLookingForward))) return;

        // [NET][FIX] UI 패널이 활성화되어 있을 때는 클릭 입력을 막습니다.
        if (gameManager != null)
        {
            if ((gameManager.matchingPanel != null && gameManager.matchingPanel.activeSelf) ||
                (gameManager.coinTossPanel != null && gameManager.coinTossPanel.activeSelf))
            {
                return;
            }
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane boardPlane = GetBoardPlane();

        if (boardPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector2Int gridIndex = GetGridIndex(hitPoint);

            int x = gridIndex.x;
            int y = gridIndex.y;

            if (gameManager.currentMode == GameManager.GameMode.MultiPlay)
            {
                // [NET][FIX] 멀티플레이 시 마스터에게 착수 요청
                if (NetworkTurnManager.Instance != null)
                {
                    NetworkTurnManager.Instance.RequestStonePlacement(x, y);
                }
            }
            else
            {
                // 로컬 모드 시 즉시 착수
                gameManager.PlaceStone(x, y, gameManager.localPlayer); 
            }
        }
    }

    /// <summary>
    /// [NET][FIX] 네트워크로부터 확정된 착수 정보를 받아 실행
    /// </summary>
    public void PlaceStoneFromNetwork(int x, int y, GameManager.Player remoteColor)
    {
        bool success = gameManager.PlaceStone(x, y, remoteColor);
        Debug.Log($"[BoardManager] 네트워크 착수 처리: ({x}, {y}), color={remoteColor}, success={success}");
    }

    /// <summary>
    /// [NET][FIX] 보드 위의 모든 돌과 마커를 제거하고 판을 초기화합니다.
    /// </summary>
    public void ClearBoard()
    {
        if (poolParent == null) return;

        // [FIX] 모든 돌을 비활성화하고 풀로 회수
        blackStonePool.Clear();
        whiteStonePool.Clear();

        foreach (Transform child in poolParent)
        {
            child.gameObject.SetActive(false);
            
            // 이름에 따라 적절한 큐에 다시 삽입
            if (child.name.Contains("Black"))
                blackStonePool.Enqueue(child.gameObject);
            else if (child.name.Contains("White"))
                whiteStonePool.Enqueue(child.gameObject);
        }

        // 마지막 수 마커 비활성화
        if (currentLastMarker != null)
        {
            currentLastMarker.SetActive(false);
        }

        Debug.Log("[BoardManager] 보드가 초기화되었습니다. (돌 풀 회수 완료)");
    }

    private void SpawnStoneVisual(int x, int y, GameManager.Player player)
    {
        Debug.Log($"[BoardManager] SpawnStoneVisual 호출됨: ({x}, {y}), Player: {player}");
        Vector3 spawnPos = GetWorldPosition(x, y, spawnHeight);
        Quaternion finalRotation = Quaternion.Euler(boardRotation) * Quaternion.Euler(stoneRotationOffset);

        Queue<GameObject> pool = (player == GameManager.Player.Black) ? blackStonePool : whiteStonePool;
        GameObject stoneToPlace = null;

        // [FIX] 풀에서 유효한 오브젝트를 찾을 때까지 추출 (파괴된 객체 방지)
        while (pool.Count > 0 && stoneToPlace == null)
        {
            stoneToPlace = pool.Dequeue();
        }

        if (stoneToPlace == null)
        {
            GameObject prefab = (player == GameManager.Player.Black) ? blackStonePrefab : whiteStonePrefab;
            if (prefab != null)
            {
                stoneToPlace = Instantiate(prefab, poolParent);
                Debug.Log($"[BoardManager] 풀에 유효한 돌이 없어 새로 생성함: {prefab.name}");
            }
        }

        if (stoneToPlace != null)
        {
            stoneToPlace.transform.SetPositionAndRotation(spawnPos, finalRotation);
            stoneToPlace.SetActive(true);
            Debug.Log($"[BoardManager] 돌 활성화 완료: {stoneToPlace.name} at {spawnPos}");
        }
        else
        {
            Debug.LogError("[BoardManager] 돌 오브젝트를 생성하거나 가져오는데 실패했습니다!");
        }

        if (currentLastMarker != null)
        {
            // ★ GameManager의 실제 spaceHeld 상태를 직접 확인하여 완벽 차단
            bool isCurrentlyHoldingSpace = (gameManager != null && gameManager.isSpaceHeld);

            if (isCurrentlyHoldingSpace) 
            {
                // 상대가 내가 스페이스 누른 사이에 돌을 둠 -> 영구 숨김 처리
                isHiddenByViewModeDrop = true;
                
                // SetActive 뿐만 아니라 아예 카메라 안 보이는 지하실로 던져버림 (2중 방어선)
                currentLastMarker.transform.position = new Vector3(0, -999f, 0); 
                currentLastMarker.SetActive(false);
            }
            else 
            {
                // 정상적으로 돌을 둠 -> 마커 위치 및 각도 갱신
                isHiddenByViewModeDrop = false;

                Vector3 worldOffset = Quaternion.Euler(boardRotation) * markerPositionOffset;
                currentLastMarker.transform.SetPositionAndRotation(spawnPos + worldOffset, Quaternion.Euler(boardRotation) * Quaternion.Euler(markerRotationOffset)); 

                currentLastMarker.SetActive(true);
            }
        }

        AudioClip soundToPlay = (player == GameManager.Player.Black) ? blackStonePlaceSound : whiteStonePlaceSound;
        if (audioSource != null && soundToPlay != null)
        {
            audioSource.PlayOneShot(soundToPlay);
        }
        
        HidePreviews();
    }

    private void OnDrawGizmos()
    {
        int size = (gameManager != null) ? gameManager.boardSize : 15;
        Gizmos.color = Color.red;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector3 pos = GetWorldPosition(x, y, 0f);
                Gizmos.DrawWireSphere(pos, Mathf.Min(gridSizeX, gridSizeZ) * 0.2f);
            }
        }
    }

    public void UpdateMarkerVisibility(bool isVisible)
    {
        isSpaceViewMode = !isVisible; 

        if (currentLastMarker != null)
        {
            if (!isVisible) 
            {
                // 스페이스바 누른 순간 -> 무조건 끈다.
                currentLastMarker.SetActive(false);
            }
            else 
            {
                // 스페이스바 뗀 순간 -> 
                // 누른 도중 착수가 있었다면 계속 꺼둠, 아니면 원래대로 켬
                if (isHiddenByViewModeDrop)
                {
                    currentLastMarker.SetActive(false);
                }
                else
                {
                    currentLastMarker.SetActive(true);
                }
            }
        }
    }
}