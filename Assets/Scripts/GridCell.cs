
using UnityEngine;

public class GridCell : MonoBehaviour
{
    [Header("Grid Coordinates")]
    [Range(0, 9)] public int x;
    [Range(0, 9)] public int y;

    [HideInInspector] public ShipData ship;
    [HideInInspector] public bool hasBeenAttacked;

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