using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [SerializeField] private BattleshipAI ai;

    public static TurnManager Instance { get; private set; }

    public int turnCount = 1;
    public bool gameStarted = false;

    // Odd = Player, Even = AI
    public bool IsPlayerTurn => turnCount % 2 == 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void StartGame()
    {
        turnCount = 1;
        gameStarted = true;
        Debug.Log("Game started! Player turn.");
    }

    public void NextTurn()
    {
        if (!gameStarted)
            return;

        turnCount++;

        Debug.Log(IsPlayerTurn ? "Player Turn" : "AI Turn");
    }

    public void RunAITurn()
    {
        if (!gameStarted)
            return;

        if (IsPlayerTurn)
            return;

        if (ai == null)
        {
            ai = FindFirstObjectByType<BattleshipAI>();
        }

        if (ai == null)
        {
            Debug.LogError("TurnManager: No BattleshipAI found.");
            return;
        }

        ai.TakeTurn();
    }
}

