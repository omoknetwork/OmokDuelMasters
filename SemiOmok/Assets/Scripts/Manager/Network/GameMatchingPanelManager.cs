/**
 * [수정 내역 - 팀 공유용]
 * 1. 게임 시작 동기화: 코인 토스 종료 시점을 GameManager와 동기화하여 전원 동시 시작 처리
 * 2. OnCoinTossFinished 구현: 코인 선택 완료 후 서버 이벤트를 통해 isGameStarted 플래그 일괄 활성화
 */
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Assets.Scripts.Manager.Network
{
    [RequireComponent(typeof(PhotonView))]
    public class GameMatchingPanelManager : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        [Header("UI References")]
        [SerializeField] private GameObject matchingPanel;
        [SerializeField] private TextMeshProUGUI matchingText;
        [SerializeField] private TextMeshProUGUI playerListText;
        [SerializeField] private GameObject coinTossPanel;

        [Header("Settings")]
        [SerializeField] private byte maxPlayers = 2;

        private const byte EVENT_START_GAME = 1;
        private const byte EVENT_START_COINTOSS_UI = 2;
        private const byte EVENT_START_COUNTDOWN = 3;

        private bool isStartRequested = false;
        private bool isGameStarted = false;
        private bool isCoinTossStarted = false;

        private readonly WaitForSeconds waitOneSecond = new(1f);
        private readonly WaitForSeconds waitHalfSecond = new(0.5f);

        private void Awake()
        {
            if (coinTossPanel != null) coinTossPanel.SetActive(false);
            if (matchingPanel != null) matchingPanel.SetActive(true);
        }

        private void Start()
        {
            UpdateMatchingState();
        }

        public override void OnJoinedRoom()
        {
            UpdateMatchingState();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            UpdateMatchingState();
        }

        /// <summary>
        /// [NET][FIX] 리매치/거절/퇴장 등으로 게임을 재초기화할 때
        /// 매칭 파이프라인의 내부 상태 플래그를 모두 리셋합니다.
        /// InitializeGame()에서도 호출되어 씬 재로드 없이도 정상 재매칭이 가능하도록 합니다.
        /// </summary>
        public void ResetMatchingState()
        {
            StopAllCoroutines();
            CancelInvoke(nameof(StartGame));

            isStartRequested = false;
            isGameStarted = false;
            isCoinTossStarted = false;

            Debug.Log("[GameMatching] 매칭 상태 초기화 완료");
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log($"[GameMatching] 플레이어 퇴장 감지: {otherPlayer.NickName}");

            GameManager gm = FindAnyObjectByType<GameManager>();

            // [NET][FIX] 상대방이 나가면 보고 있던 결과창(비디오)을 강제로 닫습니다.
            if (gm != null && gm.videoPanelPlayer != null)
            {
                gm.videoPanelPlayer.SkipVideo(); // 영상 중지 및 패널 숨김
            }

            // [NET][FIX] 공용 리셋 메서드를 호출하여 매칭 상태 초기화
            ResetMatchingState();

            // [NET][FIX] 상대가 나갔으므로 NeedsRematch를 초기화합니다.
            // 새로운 플레이어가 들어오면 첫 매칭처럼 자동 코인토스가 진행됩니다.
            if (PhotonNetwork.IsMasterClient)
            {
                Hashtable clearRoom = new() { ["NeedsRematch"] = false };
                PhotonNetwork.CurrentRoom.SetCustomProperties(clearRoom);
            }

            if (coinTossPanel != null) coinTossPanel.SetActive(false);
            if (matchingPanel != null) matchingPanel.SetActive(true);
            if (matchingText != null) matchingText.text = "상대방이 퇴장했습니다. 다시 매칭을 시작합니다.";

            if (gm != null) gm.InitializeGame();

            // [NET][FIX] 상대방이 나가서 대기실로 돌아갈 때 선생님 기믹도 확실히 멈춥니다.
            EnemyManager em = FindAnyObjectByType<EnemyManager>();
            if (em != null) em.StopGimmick();

            BoardManager bm = FindAnyObjectByType<BoardManager>();
            if (bm != null) bm.ClearBoard();

            CoinManager cm = FindAnyObjectByType<CoinManager>();
            if (cm != null) cm.ForceClosePanel();

            UpdateMatchingState();
        }

        private void UpdateMatchingState()
        {
            if (PhotonNetwork.CurrentRoom == null) return;

            UpdatePlayerListUI();

            if (PhotonNetwork.CurrentRoom.PlayerCount < maxPlayers)
            {
                if (matchingPanel != null) matchingPanel.SetActive(true);
                if (matchingText != null) matchingText.text = "상대를 찾는 중...";
                if (coinTossPanel != null) coinTossPanel.SetActive(false);

                if (PhotonNetwork.IsMasterClient)
                {
                    PhotonNetwork.CurrentRoom.IsOpen = true;
                    PhotonNetwork.CurrentRoom.IsVisible = true;
                }
                return;
            }

            // [NET][FIX] Photon Room Property '뭁비매치' 체크 — 씬 재로드에도 유지되는 안전한 검증
            // 이 방에서 게임이 한 번이라도 시작된 적 있으면, 양쪽 플레이어의 'WantsRematch'
            // Custom Property가 모두 true일 때만 코인토스를 진행합니다.
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("NeedsRematch", out object needsVal)
                && needsVal is true)
            {
                bool allWantRematch = true;
                foreach (var player in PhotonNetwork.PlayerList)
                {
                    if (!player.CustomProperties.TryGetValue("WantsRematch", out object wr) || wr is not true)
                    {
                        allWantRematch = false;
                        break;
                    }
                }
                if (!allWantRematch)
                {
                    Debug.Log("[GameMatching] 리매치 대기 중 — 양쪽 동의가 완료되지 않아 코인토스를 시작하지 않습니다.");
                    if (matchingText != null) matchingText.text = "상대를 기다리는중...";
                    return;
                }
            }

            if (isCoinTossStarted || isGameStarted) return;
            isCoinTossStarted = true;

            if (matchingPanel != null) matchingPanel.SetActive(true);
            if (matchingText != null) matchingText.text = "매칭 완료!";

            // [FIX] 매칭 완료 직후에도 코인 토스 패널은 확실히 꺼져 있어야 함 (카운트다운 동안)
            if (coinTossPanel != null) coinTossPanel.SetActive(false);

            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.IsVisible = false;

                RaiseEventOptions options = new() { Receivers = ReceiverGroup.All };
                PhotonNetwork.RaiseEvent(EVENT_START_COUNTDOWN, null, options, SendOptions.SendReliable);
            }
        }

        // ★ 매칭 패널의 '나가기' 버튼에 연결할 함수
        public void OnClickExitMatchmaking()
        {
            Debug.Log("[GameMatching] 매칭 취소 및 타이틀 이동 요청");

            // 모든 코루틴 중지 (카운트다운 등)
            StopAllCoroutines();

            // UIManager의 안전한 퇴장 로직 호출
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null)
            {
                ui.GoToTitle();
            }
            else
            {
                // UIManager가 없는 비상 상황 시 직접 처리
                if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
                SceneManager.LoadScene("Title");
            }
        }

        private System.Collections.IEnumerator CountdownRoutine()
        {
            yield return waitOneSecond;
            if (matchingText != null) matchingText.text = "매칭 완료!";
            yield return waitOneSecond;

            for (int i = 3; i > 0; i--)
            {
                if (matchingText != null) matchingText.text = i.ToString();
                yield return waitOneSecond;
            }

            if (matchingText != null) matchingText.text = "시작!";
            yield return waitHalfSecond;

            if (PhotonNetwork.IsMasterClient)
            {
                int masterResult = Random.Range(0, 2);
                RaiseEventOptions options = new() { Receivers = ReceiverGroup.All };
                PhotonNetwork.RaiseEvent(EVENT_START_COINTOSS_UI, masterResult, options, SendOptions.SendReliable);
            }
        }

        private void StartCoinToss(int masterResult)
        {
            // [FIX] 코인 토스가 시작될 때 실행 중인 카운트다운 코루틴이 있다면 중지
            StopAllCoroutines();

            if (coinTossPanel != null) coinTossPanel.SetActive(true);
            if (matchingPanel != null) matchingPanel.SetActive(false);

            int myResult;
            if (PhotonNetwork.IsMasterClient)
                myResult = masterResult;
            else
                myResult = (masterResult == 0) ? 1 : 0;

            CoinToss coinToss = FindFirstObjectByType<CoinToss>();
            if (coinToss != null) coinToss.StartToss(myResult);
        }

        private void UpdatePlayerListUI()
        {
            if (playerListText == null) return;

            StringBuilder sb = new();
            sb.AppendLine("<b>[Player List]</b>");

            foreach (var player in PhotonNetwork.PlayerList)
            {
                string suffix = player.IsLocal ? " (Me)" : "";
                // [FIX] 같은 닉네임일 경우를 대비해 고유 번호(#ActorNumber)를 함께 표시하여 구분 가능하게 함
                sb.AppendLine($"- {player.NickName} (#{player.ActorNumber}){suffix}");
            }

            playerListText.text = sb.ToString();
        }

        public void OnCoinTossFinished()
        {
            if (coinTossPanel != null)
                coinTossPanel.SetActive(false);

            TryStartGame();
        }

        private void TryStartGame()
        {
            if (PhotonNetwork.CurrentRoom == null) return;

            if (PhotonNetwork.CurrentRoom.PlayerCount == maxPlayers)
            {
                if (PhotonNetwork.IsMasterClient && !isStartRequested)
                {
                    isStartRequested = true;
                    Invoke(nameof(StartGame), 1f);
                }
            }
        }

        private void StartGame()
        {
            // [NET][FIX] CachingOption을 제거합니다. 이전 게임의 시작 이벤트가 룸 캐시에 남아있으면
            // 새로 들어온 플레이어가 코인 토스를 건너뛰고 바로 게임 시작 상태가 되는 버그가 발생합니다.
            RaiseEventOptions options = new() { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(EVENT_START_GAME, null, options, SendOptions.SendReliable);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == EVENT_START_GAME)
                ProcessStartGame();
            else if (photonEvent.Code == EVENT_START_COINTOSS_UI)
                StartCoinToss((int)photonEvent.CustomData);
            else if (photonEvent.Code == EVENT_START_COUNTDOWN)
                StartCoroutine(CountdownRoutine());
        }

        private void ProcessStartGame()
        {
            if (isGameStarted) return;
            isGameStarted = true;

            // GameManager에도 게임 시작 알림
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null) gm.isGameStarted = true;

            // [NET][FIX] 이 방에서 게임이 시작되었음을 Room Property로 기록합니다.
            // 다음 게임부터는 양쪽 WantsRematch 동의가 있어야만 코인토스가 진행됩니다.
            if (PhotonNetwork.IsMasterClient)
            {
                Hashtable roomProps = new() { ["NeedsRematch"] = true };
                PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
            }

            // [NET][FIX] 이전 판의 WantsRematch가 남아있지 않도록 초기화
            Hashtable clearProps = new() { ["WantsRematch"] = null };
            PhotonNetwork.LocalPlayer.SetCustomProperties(clearProps);

            Debug.Log("[GameMatching] 게임 시작! (모든 클라이언트 동기화, NeedsRematch=true)");
            if (matchingPanel != null)
            {
                matchingPanel.SetActive(false);
            }
        }

        /// <summary>
        /// [NET][FIX] 플레이어가 WantsRematch 설정을 변경하면 매칭 상태를 다시 확인합니다.
        /// </summary>
        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (changedProps.ContainsKey("WantsRematch"))
            {
                Debug.Log($"[GameMatching] {targetPlayer.NickName}의 WantsRematch 변경 감지 → 매칭 상태 재확인");
                UpdateMatchingState();
            }
        }
    }
}
