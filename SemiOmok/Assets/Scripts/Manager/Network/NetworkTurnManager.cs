using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Assets.Scripts.Manager.Network
{
    public class NetworkTurnManager : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public static NetworkTurnManager Instance { get; private set; }

        private const byte EVENT_REQUEST_PLACE_STONE = 10;
        private const byte EVENT_CONFIRM_PLACE_STONE = 11;
        private const byte EVENT_SYNC_BOARD_STATE = 15;

        private GameManager gameManager;
        private BoardManager boardManager;

        private void Awake()
        {
            Instance = this;
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void Start()
        {
            gameManager = FindAnyObjectByType<GameManager>();
            boardManager = FindAnyObjectByType<BoardManager>();

            if (gameManager == null) Debug.LogError("[NetworkTurnManager] GameManager를 찾을 수 없습니다.");
            if (boardManager == null) Debug.LogError("[NetworkTurnManager] BoardManager를 찾을 수 없습니다.");

            SyncColorFromRoomPlayers();
        }

        public void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            if (Instance == this) Instance = null;
        }

        public void RequestStonePlacement(int x, int y)
        {
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (!PhotonNetwork.InRoom || gameManager == null) return;

            int colorCode = gameManager.localPlayer == GameManager.Player.Black ? 1 : 2;
            int[] content = { x, y, colorCode };

            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
            PhotonNetwork.RaiseEvent(EVENT_REQUEST_PLACE_STONE, content, options, SendOptions.SendReliable);

            Debug.Log($"[NetworkTurnManager] 착수 요청 송신 (To Master): ({x}, {y})");
        }

        private void BroadcastConfirmStone(int x, int y, int colorCode)
        {
            int[] content = { x, y, colorCode };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(EVENT_CONFIRM_PLACE_STONE, content, options, SendOptions.SendReliable);
        }

        /// <summary>
        /// [NET] 마스터 클라이언트가 시간 초과 등으로 인해 강제로 돌을 두어야 할 때 사용합니다.
        /// </summary>
        public void BroadcastConfirmStoneFromMaster(int x, int y, int colorCode)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                BroadcastConfirmStone(x, y, colorCode);
            }
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                SyncBoardStateToPlayer(newPlayer);
                
                // [NET][FIX] 추가적인 게임 상태(체력 등) 동기화
                if (gameManager != null) gameManager.SyncGameStateToPlayer(newPlayer);
            }
        }

        private void SyncBoardStateToPlayer(Player targetPlayer)
        {
            if (gameManager == null) return;

            byte[] boardData = new byte[gameManager.boardSize * gameManager.boardSize];
            for (int i = 0; i < gameManager.boardSize; i++)
            {
                for (int j = 0; j < gameManager.boardSize; j++)
                {
                    GameManager.Player state = gameManager.GetCellState(i, j);
                    boardData[i * gameManager.boardSize + j] = (byte)state;
                }
            }

            object[] syncContent = new object[] 
            { 
                boardData, 
                (byte)gameManager.currentPlayer
            };

            RaiseEventOptions options = new RaiseEventOptions { TargetActors = new int[] { targetPlayer.ActorNumber } };
            PhotonNetwork.RaiseEvent(EVENT_SYNC_BOARD_STATE, syncContent, options, SendOptions.SendReliable);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == EVENT_REQUEST_PLACE_STONE) HandleRequestEvent(photonEvent);
            else if (photonEvent.Code == EVENT_CONFIRM_PLACE_STONE) HandleConfirmEvent(photonEvent);
            else if (photonEvent.Code == EVENT_SYNC_BOARD_STATE) HandleSyncBoardEvent(photonEvent);
        }

        private void HandleRequestEvent(EventData photonEvent)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int[] data = photonEvent.CustomData as int[];
            if (data == null || data.Length < 3) return;

            int x = data[0];
            int y = data[1];
            int colorCode = data[2];
            GameManager.Player requestColor = colorCode == 1 ? GameManager.Player.Black : GameManager.Player.White;

            if (gameManager != null)
            {
                if (gameManager.currentPlayer == requestColor && 
                    gameManager.GetCellState(x, y) == GameManager.Player.None &&
                    !gameManager.isGameOver)
                {
                    BroadcastConfirmStone(x, y, colorCode);
                }
            }
        }

        private void HandleSyncBoardEvent(EventData photonEvent)
        {
            object[] data = photonEvent.CustomData as object[];
            if (data == null || data.Length < 2) return;

            byte[] boardData = (byte[])data[0];
            GameManager.Player nextTurn = (GameManager.Player)(byte)data[1];

            if (gameManager != null && boardManager != null)
            {
                boardManager.ClearBoard();
                for (int i = 0; i < gameManager.boardSize; i++)
                {
                    for (int j = 0; j < gameManager.boardSize; j++)
                    {
                        GameManager.Player stoneColor = (GameManager.Player)boardData[i * gameManager.boardSize + j];
                        if (stoneColor != GameManager.Player.None)
                        {
                            boardManager.PlaceStoneFromNetwork(i, j, stoneColor);
                        }
                    }
                }
                gameManager.currentPlayer = nextTurn;
            }
        }

        private void HandleConfirmEvent(EventData photonEvent)
        {
            if (boardManager == null) boardManager = FindAnyObjectByType<BoardManager>();
            int[] data = photonEvent.CustomData as int[];
            if (data == null || data.Length < 3) return;

            int x = data[0];
            int y = data[1];
            int colorCode = data[2];
            GameManager.Player confirmedColor = colorCode == 1 ? GameManager.Player.Black : GameManager.Player.White;

            boardManager.PlaceStoneFromNetwork(x, y, confirmedColor);
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            if (!changedProps.ContainsKey("StoneColor")) return;
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

            int colorCode = (int)changedProps["StoneColor"];
            GameManager.Player selectedColor = colorCode == 1 ? GameManager.Player.Black : GameManager.Player.White;

            if (targetPlayer.IsLocal)
            {
                gameManager.localPlayer = selectedColor;
            }
            else
            {
                gameManager.localPlayer = (selectedColor == GameManager.Player.Black) ? GameManager.Player.White : GameManager.Player.Black;
                CoinManager coinManager = FindAnyObjectByType<CoinManager>();
                if (coinManager != null) coinManager.ShowRemoteSelectionResult(selectedColor);
            }
        }

        private void SyncColorFromRoomPlayers()
        {
            if (!PhotonNetwork.InRoom || gameManager == null) return;

            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (!player.CustomProperties.ContainsKey("StoneColor")) continue;

                int colorCode = (int)player.CustomProperties["StoneColor"];
                GameManager.Player selectedColor = colorCode == 1 ? GameManager.Player.Black : GameManager.Player.White;

                if (player.IsLocal) gameManager.localPlayer = selectedColor;
                else gameManager.localPlayer = (selectedColor == GameManager.Player.Black) ? GameManager.Player.White : GameManager.Player.Black;
            }
        }
    }
}
