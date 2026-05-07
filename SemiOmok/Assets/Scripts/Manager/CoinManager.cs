/**
 * [수정 내역 - 팀 공유용]
 * 1. 대기 UX 개선: 코인 토스 패배자도 상대의 선택이 끝날 때까지 화면을 유지하며 진행 상황 안내 (상태 텍스트 업데이트)
 * 2. 종료 통보 로직: UI가 닫힐 때 GameMatchingPanelManager에 알려 전체 게임이 시작되도록 트리거 추가
 */
using UnityEngine;
using System.Collections;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class CoinManager : MonoBehaviour
{
    [Header("Manager References")]
    public GameManager gameManager;
    public AIManager aiManager;

    [Header("UI References")]
    public GameObject mainCoinTossPanel; // 전체 패널
    public GameObject choicePanel;       // 흑백 선택 패널
    public TextMeshProUGUI resultText;   // 결과 텍스트

    [Header("Delay Settings")]
    public float closeDelay = 2f;        // 창 닫히는 시간


    private readonly WaitForSeconds coinTossWait = new(2.5f);
    private readonly WaitForSeconds waitOneSecond = new(1.0f);
    private bool isSelected = false;

    /// <summary>
    /// 동전 앞면(승리)이 나왔을 때 코인에서 호출
    /// </summary>
    public void TossResultWin()
    {
        if (resultText != null)
        {
            resultText.color = Color.white;
            resultText.text = "코인 토스 승리! 돌의 색상을 선택하세요.";
        }
        if (choicePanel != null) choicePanel.SetActive(true);

        // [NET][FIX] 멀티플레이 시 10초 타임아웃 코루틴 시작
        if (gameManager != null && gameManager.currentMode == GameManager.GameMode.MultiPlay)
        {
            StartCoroutine(AutoSelectWhiteRoutine());
        }
    }

    /// <summary>
    /// 동전 뒷면(패배)이 나왔을 때 코인에서 호출
    /// </summary>
    public void TossResultLose()
    {
        if (resultText != null)
        {
            resultText.color = Color.white;
            // [NET][FIX] 멀티플레이어 환경에서는 상대방의 선택을 기다리는 대기 메시지를 표시합니다.
            if (gameManager != null && gameManager.currentMode == GameManager.GameMode.MultiPlay)
            {
                resultText.text = "상대방이 색상을 선택 중입니다...";
            }
            else
            {
                resultText.text = "당신은 '백' 후공입니다.";
            }
        }
        if (choicePanel != null) choicePanel.SetActive(false);

        if (gameManager != null && gameManager.currentMode == GameManager.GameMode.Local)
        {
            gameManager.localPlayer = GameManager.Player.White;
            if (aiManager != null) aiManager.SetAIColor(GameManager.Player.Black);
            StartCoroutine(ClosePanelRoutine());
        }
    }

    /// <summary>
    /// [NET][FIX] 멀티플레이에서 내가 선택권이 없을 때, 상대방이 선택을 완료하면 호출됩니다.
    /// </summary>
    public void ShowRemoteSelectionResult(GameManager.Player remoteColor)
    {
        if (isSelected) return; 

        GameManager.Player myColor = (remoteColor == GameManager.Player.Black) ? GameManager.Player.White : GameManager.Player.Black;

        if (resultText != null)
        {
            resultText.color = Color.white;
            string remoteColorStr = (remoteColor == GameManager.Player.Black) ? "<color=black>흑</color>" : "<color=white>백</color>";
            string myColorStr = (myColor == GameManager.Player.Black) ? "<color=black>흑</color>" : "<color=white>백</color>";
            resultText.text = $"상대가 {remoteColorStr}을 선택했습니다.\n당신은 {myColorStr}입니다!";
        }

        StartCoroutine(ClosePanelRoutine());
    }

    /// <summary>
    /// 플레이어가 UI 버튼으로 '흑'을 선택했을 때
    /// </summary>
    public void OnClickBlackButton()
    {
        if (isSelected) return;
        isSelected = true;
        StopAllCoroutines();

        if (resultText != null)
        {
            resultText.color = Color.black; 
            resultText.text = "당신이 '흑' 선공입니다.";
        }

        if (gameManager != null)
        {
            gameManager.localPlayer = GameManager.Player.Black;
            if (gameManager.currentMode == GameManager.GameMode.MultiPlay) SetStoneColorProperty(GameManager.Player.Black);
            else if (aiManager != null) aiManager.SetAIColor(GameManager.Player.White);
        }

        StartCoroutine(ClosePanelRoutine());
    }

    /// <summary>
    /// 플레이어가 UI 버튼으로 '백'을 선택했을 때
    /// </summary>
    public void OnClickWhiteButton()
    {
        if (isSelected) return;
        isSelected = true;
        StopAllCoroutines();

        if (resultText != null)
        {
            resultText.color = Color.white; 
            resultText.text = "당신이 '백' 후공입니다.";
        }

        if (gameManager != null)
        {
            gameManager.localPlayer = GameManager.Player.White;
            if (gameManager.currentMode == GameManager.GameMode.MultiPlay) SetStoneColorProperty(GameManager.Player.White);
            else if (aiManager != null) aiManager.SetAIColor(GameManager.Player.Black);
        }

        StartCoroutine(ClosePanelRoutine());
    }

    private void SetStoneColorProperty(GameManager.Player color)
    {
        if (!PhotonNetwork.InRoom) return;


        Hashtable props = new() { ["StoneColor"] = color == GameManager.Player.Black ? 1 : 2 };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        Debug.Log($"[CoinManager] 내 StoneColor 저장: {color}");
    }

    private IEnumerator ClosePanelRoutine()
    {
        yield return new WaitForSeconds(closeDelay);
        
        isSelected = false;
        if (resultText != null) resultText.text = "";
        
        // 전체 코인 토스 UI 비활성화
        if (mainCoinTossPanel != null) mainCoinTossPanel.SetActive(false);

        // [NET][FIX] 싱글 플레이(Local)에서도 타이머와 게임 로직이 정상 시작되도록 GameManager의 시작 함수를 호출합니다.
        if (gameManager != null && gameManager.currentMode == GameManager.GameMode.Local)
        {
            gameManager.StartGameAfterCoinToss(gameManager.localPlayer);
        }
        else
        {
            var gmpm = FindFirstObjectByType<Assets.Scripts.Manager.Network.GameMatchingPanelManager>();
            if (gmpm != null) gmpm.OnCoinTossFinished();
        }
    }

    public void ForceClosePanel()
    {
        StopAllCoroutines();
        isSelected = false;
        if (resultText != null) resultText.text = "";
        if (mainCoinTossPanel != null) mainCoinTossPanel.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
    }

    private IEnumerator AutoSelectWhiteRoutine()
    {
        float timer = 10f;
        while (timer > 0)
        {
            if (resultText != null) resultText.text = $"코인 토스 승리! 색상을 선택하세요. ({Mathf.CeilToInt(timer)}초)";
            yield return waitOneSecond;
            timer -= 1f;
        }
        if (!isSelected) OnClickWhiteButton();
    }
}