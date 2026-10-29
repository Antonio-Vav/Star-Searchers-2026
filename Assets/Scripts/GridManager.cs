
using System;
using System.Collections.Generic;
using UnityEngine;

public enum ShipOrientation
{
    Horizontal,
    Vertical
}

public enum AttackResult
{
    Invalid,
    AlreadyAttacked,
    Miss,
    Hit,
    Sunk
}

[Serializable]
public class ShipData
{
    public string shipID;
    public int length;
    public List<GridCell> occupiedCells = new List<GridCell>();
    public HashSet<GridCell> hitCells = new HashSet<GridCell>();

    public bool IsSunk => occupiedCells.Count > 0 && hitCells.Count == occupiedCells.Count;

    public int RemainingHealth => occupiedCells.Count - hitCells.Count;

    public ShipData(string id, int size)
    {
        shipID = id;
        length = size;
    }

    public void RegisterHit(GridCell cell)
    {
        if (occupiedCells.Contains(cell))
        {
            hitCells.Add(cell);
        }
    }
}

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;

    [Header("Cell References")]
    [SerializeField] private GridCell[] cells;

    private GridCell[,] grid;

    private Dictionary<string, ShipData> ships = new Dictionary<string, ShipData>();

    public event Action<GridCell, AttackResult> OnCellAttacked;
    public event Action<ShipData> OnShipPlaced;
    public event Action<ShipData> OnShipRemoved;
    public event Action<ShipData> OnShipSunk;
    public event Action OnAllShipsSunk;

    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public int ShipCount => ships.Count;

    private void Awake()
    {
        InitializeGrid();
    }

    // GRID INITIALIZATION


    [Header("Grid References")]
    [SerializeField] private Transform cellGridParent;

    public void InitializeGrid()
    {
        if (cellGridParent == null)
        {
            Debug.LogError("CellGrid parent is not assigned!");
            return;
        }

        // Finds all GridCell components under CellGrid,
        // including cells nested inside CellGroups.
        cells = cellGridParent.GetComponentsInChildren<GridCell>(true);

        Debug.Log($"Found {cells.Length} GridCell components.");

        foreach (GridCell cell in cells)
        {
            if (cell != null)
            {
                Debug.Log(
                    $"Found cell: {cell.name}, " +
                    $"Coordinates: ({cell.x}, {cell.y})"
                );
            }
        }

        grid = new GridCell[gridWidth, gridHeight];
        ships.Clear();

        int registeredCells = 0;

        foreach (GridCell cell in cells)
        {
            if (!IsWithinBounds(cell.x, cell.y))
            {
                Debug.LogError(
                    $"Cell {cell.name} has invalid coordinates ({cell.x}, {cell.y})"
                );
                continue;
            }

            if (grid[cell.x, cell.y] != null)
            {
                Debug.LogError(
                    $"Duplicate coordinates ({cell.x}, {cell.y})!"
                );
                continue;
            }

            grid[cell.x, cell.y] = cell;

            if (cell.ship != null)
            {
                Debug.LogWarning(
                    $"Cell ({cell.x}, {cell.y}) already has a ship " +
                    $"before ResetCell(). ID='{cell.ship.shipID}', " +
                    $"Length={cell.ship.length}, Cell={cell.name}"
                );
            }

            cell.ResetCell();
            Debug.Log(
                $"Reset cell ({cell.x}, {cell.y}): ship is null = {cell.ship == null}"
            );

            registeredCells++;
        }

        Debug.Log($"Grid initialized: {registeredCells}/{gridWidth * gridHeight} cells registered.");

        if (registeredCells != gridWidth * gridHeight)
        {
            Debug.LogError("Grid initialization incomplete! Check cell coordinates and GridCell components.");
        }
    }


    // CELL METHODS

    public bool IsWithinBounds(int x, int y)
    {
        return x >= 0 &&
               x < gridWidth &&
               y >= 0 &&
               y < gridHeight;
    }

    public GridCell GetCell(int x, int y)
    {
        if (grid == null)
        {
            Debug.LogError("Grid has not been initialized!");
            return null;
        }

        if (x < 0 || x >= grid.GetLength(0) ||
            y < 0 || y >= grid.GetLength(1))
        {
            return null;
        }

        return grid[x, y];
    }

    public bool IsCellEmpty(int x, int y)
    {
        GridCell cell = GetCell(x, y);

        return cell != null && cell.IsEmpty;
    }

    public bool HasShipAt(int x, int y)
    {
        GridCell cell = GetCell(x, y);

        return cell != null && cell.HasShip;
    }

    public bool HasBeenAttacked(int x, int y)
    {
        GridCell cell = GetCell(x, y);

        return cell != null && cell.hasBeenAttacked;
    }

    // SHIP PLACEMENT

    
    public bool CanPlaceShip(int x, int y, int length, ShipOrientation orientation)
    {
        if (length <= 0)
        {
            Debug.LogWarning($"Invalid ship length: {length}");
            return false;
        }

        for (int i = 0; i < length; i++)
        {
            int checkX = x;
            int checkY = y;

            if (orientation == ShipOrientation.Horizontal)
                checkX += i;
            else
                checkY += i;

            GridCell cell = GetCell(checkX, checkY);

            // Check whether the grid position exists.
            if (cell == null)
            {
                Debug.LogWarning(
                    $"Placement rejected: no cell exists at ({checkX}, {checkY})."
                );
                return false;
            }

            // Check whether the cell already contains a ship.
            if (cell.ship != null)
            {
                Debug.LogError(
                    $"OCCUPIED CELL: ({checkX}, {checkY}) | " +
                    $"Ship ID: '{cell.ship.shipID}' | " +
                    $"Ship length: {cell.ship.length} | " +
                    $"GridManager: {gameObject.name}"
                );

                return false;
            }

            // Check whether the cell has already been attacked.
            if (cell.hasBeenAttacked)
            {
                Debug.LogWarning(
                    $"Placement rejected: cell ({checkX}, {checkY}) " +
                    "has already been attacked."
                );
                return false;
            }
        }

        return true;
    }


    public bool AddShip(string shipID, int x, int y, int length, ShipOrientation orientation)
    {
        if (string.IsNullOrWhiteSpace(shipID))
            return false;

        if (ships.ContainsKey(shipID))
        {
            Debug.LogWarning($"Ship {shipID} already exists.");
            return false;
        }

        if (!CanPlaceShip(x, y, length, orientation))
        {
            Debug.LogWarning("Invalid ship placement.");
            return false;
        }

        ShipData newShip = new ShipData(shipID, length);

        for (int i = 0; i < length; i++)
        {
            int cellX = x;
            int cellY = y;

            if (orientation == ShipOrientation.Horizontal)
                cellX += i;
            else
                cellY += i;

            GridCell cell = GetCell(cellX, cellY);

            cell.ship = newShip;
            newShip.occupiedCells.Add(cell);
        }

        ships.Add(shipID, newShip);

        OnShipPlaced?.Invoke(newShip);

        Debug.Log($"Placed {shipID}");

        return true;
    }

    // SHIP REMOVAL

    public bool RemoveShip(string shipID)
    {
        if (!ships.TryGetValue(shipID, out ShipData ship))
            return false;

        // Don't remove ships that have been attacked already.
        if (ship.hitCells.Count > 0)
            return false;

        foreach (GridCell cell in ship.occupiedCells)
        {
            cell.ship = null;
        }

        ships.Remove(shipID);

        OnShipRemoved?.Invoke(ship);

        return true;
    }

    public bool RemoveShipAt(int x, int y)
    {
        GridCell cell = GetCell(x, y);

        if (cell == null || !cell.HasShip)
            return false;

        return RemoveShip(cell.ship.shipID);
    }

    // SHIP INFORMATION

    public ShipData GetShip(string shipID)
    {
        ships.TryGetValue(shipID, out ShipData ship);
        return ship;
    }

    public ShipData GetShipAt(int x, int y)
    {
        GridCell cell = GetCell(x, y);

        return cell != null ? cell.ship : null;
    }

    public bool IsShipSunk(string shipID)
    {
        ShipData ship = GetShip(shipID);

        return ship != null && ship.IsSunk;
    }

    public List<ShipData> GetAllShips()
    {
        return new List<ShipData>(ships.Values);
    }

    public int GetRemainingShipCount()
    {
        int count = 0;

        foreach (ShipData ship in ships.Values)
        {
            if (!ship.IsSunk)
                count++;
        }

        return count;
    }

    // ATTACK LOGIC

    public AttackResult AttackCell(int x, int y)
    {
        GridCell cell = GetCell(x, y);

        if (cell == null)
            return AttackResult.Invalid;

        if (cell.hasBeenAttacked)
            return AttackResult.AlreadyAttacked;

        cell.hasBeenAttacked = true;

        if (!cell.HasShip)
        {
            OnCellAttacked?.Invoke(cell, AttackResult.Miss);
            return AttackResult.Miss;
        }

        ShipData ship = cell.ship;

        ship.RegisterHit(cell);

        if (ship.IsSunk)
        {
            OnCellAttacked?.Invoke(cell, AttackResult.Sunk);
            OnShipSunk?.Invoke(ship);

            if (AreAllShipsSunk())
                OnAllShipsSunk?.Invoke();

            return AttackResult.Sunk;
        }

        OnCellAttacked?.Invoke(cell, AttackResult.Hit);
        return AttackResult.Hit;
    }

    // WIN CONDITION

    public bool AreAllShipsSunk()
    {
        if (ships.Count == 0)
            return false;

        foreach (ShipData ship in ships.Values)
        {
            if (!ship.IsSunk)
                return false;
        }

        return true;
    }

    // BOARD INFORMATION

    public int GetTotalShipCells()
    {
        int total = 0;

        foreach (ShipData ship in ships.Values)
        {
            total += ship.length;
        }

        return total;
    }

    public int GetTotalHits()
    {
        int total = 0;

        foreach (ShipData ship in ships.Values)
        {
            total += ship.hitCells.Count;
        }

        return total;
    }

    public int GetTotalAttacks()
    {
        int total = 0;

        foreach (GridCell cell in cells)
        {
            if (cell != null && cell.hasBeenAttacked)
                total++;
        }

        return total;
    }

    // RESET METHODS

    public void ClearShips()
    {
        ResetBoard();
    }

    public void ResetBoard()
    {
        ships.Clear();

        foreach (GridCell cell in cells)
        {
            if (cell != null)
                cell.ResetCell();
        }
    }

    public void ResetAttacks()
    {
        foreach (GridCell cell in cells)
        {
            if (cell != null)
                cell.hasBeenAttacked = false;
        }

        foreach (ShipData ship in ships.Values)
        {
            ship.hitCells.Clear();
        }
    }

}
