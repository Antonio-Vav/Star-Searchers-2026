using UnityEngine;
public class GridCell : MonoBehaviour
{
    [Header("Grid Coordinates")]
    [Range(0, 9)] public int x;
    [Range(0, 9)] public int y;

    // Runtime only board state. Do NOT let Unity serialize these fields.
    [System.NonSerialized] public ShipData ship;
    [System.NonSerialized] public bool hasBeenAttacked;

    public bool HasShip => ship != null;
    public bool IsEmpty => ship == null;
    public bool IsHit => hasBeenAttacked && HasShip;
    public bool IsMiss => hasBeenAttacked && !HasShip;

    public Vector2Int Coordinates => new Vector2Int(x, y);

    public void ResetCell()
    {
        ship = null;
        hasBeenAttacked = false;
    }

}