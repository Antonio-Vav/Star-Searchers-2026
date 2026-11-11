
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

    [Header("AI Settings")]
    [Tooltip("How much extra weight placements receive for covering known hits.")]
    [SerializeField] private int hitWeight = 20;

    [SerializeField] private bool logAttacks = true;
    [SerializeField] private bool logHeatmap = false;

    // HUD information.
    public Vector2Int LastAttackPosition { get; private set; }
        = new Vector2Int(-1, -1);

    public AttackResult LastAttackResult { get; private set; }
        = AttackResult.Invalid;

    // 0 = unknown, 1 = miss, 2 = hit, 3 = sunk.
    private const int UNKNOWN = 0;
    private const int MISS = 1;
    private const int HIT = 2;
    private const int SUNK = 3;

    // AI's knowledge of the board.
    private int[,] shotHistory;

    // Original fleet configuration and remaining ship lengths.
    private readonly List<int> initialShipLengths =
        new List<int> { 5, 4, 3, 3, 2 };

    private readonly List<int> remainingShipLengths =
        new List<int>();

    // Most recently calculated probability heatmap.
    public int[,] LastHeatmap { get; private set; }

    private readonly System.Random random =
        new System.Random();

    // Horizontal and vertical directions.
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private void Awake()
    {
        FindReferences();
        InitializeKnowledge();
    }

    private void Start()
    {
        FindReferences();

        if (playerGrid == null)
        {
            Debug.LogError(
                "BattleshipAI: Player grid is missing."
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

        if (playerGrid != null)
        {
            gridWidth = playerGrid.GridWidth;
            gridHeight = playerGrid.GridHeight;
        }
    }

    private void InitializeKnowledge()
    {
        shotHistory = new int[gridWidth, gridHeight];

        LastHeatmap = new int[gridWidth, gridHeight];
    }

    // Receives the actual fleet configuration from GameManager.
    // Only ship lengths are copied, not their positions.
    public void ConfigureFleet(List<ShipData> fleet)
    {
        if (fleet == null || fleet.Count == 0)
        {
            Debug.LogWarning(
                "BattleshipAI: Invalid fleet configuration."
            );
            return;
        }

        initialShipLengths.Clear();

        foreach (ShipData ship in fleet)
        {
            if (ship != null && ship.length > 0)
            {
                initialShipLengths.Add(ship.length);
            }
        }

        ResetAI();
    }

    // Called once per AI turn.
    // Does not advance the turn counter.
    public bool TakeTurn()
    {
        LastAttackPosition = new Vector2Int(-1, -1);
        LastAttackResult = AttackResult.Invalid;

        if (TurnManager.Instance == null)
        {
            Debug.LogError("BattleshipAI: TurnManager missing.");
            return false;
        }

        if (!TurnManager.Instance.gameStarted ||
            TurnManager.Instance.IsPlayerTurn)
        {
            return false;
        }

        FindReferences();

        if (playerGrid == null)
        {
            Debug.LogError("BattleshipAI: Player grid missing.");
            return false;
        }

        // Calculate the probability of every possible target.
        LastHeatmap = CalculateHeatmap();

        // Find the best unattacked square.
        Vector2Int target = ChooseBestTarget(LastHeatmap);

        if (target.x < 0 || target.y < 0)
        {
            Debug.LogWarning(
                "BattleshipAI: No available attack squares."
            );
            return false;
        }

        if (logAttacks)
        {
            Debug.Log(
                $"Probability AI attacks ({target.x}, {target.y}) " +
                $"with heatmap score {LastHeatmap[target.x, target.y]}."
            );
        }

        // Perform the actual attack.
        AttackResult result =
            playerGrid.AttackCell(target.x, target.y);

        LastAttackPosition = target;
        LastAttackResult = result;

        UpdateKnowledge(target, result);

        return result != AttackResult.Invalid &&
               result != AttackResult.AlreadyAttacked;
    }

    // PROBABILITY HEATMAP

    private int[,] CalculateHeatmap()
    {
        int[,] heatmap =
            new int[gridWidth, gridHeight];

        // Consider every ship that has not been sunk.
        foreach (int shipLength in remainingShipLengths)
        {
            // Check horizontal and vertical placements.
            foreach (ShipOrientation orientation in
                     new[] {
                         ShipOrientation.Horizontal,
                         ShipOrientation.Vertical
                     })
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        if (!IsValidPlacement(
                                x, y, shipLength, orientation))
                        {
                            continue;
                        }

                        // Count how many known hits this placement
                        // could explain.
                        int hitsCovered = CountHits(
                            x, y, shipLength, orientation
                        );

                        // Placements covering known hits are
                        // considerably more valuable.
                        int score = 1 +
                            (hitsCovered * hitWeight);

                        AddPlacementToHeatmap(
                            heatmap,
                            x,
                            y,
                            shipLength,
                            orientation,
                            score
                        );
                    }
                }
            }
        }

        if (logHeatmap)
        {
            PrintHeatmap(heatmap);
        }

        return heatmap;
    }

    // Checks whether a possible ship placement is consistent
    // with the AI's known information.
    
    private bool IsValidPlacement(
        int startX,
        int startY,
        int length,
        ShipOrientation orientation)
    {
        for (int i = 0; i < length; i++)
        {
            int x = startX;
            int y = startY;

            if (orientation == ShipOrientation.Horizontal)
                x += i;
            else
                y += i;

            if (x < 0 || x >= gridWidth ||
                y < 0 || y >= gridHeight)
            {
                return false;
            }

            if (shotHistory[x, y] == MISS ||
                shotHistory[x, y] == SUNK)
            {
                return false;
            }
        }

        // Reject placements that contradict an identified
        // ship direction.
        return IsConsistentWithDirectionalHits(
            startX, startY, length, orientation
        );
    }


    private bool IsConsistentWithDirectionalHits(
        int startX,
        int startY,
        int length,
        ShipOrientation orientation)
    {
        for (int i = 0; i < length; i++)
        {
            int x = startX;
            int y = startY;

            if (orientation == ShipOrientation.Horizontal)
                x += i;
            else
                y += i;

            if (shotHistory[x, y] != HIT)
                continue;

            // Check for neighboring hits.
            bool left = x > 0 &&
                        shotHistory[x - 1, y] == HIT;

            bool right = x < gridWidth - 1 &&
                        shotHistory[x + 1, y] == HIT;

            bool down = y > 0 &&
                        shotHistory[x, y - 1] == HIT;

            bool up = y < gridHeight - 1 &&
                    shotHistory[x, y + 1] == HIT;

            bool horizontalHit = left || right;
            bool verticalHit = up || down;

            // A discovered hit cannot belong to a ship
            // extending in two different directions.
            if (horizontalHit && verticalHit)
                return false;

            // Horizontal ship detected.
            if (horizontalHit)
            {
                if (orientation != ShipOrientation.Horizontal)
                    return false;

                int minX = x;
                int maxX = x;

                while (minX > 0 &&
                    shotHistory[minX - 1, y] == HIT)
                {
                    minX--;
                }

                while (maxX < gridWidth - 1 &&
                    shotHistory[maxX + 1, y] == HIT)
                {
                    maxX++;
                }

                int placementEnd = startX + length - 1;

                // Placement must cover every consecutive hit.
                if (startX > minX || placementEnd < maxX)
                    return false;
            }

            // Vertical ship detected.
            if (verticalHit)
            {
                if (orientation != ShipOrientation.Vertical)
                    return false;

                int minY = y;
                int maxY = y;

                while (minY > 0 &&
                    shotHistory[x, minY - 1] == HIT)
                {
                    minY--;
                }

                while (maxY < gridHeight - 1 &&
                    shotHistory[x, maxY + 1] == HIT)
                {
                    maxY++;
                }

                int placementEnd = startY + length - 1;

                if (startY > minY || placementEnd < maxY)
                    return false;
            }
        }

        return true;
    }


    // Counts known, unresolved hits covered by a placement.
    private int CountHits(
        int startX,
        int startY,
        int length,
        ShipOrientation orientation)
    {
        int hits = 0;

        for (int i = 0; i < length; i++)
        {
            int x = startX;
            int y = startY;

            if (orientation == ShipOrientation.Horizontal)
                x += i;
            else
                y += i;

            if (shotHistory[x, y] == HIT)
            {
                hits++;
            }
        }

        return hits;
    }

    // Every possible placement contributes its score to
    // each unattacked square that it occupies.
    private void AddPlacementToHeatmap(
        int[,] heatmap,
        int startX,
        int startY,
        int length,
        ShipOrientation orientation,
        int score)
    {
        for (int i = 0; i < length; i++)
        {
            int x = startX;
            int y = startY;

            if (orientation == ShipOrientation.Horizontal)
                x += i;
            else
                y += i;

            // Already attacked squares cannot be selected again.
            if (shotHistory[x, y] != UNKNOWN)
                continue;

            heatmap[x, y] += score;
        }
    }

    // TARGET SELECTION

    private Vector2Int ChooseBestTarget(int[,] heatmap)
    {
        int highestScore = -1;

        List<Vector2Int> bestTargets =
            new List<Vector2Int>();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GridCell cell = playerGrid.GetCell(x, y);

                if (cell == null || cell.hasBeenAttacked)
                    continue;

                int score = heatmap[x, y];

                if (score > highestScore)
                {
                    highestScore = score;
                    bestTargets.Clear();

                    bestTargets.Add(
                        new Vector2Int(x, y)
                    );
                }
                else if (score == highestScore)
                {
                    bestTargets.Add(
                        new Vector2Int(x, y)
                    );
                }
            }
        }

        if (bestTargets.Count == 0)
        {
            return new Vector2Int(-1, -1);
        }

        // Randomize ties so the AI does not always favor
        // the same direction when probabilities are equal.
        int index = random.Next(bestTargets.Count);

        return bestTargets[index];
    }

    // ATTACK KNOWLEDGE

    private void UpdateKnowledge(
        Vector2Int position,
        AttackResult result)
    {
        int x = position.x;
        int y = position.y;

        switch (result)
        {
            case AttackResult.Miss:

                shotHistory[x, y] = MISS;

                if (logAttacks)
                {
                    Debug.Log(
                        $"AI missed at ({x}, {y})."
                    );
                }

                break;

            case AttackResult.Hit:

                shotHistory[x, y] = HIT;

                if (logAttacks)
                {
                    Debug.Log(
                        $"AI hit a ship at ({x}, {y})!"
                    );
                }

                break;

            case AttackResult.Sunk:

                if (logAttacks)
                {
                    Debug.Log(
                        $"AI sunk a ship at ({x}, {y})!"
                    );
                }

                RegisterSunkShip(x, y);

                break;

            case AttackResult.AlreadyAttacked:

                Debug.LogWarning(
                    "AI selected an already-attacked square."
                );

                break;

            case AttackResult.Invalid:

                Debug.LogWarning(
                    "AI attack was invalid."
                );

                break;
        }
    }

    // When a ship is sunk, its entire occupied area is now
    // known to the AI and can be excluded from future calculations.
    private void RegisterSunkShip(int x, int y)
    {
        ShipData sunkShip = playerGrid.GetShipAt(x, y);

        if (sunkShip == null)
        {
            shotHistory[x, y] = SUNK;
            return;
        }

        int sunkLength = sunkShip.length;

        foreach (GridCell cell in sunkShip.occupiedCells)
        {
            if (cell == null)
                continue;

            shotHistory[cell.x, cell.y] = SUNK;
        }

        // Remove exactly one ship of this length.
        int index = remainingShipLengths.IndexOf(sunkLength);

        if (index >= 0)
        {
            remainingShipLengths.RemoveAt(index);
        }

        if (logAttacks)
        {
            Debug.Log(
                $"AI removed sunk ship '{sunkShip.shipID}' " +
                $"(length {sunkLength}) from its probability calculations."
            );
        }
    }

    // OPTIONAL HEATMAP DEBUGGING

    private void PrintHeatmap(int[,] heatmap)
    {
        System.Text.StringBuilder output =
            new System.Text.StringBuilder();

        output.AppendLine("AI PROBABILITY HEATMAP");

        // Display the top row first.
        for (int y = gridHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                output.Append(
                    heatmap[x, y].ToString("D4")
                );

                output.Append(" ");
            }

            output.AppendLine();
        }

        Debug.Log(output.ToString());
    }

    // RESET

    public void ResetAI()
    {
        if (playerGrid != null)
        {
            gridWidth = playerGrid.GridWidth;
            gridHeight = playerGrid.GridHeight;
        }

        InitializeKnowledge();

        remainingShipLengths.Clear();

        remainingShipLengths.AddRange(
            initialShipLengths
        );

        LastAttackPosition = new Vector2Int(-1, -1);
        LastAttackResult = AttackResult.Invalid;
    }

    public void SetGridDimensions(int width, int height)
    {
        gridWidth = Mathf.Max(1, width);
        gridHeight = Mathf.Max(1, height);

        InitializeKnowledge();
    }
}
