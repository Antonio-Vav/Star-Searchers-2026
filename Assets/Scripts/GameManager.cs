using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Existing gameplay references")]
    [SerializeField] private PlayerBoardSetup playerBoardSetup;
    [SerializeField] private GridManager playerGrid;
    [SerializeField] private GridManager aiGrid;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private BattleshipAI battleshipAI;

    [Header("Fire-coordinate UI (A1 to J10)")]
    [SerializeField] private TMP_InputField targetInput;
    [SerializeField] private Button fireButton;
    [SerializeField] private GameObject firePanel; // optional container; can be left unassigned
    [SerializeField] private TMP_Text turnStatusText;
    [SerializeField] private TMP_Text playerShotText;
    [SerializeField] private TMP_Text aiShotText;

    [Header("Grid lettering")]
    [Tooltip("Check the GridCell at the visually TOP LEFT of your board. " +
             "If it has y=0, leave this checked. If it has y=9, uncheck.")]
    [SerializeField] private bool rowAIsYZero = true;

    [Header("Gameplay settings")]
    [Min(0f)][SerializeField] private float aiTurnDelay = 0.7f;
    [SerializeField] private bool revealAIPositionsInConsole = false;

    private bool matchStarted;
    private bool gameOver;
    private bool aiTurnRunning;

    private struct PlannedShip
    {
        public string id;
        public int length;
        public int x;
        public int y;
        public ShipOrientation orientation;

        public PlannedShip(string id, int length, int x, int y, ShipOrientation orientation)
        {
            this.id = id;
            this.length = length;
            this.x = x;
            this.y = y;
            this.orientation = orientation;
        }
    }

    private readonly System.Random placementRandom = new System.Random();
    private readonly List<PlannedShip> savedAIPlacements = new List<PlannedShip>();

    private void Start()
    {
        if (playerBoardSetup != null && playerGrid == null)
            playerGrid = playerBoardSetup.PlayerGrid;

        if (turnManager == null)
            turnManager = TurnManager.Instance;

        // New play session always starts in placement mode.
        if (turnManager != null)
        {
            turnManager.gameStarted = false;
            turnManager.turnCount = 1;
        }

        if (fireButton != null)
            fireButton.interactable = false;

        if (firePanel != null)
            firePanel.SetActive(false);

        SetText(turnStatusText, "Place your ships, then press Confirm Placement.");
        SetText(playerShotText, "Your last shot: —");
        SetText(aiShotText, "AI last shot: —");

        if (!ReferencesValid())
            Debug.LogError("GameManager: Configure the missing references in the Inspector.", this);
    }

    public void ConfirmPlacementAndStartGame()
    {
        if (matchStarted || gameOver)
            return;

        if (!ReferencesValid() || !BoardsValid())
        {
            SetText(turnStatusText, "Configuration error — check Console and Inspector.");
            return;
        }

        // This checks that every DraggableShip is placed, locks the fleet,
        // and hides your existing Confirm button.
        playerBoardSetup.ConfirmBoard();
        if (!playerBoardSetup.SetupComplete)
        {
            SetText(turnStatusText, "Place every ship before confirming.");
            return;
        }

        List<ShipData> playerFleet = playerGrid.GetAllShips();
        if (playerFleet.Count == 0)
        {
            Debug.LogError("GameManager: Player fleet has no ships.", this);
            SetText(turnStatusText, "No player ships were registered.");
            return;
        }

        if (!DeployAIFleet(playerFleet))
        {
            SetText(turnStatusText, "AI ship placement failed — check Console.");
            return;
        }

        battleshipAI.ResetAI();
        turnManager.StartGame();
        matchStarted = true;
        gameOver = false;
        aiTurnRunning = false;

        if (firePanel != null)
            firePanel.SetActive(true);
        if (fireButton != null)
            fireButton.interactable = true;

        SetText(turnStatusText, "Your turn! Enter A1–J10 and press Fire.");
        SetText(playerShotText, "Your last shot: —");
        SetText(aiShotText, "AI last shot: —");
        Debug.Log($"Match ready: {playerGrid.ShipCount} player ships, " +
                  $"{aiGrid.ShipCount} hidden AI ships.");
    }

    public void FireAtCoordinates()
    {
        if (!matchStarted || gameOver || aiTurnRunning ||
            turnManager == null || !turnManager.gameStarted || !turnManager.IsPlayerTurn)
            return;

        if (targetInput == null || !TryReadCoordinate(targetInput.text, out int x, out int y))
        {
            SetText(playerShotText, "Invalid coordinate. Use A1, B7, J10, etc.");
            return;
        }

        AttackResult result = aiGrid.AttackCell(x, y);
        string location = ToBoardLabel(x, y);

        if (result == AttackResult.Invalid)
        {
            SetText(playerShotText, location + ": invalid square.");
            return;
        }
        if (result == AttackResult.AlreadyAttacked)
        {
            SetText(playerShotText, location + ": already fired here! Try another.");
            return; // Do NOT consume a turn on an already-attacked square.
        }

        SetText(playerShotText, FormatShot("You", location, result, aiGrid, x, y));
        Debug.Log($"Player fires {location} ({x},{y}): {result}");
        targetInput.SetTextWithoutNotify(string.Empty);

        if (aiGrid.AreAllShipsSunk())
        {
            FinishGame("YOU WIN! All enemy ships are sunk.");
            return;
        }

        // A valid hit or miss spends the player's turn.
        turnManager.NextTurn();
        aiTurnRunning = true;
        if (fireButton != null)
            fireButton.interactable = false;

        SetText(turnStatusText, "AI turn...");
        StartCoroutine(ExecuteAITurn());
    }

    private IEnumerator ExecuteAITurn()
    {
        if (aiTurnDelay > 0f)
            yield return new WaitForSeconds(aiTurnDelay);

        if (gameOver || !matchStarted)
            yield break;

        // TurnManager invokes the existing BattleshipAI.TakeTurn().
        bool attacked = turnManager.TryRunAITurn();
        AttackResult result = battleshipAI.LastAttackResult;
        Vector2Int target = battleshipAI.LastAttackPosition;

        if (!attacked || target.x < 0 || target.y < 0 ||
            result == AttackResult.Invalid || result == AttackResult.AlreadyAttacked)
        {
            Debug.LogError("GameManager: AI could not perform a valid attack. " +
                           "Make sure TurnManager and GameManager reference the SAME " +
                           "BattleshipAI, and that BattleshipAI points to PLAYER Grid.", this);
            FinishGame("AI turn failed — inspect the Console.");
            yield break;
        }

        string location = ToBoardLabel(target.x, target.y);
        SetText(aiShotText, FormatShot("AI", location, result, playerGrid, target.x, target.y));
        Debug.Log($"AI fires {location} ({target.x},{target.y}): {result}");

        if (playerGrid.AreAllShipsSunk())
        {
            FinishGame("AI WINS! All your ships are sunk.");
            yield break;
        }

        turnManager.NextTurn();
        aiTurnRunning = false;
        if (fireButton != null)
            fireButton.interactable = true;
        SetText(turnStatusText, "Your turn! Enter A1–J10 and press Fire.");
    }

    private void FinishGame(string message)
    {
        gameOver = true;
        aiTurnRunning = false;
        turnManager.EndGame();
        if (fireButton != null)
            fireButton.interactable = false;
        SetText(turnStatusText, message);
        Debug.Log(message);
    }

    private bool ReferencesValid()
    {
        if (playerBoardSetup == null || playerGrid == null || aiGrid == null ||
            turnManager == null || battleshipAI == null || targetInput == null ||
            fireButton == null)
        {
            Debug.LogError("GameManager: Assign PlayerBoardSetup, Player Grid, " +
                "AI Grid, TurnManager, BattleshipAI, Target Input, and Fire Button.", this);
            return false;
        }

        if (playerGrid == aiGrid)
        {
            Debug.LogError("GameManager: Player Grid and AI Grid MUST be two " +
                "different GridManager components.", this);
            return false;
        }

        return true;
    }

    private bool BoardsValid()
    {
        if (playerGrid.GridWidth != 10 || playerGrid.GridHeight != 10 ||
            aiGrid.GridWidth != 10 || aiGrid.GridHeight != 10)
        {
            Debug.LogError("This input screen expects two 10x10 boards.", this);
            return false;
        }

        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                if (playerGrid.GetCell(x, y) == null || aiGrid.GetCell(x, y) == null)
                {
                    Debug.LogError($"GameManager: Missing cell ({x},{y}) on " +
                                   "the Player or AI grid. Check Cell Grid Parent " +
                                   "on BOTH GridManager components.", this);
                    return false;
                }
            }
        }
        return true;
    }

    private bool DeployAIFleet(List<ShipData> playerFleet)
    {
        // Reuse the exact ship IDs and lengths you already chose for the player.
        // Each board has its own independent ShipData dictionary and cells.
        playerFleet.Sort((a, b) => b.length.CompareTo(a.length));

        bool[,] occupancy = new bool[aiGrid.GridWidth, aiGrid.GridHeight];
        List<PlannedShip> plan = new List<PlannedShip>();
        if (!TryPlanFleet(playerFleet, 0, occupancy, plan))
        {
            Debug.LogError("GameManager: Could not fit the fleet on the AI board.", this);
            return false;
        }

        aiGrid.ResetBoard();
        savedAIPlacements.Clear();

        foreach (PlannedShip ship in plan)
        {
            if (!aiGrid.AddShip(ship.id, ship.x, ship.y, ship.length, ship.orientation))
            {
                aiGrid.ResetBoard();
                savedAIPlacements.Clear();
                Debug.LogError($"GameManager: Failed to place AI ship {ship.id}.", this);
                return false;
            }

            // Saved for this match in addition to the GridManager's ShipData.
            savedAIPlacements.Add(ship);
            if (revealAIPositionsInConsole)
                Debug.Log($"[AI FLEET DEBUG] {ship.id}, {ship.length} cells, " +
                          $"start=({ship.x},{ship.y}), {ship.orientation}");
        }

        return aiGrid.ShipCount == playerFleet.Count;
    }

    // A randomized backtracking planner which avoids overlaps
    private bool TryPlanFleet(List<ShipData> fleet, int shipIndex,
                              bool[,] occupied, List<PlannedShip> plan)
    {
        if (shipIndex == fleet.Count)
            return true;

        ShipData ship = fleet[shipIndex];
        if (ship == null || ship.length < 1 || string.IsNullOrWhiteSpace(ship.shipID))
            return false;

        List<PlannedShip> choices = new List<PlannedShip>();
        foreach (ShipOrientation direction in new[] {
                     ShipOrientation.Horizontal, ShipOrientation.Vertical })
        {
            for (int x = 0; x < aiGrid.GridWidth; x++)
            {
                for (int y = 0; y < aiGrid.GridHeight; y++)
                {
                    if (SpaceAvailable(x, y, ship.length, direction, occupied))
                        choices.Add(new PlannedShip(ship.shipID, ship.length, x, y, direction));
                }
            }
        }

        // Shuffle choices so placement is random on every Play run.
        for (int i = choices.Count - 1; i > 0; i--)
        {
            int j = placementRandom.Next(i + 1);
            PlannedShip temp = choices[i];
            choices[i] = choices[j];
            choices[j] = temp;
        }

        foreach (PlannedShip choice in choices)
        {
            // An earlier recursion level may have changed the occupancy map.
            if (!SpaceAvailable(choice.x, choice.y, choice.length,
                                choice.orientation, occupied))
                continue;

            MarkCells(choice, occupied, true);
            plan.Add(choice);

            if (TryPlanFleet(fleet, shipIndex + 1, occupied, plan))
                return true;

            plan.RemoveAt(plan.Count - 1);
            MarkCells(choice, occupied, false);
        }

        return false;
    }

    private static bool SpaceAvailable(int x, int y, int length,
                                       ShipOrientation direction, bool[,] occupied)
    {
        for (int i = 0; i < length; i++)
        {
            int cx = x + (direction == ShipOrientation.Horizontal ? i : 0);
            int cy = y + (direction == ShipOrientation.Vertical ? i : 0);
            if (cx < 0 || cy < 0 || cx >= occupied.GetLength(0) ||
                cy >= occupied.GetLength(1) || occupied[cx, cy])
                return false;
        }
        return true;
    }

    private static void MarkCells(PlannedShip ship, bool[,] occupied, bool value)
    {
        for (int i = 0; i < ship.length; i++)
        {
            int x = ship.x + (ship.orientation == ShipOrientation.Horizontal ? i : 0);
            int y = ship.y + (ship.orientation == ShipOrientation.Vertical ? i : 0);
            occupied[x, y] = value;
        }
    }

    private bool TryReadCoordinate(string raw, out int x, out int y)
    {
        x = -1;
        y = -1;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string input = raw.Trim().ToUpperInvariant();
        if (input.Length < 2 || input.Length > 3 ||
            input[0] < 'A' || input[0] > 'J')
            return false;

        int column;
        if (!int.TryParse(input.Substring(1), out column) || column < 1 || column > 10)
            return false;

        int row = input[0] - 'A';
        x = column - 1;
        y = rowAIsYZero ? row : 9 - row;
        return true;
    }

    private string ToBoardLabel(int x, int y)
    {
        int row = rowAIsYZero ? y : 9 - y;
        return ((char)('A' + row)).ToString() + (x + 1);
    }

    private static string FormatShot(string shooter, string location,
                                     AttackResult result, GridManager attackedGrid,
                                     int x, int y)
    {
        if (result == AttackResult.Miss)
            return $"{shooter} fired at {location}: MISS.";
        if (result == AttackResult.Hit)
            return $"{shooter} fired at {location}: HIT!";
        if (result == AttackResult.Sunk)
        {
            ShipData sunk = attackedGrid.GetShipAt(x, y);
            string name = sunk != null ? sunk.shipID : "a ship";
            return $"{shooter} fired at {location}: SUNK {name}!";
        }
        return $"{shooter} fired at {location}: {result}.";
    }

    private static void SetText(TMP_Text component, string text)
    {
        if (component != null)
            component.text = text;
    }

}