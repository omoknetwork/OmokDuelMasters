using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro; 

public class AIManager : MonoBehaviour
{
    [Header("Game Manager Reference")]
    public GameManager gameManager;

    [Header("AI Settings")]
    [Tooltip("AI가 앞을 내다보는 수(깊이)입니다. 2는 빠름, 3은 적당합니다. (3 권장)")]
    public int searchDepth = 3;

    [Header("UI References")]
    public TextMeshProUGUI aiStateText; 

    [Header("Debug View (Do Not Edit)")]
    public GameManager.Player aiPlayerColor = GameManager.Player.None;

    private bool isAiThinking = false;
    private Coroutine thinkingTextCoroutine; 

    private void Start()
    {
        // ★ 씬 이름이 "Class"가 아니면 꺼버리는 하드코딩 로직을 제거했습니다!
        
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        if (gameManager != null)
        {
            gameManager.OnTurnChanged += OnTurnChanged;
        }

        if (aiStateText != null)
            aiStateText.text = "";
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnTurnChanged -= OnTurnChanged;
        }
    }

    public void SetAIColor(GameManager.Player color)
    {
        aiPlayerColor = color;
        Debug.Log($"AI 설정 완료: AI는 {aiPlayerColor} 입니다.");

        if (gameManager.currentPlayer == aiPlayerColor && !isAiThinking && !gameManager.isGameOver)
        {
            StartCoroutine(CalculateAndMakeMove());
        }
    }

    private void OnTurnChanged(GameManager.Player currentPlayer)
    {
        if (aiPlayerColor == GameManager.Player.None) return;

        // enabled가 꺼져있지 않다면 내 턴에 맞춰 AI의 착수 계산을 시작합니다.
        if (currentPlayer == aiPlayerColor && !gameManager.isGameOver && !isAiThinking && enabled)
        {
            StartCoroutine(CalculateAndMakeMove());
        }
    }

    private IEnumerator ShowThinkingText()
    {
        if (aiStateText == null) yield break;

        string baseText = "AI 생각중";
        int dotCount = 1;

        while (isAiThinking)
        {
            string dots = new string('.', dotCount);
            aiStateText.text = baseText + dots;

            dotCount++;
            if (dotCount > 3) dotCount = 1;

            yield return new WaitForSeconds(0.3f);
        }

        aiStateText.text = "";
    }

    private IEnumerator CalculateAndMakeMove()
    {   
        isAiThinking = true;

        if (thinkingTextCoroutine != null) StopCoroutine(thinkingTextCoroutine);
        thinkingTextCoroutine = StartCoroutine(ShowThinkingText());

        if (IsBoardEmpty())
        {
            yield return new WaitForSeconds(1.0f); 
            isAiThinking = false; 
            gameManager.PlaceStone(gameManager.boardSize / 2, gameManager.boardSize / 2, aiPlayerColor);
            yield break;
        }

        int bestX = -1;
        int bestY = -1;
        int bestValue = int.MinValue;

        List<Vector2Int> candidates = GetCandidateMoves();

        if (candidates.Count == 0)
        {
            isAiThinking = false;
            yield break;
        }

        yield return new WaitForSeconds(0.1f);

        int alpha = int.MinValue;
        int beta = int.MaxValue;

        // 성능을 위해 현재 보드판을 복사해서 1회만 할당
        int[,] simulateBoard = GetBoardCopy();

        foreach (var move in candidates)
        {
            if (aiPlayerColor == GameManager.Player.Black && gameManager.IsForbidden(move.x, move.y, aiPlayerColor))
                continue;

            simulateBoard[move.x, move.y] = (int)aiPlayerColor;
            int boardValue = Minimax(simulateBoard, searchDepth - 1, alpha, beta, false);
            simulateBoard[move.x, move.y] = 0; // 원상복구

            if (boardValue > bestValue)
            {
                bestValue = boardValue;
                bestX = move.x;
                bestY = move.y;
            }

            alpha = Mathf.Max(alpha, bestValue);
        }

        if (bestX == -1 || bestY == -1)
        {
            bestX = candidates[0].x;
            bestY = candidates[0].y;
        }

        yield return new WaitForSeconds(0.2f); 
        
        isAiThinking = false; 
        gameManager.PlaceStone(bestX, bestY, aiPlayerColor);
    }

    private int Minimax(int[,] board, int depth, int alpha, int beta, bool isMaximizing)
    {
        if (depth == 0)
        {
            return EvaluateBoard(board);
        }

        List<Vector2Int> candidates = GetCandidateMovesFromBoard(board);
        if (candidates.Count == 0) return EvaluateBoard(board);

        if (isMaximizing)
        {
            int maxEval = int.MinValue;
            foreach (var move in candidates)
            {
                if (aiPlayerColor == GameManager.Player.Black && IsSimulateForbidden(board, move.x, move.y, (int)aiPlayerColor)) continue;

                board[move.x, move.y] = (int)aiPlayerColor;
                int eval = Minimax(board, depth - 1, alpha, beta, false);
                board[move.x, move.y] = 0; 

                maxEval = Mathf.Max(maxEval, eval);
                alpha = Mathf.Max(alpha, eval);
                if (beta <= alpha) break; 
            }
            if (maxEval == int.MinValue) return EvaluateBoard(board);
            return maxEval;
        }
        else
        {
            int minEval = int.MaxValue;
            int opponentColor = (int)((aiPlayerColor == GameManager.Player.Black) ? GameManager.Player.White : GameManager.Player.Black);

            foreach (var move in candidates)
            {
                if (opponentColor == (int)GameManager.Player.Black && IsSimulateForbidden(board, move.x, move.y, opponentColor)) continue;

                board[move.x, move.y] = opponentColor;
                int eval = Minimax(board, depth - 1, alpha, beta, true);
                board[move.x, move.y] = 0; 

                minEval = Mathf.Min(minEval, eval);
                beta = Mathf.Min(beta, eval);
                if (beta <= alpha) break; 
            }
            if (minEval == int.MaxValue) return EvaluateBoard(board);
            return minEval;
        }
    }

    private int EvaluateBoard(int[,] board)
    {
        int score = 0;
        int opponentColor = (int)((aiPlayerColor == GameManager.Player.Black) ? GameManager.Player.White : GameManager.Player.Black);

        // 내 돌의 평가점수
        score += EvaluateForPlayer(board, (int)aiPlayerColor);
        // 상대 돌의 평가점수 (방어를 더 우선시하기 위해 1.5배 가중치 부여)
        score -= (int)(EvaluateForPlayer(board, opponentColor) * 1.5f); 

        return score;
    }

    private int EvaluateForPlayer(int[,] board, int player)
    {
        int score = 0;
        int size = gameManager.boardSize;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (board[x, y] != player) continue;

                score += EvaluateLine(board, player, x, y, 1, 0); 
                score += EvaluateLine(board, player, x, y, 0, 1); 
                score += EvaluateLine(board, player, x, y, 1, 1); 
                score += EvaluateLine(board, player, x, y, 1, -1);
            }
        }
        return score;
    }

    private int EvaluateLine(int[,] board, int player, int x, int y, int dx, int dy)
    {
        int size = gameManager.boardSize;

        int prevX = x - dx;
        int prevY = y - dy;
        if (prevX >= 0 && prevX < size && prevY >= 0 && prevY < size && board[prevX, prevY] == player)
            return 0;

        int consecutiveStones = 0;
        int emptyEnds = 0;

        if (prevX >= 0 && prevX < size && prevY >= 0 && prevY < size && board[prevX, prevY] == 0)
            emptyEnds++;

        int currX = x;
        int currY = y;
        while (currX >= 0 && currX < size && currY >= 0 && currY < size && board[currX, currY] == player)
        {
            consecutiveStones++;
            currX += dx;
            currY += dy;
        }

        if (currX >= 0 && currX < size && currY >= 0 && currY < size && board[currX, currY] == 0)
            emptyEnds++;

        if (consecutiveStones >= 5) return 1000000; // 승리
        if (consecutiveStones == 4)
        {
            if (emptyEnds == 2) return 100000; // 열린 4
            if (emptyEnds == 1) return 10000;  // 닫힌 4
        }
        if (consecutiveStones == 3)
        {
            if (emptyEnds == 2) return 5000;   // 열린 3
            if (emptyEnds == 1) return 500;    // 닫힌 3
        }
        if (consecutiveStones == 2)
        {
            if (emptyEnds == 2) return 100;    
            if (emptyEnds == 1) return 10;     
        }

        return 0;
    }

    private int[,] GetBoardCopy()
    {
        int size = gameManager.boardSize;
        int[,] copy = new int[size, size];
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                copy[x, y] = (int)gameManager.GetCellState(x, y);
            }
        }
        return copy;
    }

    private bool IsBoardEmpty()
    {
        int size = gameManager.boardSize;
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (gameManager.GetCellState(x, y) != GameManager.Player.None) return false;
            }
        }
        return true;
    }

    private List<Vector2Int> GetCandidateMoves()
    {
        return GetCandidateMovesFromBoard(GetBoardCopy());
    }

    private List<Vector2Int> GetCandidateMovesFromBoard(int[,] board)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        int size = gameManager.boardSize;
        bool[,] hasNeighbor = new bool[size, size];

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (board[x, y] != 0) 
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < size && ny >= 0 && ny < size && board[nx, ny] == 0)
                            {
                                if (!hasNeighbor[nx, ny])
                                {
                                    hasNeighbor[nx, ny] = true;
                                    candidates.Add(new Vector2Int(nx, ny));
                                }
                            }
                        }
                    }
                }
            }
        }
        return candidates;
    }

    private bool IsSimulateForbidden(int[,] board, int x, int y, int playerInt)
    {
        return false; 
    }
}
