using System.Collections.Generic;
using UnityEngine;

public class BattleshipAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerBoardSetup playerBoardSetup;
    [SerializeField] private GridManager playerGrid;

    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;

    [Header("Debug")]
    [SerializeField] private bool logAttacks = true;

    private readonly System.Random random = new System.Random();

    // Squares adjacent to previous hits that the AI should investigate.
    private readonly List<Vector2Int> targetQueue =
        new List<Vector2Int>();

    // Up, down, left, right.
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0)
    };

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        FindReferences();

        if (playerGrid == null)
        {
            Debug.LogError(
                "BattleshipAI: Player grid is missing. " +
                "Assign PlayerBoardSetup or Player Grid in the Inspector."
            );
        }
    }

    private void FindReferences()
    {
        if (playerBoardSetup == null)
        {
            playerBoardSetup =
                FindFirstObjectByType<PlayerBoardSetup>();
        }

        if (playerGrid == null && playerBoardSetup != null)
        {
            playerGrid = playerBoardSetup.PlayerGrid;
        }
    }


    /// Executes one AI attack.
    /// Call this once when TurnManager indicates it is the AI's turn.
    /// This method does not advance the turn counter.
    public bool TakeTurn()
    {
        if (TurnManager.Instance == null)
        {
            Debug.LogError(
                "BattleshipAI: TurnManager.Instance was not found."
            );

            return false;
        }

        if (!TurnManager.Instance.gameStarted)
        {
            Debug.LogWarning(
                "BattleshipAI: The game has not started."
            );

            return false;
        }

        if (TurnManager.Instance.IsPlayerTurn)
        {
            Debug.LogWarning(
                "BattleshipAI: It is currently the player's turn."
            );

            return false;
        }

        FindReferences();

        if (playerGrid == null)
        {
            Debug.LogError(
                "BattleshipAI: The player's grid is not assigned."
            );

            return false;
        }

        Vector2Int target = ChooseTarget();

        if (target.x < 0 || target.y < 0)
        {
            Debug.LogWarning(
                "BattleshipAI: No unattacked squares remain."
            );

            return false;
        }

        if (logAttacks)
        {
            Debug.Log(
                $"AI attacks square ({target.x}, {target.y})."
            );
        }

        AttackResult result =
            playerGrid.AttackCell(target.x, target.y);

        HandleAttackResult(target, result);

        return result != AttackResult.Invalid &&
            result != AttackResult.AlreadyAttacked;
    }

    /// Chooses an adjacent target if one is available.
    /// Otherwise, chooses a random unattacked square.
    private Vector2Int ChooseTarget()
    {
        while (targetQueue.Count > 0)
        {
            int index = random.Next(targetQueue.Count);

            Vector2Int candidate = targetQueue[index];

            targetQueue.RemoveAt(index);

            GridCell cell =
                playerGrid.GetCell(candidate.x, candidate.y);

            if (cell != null && !cell.hasBeenAttacked)
            {
                return candidate;
            }
        }

        return ChooseRandomSquare();
    }

    /// Selects a random square that has not already been attacked.
    private Vector2Int ChooseRandomSquare()
    {
        List<Vector2Int> availableSquares =
            new List<Vector2Int>();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GridCell cell = playerGrid.GetCell(x, y);

                if (cell != null && !cell.hasBeenAttacked)
                {
                    availableSquares.Add(
                        new Vector2Int(x, y)
                    );
                }
            }
        }

        if (availableSquares.Count == 0)
        {
            return new Vector2Int(-1, -1);
        }

        int randomIndex = random.Next(availableSquares.Count);

        return availableSquares[randomIndex];
    }

    /// Updates the AI's targeting strategy based on the attack result.
    private void HandleAttackResult(
        Vector2Int target,
        AttackResult result)
    {
        switch (result)
        {
            case AttackResult.Miss:
                if (logAttacks)
                {
                    Debug.Log(
                        $"AI missed at ({target.x}, {target.y})."
                    );
                }
                break;

            case AttackResult.Hit:
                if (logAttacks)
                {
                    Debug.Log(
                        $"AI hit a ship at ({target.x}, {target.y})!"
                    );
                }

                AddAdjacentTargets(target);
                break;

            case AttackResult.Sunk:
                if (logAttacks)
                {
                    Debug.Log(
                        $"AI sank a ship at ({target.x}, {target.y})!"
                    );
                }

                targetQueue.Clear();
                break;

            case AttackResult.AlreadyAttacked:
                Debug.LogWarning(
                    $"AI selected an already-attacked square: {target}."
                );
                break;

            case AttackResult.Invalid:
                Debug.LogWarning(
                    $"AI attack was invalid at {target}. " +
                    "Check that the grid is initialized correctly."
                );
                break;

            default:
                Debug.LogWarning(
                    $"AI received an unexpected result: {result}."
                );
                break;
        }
    }

    /// Adds valid, unattacked neighboring squares to the target queue.
    private void AddAdjacentTargets(Vector2Int hitPosition)
    {
        foreach (Vector2Int direction in Directions)
        {
            Vector2Int neighbor = hitPosition + direction;

            // Keep coordinates inside the board.
            if (neighbor.x < 0 || neighbor.x >= gridWidth ||
                neighbor.y < 0 || neighbor.y >= gridHeight)
            {
                continue;
            }

            GridCell cell =
                playerGrid.GetCell(neighbor.x, neighbor.y);

            if (cell == null || cell.hasBeenAttacked)
            {
                continue;
            }

            if (!targetQueue.Contains(neighbor))
            {
                targetQueue.Add(neighbor);
            }
        }
    }

    /// Clears the AI's targeting memory when a new game starts.
    public void ResetAI()
    {
        targetQueue.Clear();
    }

    /// Optional helper if the board dimensions change.
    public void SetGridDimensions(int width, int height)
    {
        gridWidth = Mathf.Max(1, width);
        gridHeight = Mathf.Max(1, height);

        targetQueue.Clear();
    }
}