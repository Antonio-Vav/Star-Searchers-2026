using UnityEngine;

// One shared turn counter: odd turns belong to the player, even turns to AI.
// GameManager decides when to advance the turn; this component owns turn state.
public class TurnManager : MonoBehaviour
{
    [SerializeField] private BattleshipAI ai;

    public static TurnManager Instance { get; private set; }

    public int turnCount = 1;
    public bool gameStarted = false;

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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
        TryRunAITurn();
    }

    // GameManager uses this so it knows whether the AI actually attacked.
    public bool TryRunAITurn()
    {
        if (!gameStarted || IsPlayerTurn)
            return false;

        if (ai == null)
            ai = FindFirstObjectByType<BattleshipAI>();

        if (ai == null)
        {
            Debug.LogError("TurnManager: No BattleshipAI found.");
            return false;
        }

        return ai.TakeTurn();
    }

    public void EndGame()
    {
        gameStarted = false;
        Debug.Log("Game ended.");
    }

}