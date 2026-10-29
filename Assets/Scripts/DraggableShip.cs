
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class DraggableShip : MonoBehaviour
{
    [Header("Ship Settings")]
    [SerializeField] private string shipID = "Destroyer";
    [SerializeField] private int shipLength = 3;

    [Header("References")]
    [SerializeField] private PlayerBoardSetup boardSetup;

    private GridManager grid;
    private Camera mainCamera;

    private bool dragging = false;
    private bool isPlaced = false;

    private int placedX;
    private int placedY;

    // Your sprites are vertically oriented by default.
    private ShipOrientation orientation =
        ShipOrientation.Vertical;

    private ShipOrientation savedOrientation;

    private Vector3 savedPosition;
    private Quaternion savedRotation;

    private Vector3 dragOffset;

    public bool IsPlaced => isPlaced;

    private void Start()
    {
        mainCamera = Camera.main;

        if (boardSetup != null)
            grid = boardSetup.PlayerGrid;

        savedPosition = transform.position;
        savedRotation = transform.rotation;
        savedOrientation = orientation;
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (Mouse.current == null || mainCamera == null)
            return transform.position;

        Vector3 mouse = Mouse.current.position.ReadValue();

        mouse.z = Mathf.Abs(
            mainCamera.transform.position.z -
            transform.position.z
        );

        Vector3 world = mainCamera.ScreenToWorldPoint(mouse);
        world.z = transform.position.z;

        return world;
    }

    private void OnMouseDown()
    {
        if (boardSetup == null ||
            boardSetup.SetupComplete ||
            grid == null ||
            mainCamera == null)
            return;

        dragging = true;

        // Keep the point where you clicked on the ship.
        dragOffset = transform.position - GetMouseWorldPosition();
    }

    private void OnMouseDrag()
    {
        if (!dragging)
            return;

        transform.position = GetMouseWorldPosition() + dragOffset;
    }

    private void Update()
    {
        if (!dragging)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            orientation =
                orientation == ShipOrientation.Horizontal
                    ? ShipOrientation.Vertical
                    : ShipOrientation.Horizontal;

            UpdateRotation();
        }
    }

    private void OnMouseUp()
    {
        if (!dragging)
            return;

        dragging = false;

        // Find the grid position that best matches
        // the ship's current center.
        GridCell targetCell = FindBestPlacement();

        if (targetCell == null)
        {
            RestoreShip();
            return;
        }

        bool previouslyPlaced = isPlaced;

        int oldX = placedX;
        int oldY = placedY;

        // Temporarily remove this ship from the grid
        // so it doesn't overlap itself while moving.
        if (previouslyPlaced)
        {
            if (!grid.RemoveShip(shipID))
            {
                RestoreShip();
                return;
            }
        }

        bool success = grid.AddShip(
            shipID,
            targetCell.x,
            targetCell.y,
            shipLength,
            orientation
        );

        Debug.Log(
            $"Placement attempt: Ship={shipID}, " +
            $"Length={shipLength}, " +
            $"Start=({targetCell.x}, {targetCell.y}), " +
            $"Orientation={orientation}, " +
            $"Success={success}"
        );

        if (success)
        {
            isPlaced = true;

            placedX = targetCell.x;
            placedY = targetCell.y;

            SnapToGrid(placedX, placedY);

            savedPosition = transform.position;
            savedRotation = transform.rotation;
            savedOrientation = orientation;

            Debug.Log(shipID + " placed successfully!");
        }
        else
        {
            if (previouslyPlaced)
            {
                grid.AddShip(
                    shipID,
                    oldX,
                    oldY,
                    shipLength,
                    savedOrientation
                );
            }

            RestoreShip();

            Debug.LogWarning(
                $"Invalid placement for {shipID} at " +
                $"({targetCell.x}, {targetCell.y}), " +
                $"orientation: {orientation}, length: {shipLength}."
            );
        }
    }

    private GridCell FindBestPlacement()
    {
        GridCell bestCell = null;
        float bestDistance = Mathf.Infinity;

        // Measure grid spacing using real cell positions.
        GridCell cell00 = grid.GetCell(0, 0);
        GridCell cell10 = grid.GetCell(1, 0);
        GridCell cell01 = grid.GetCell(0, 1);

        if (cell00 == null || cell10 == null || cell01 == null)
            return null;

        float xSpacing = Vector2.Distance(
            cell00.transform.position,
            cell10.transform.position
        );

        float ySpacing = Vector2.Distance(
            cell00.transform.position,
            cell01.transform.position
        );

        float maxDistance = Mathf.Max(xSpacing, ySpacing) * 0.75f;

        // Test every possible starting cell.
        for (int x = 0; x < grid.GridWidth; x++)
        {
            for (int y = 0; y < grid.GridHeight; y++)
            {
                int endX = x;
                int endY = y;

                if (orientation == ShipOrientation.Horizontal)
                    endX += shipLength - 1;
                else
                    endY += shipLength - 1;

                GridCell first = grid.GetCell(x, y);
                GridCell last = grid.GetCell(endX, endY);

                // Ignore placements that extend off the board.
                if (first == null || last == null)
                    continue;

                Vector3 center =
                    (first.transform.position +
                     last.transform.position) / 2f;

                // Ignore Z distance.
                float distance = Vector2.Distance(
                    transform.position,
                    center
                );

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = first;
                }
            }
        }

        // Don't allow dropping far away from the grid.
        if (bestDistance > maxDistance)
            return null;

        return bestCell;
    }

    private void SnapToGrid(int x, int y)
    {
        GridCell first = grid.GetCell(x, y);

        int endX = x;
        int endY = y;

        if (orientation == ShipOrientation.Horizontal)
            endX += shipLength - 1;
        else
            endY += shipLength - 1;

        GridCell last = grid.GetCell(endX, endY);

        if (first == null || last == null)
            return;

        Vector3 center =
            (first.transform.position +
             last.transform.position) / 2f;

        center.z = transform.position.z;

        transform.position = center;

        UpdateRotation();
    }


    private void UpdateRotation()
    {
        // Vertical sprites point upward by default.
        float angle =
            orientation == ShipOrientation.Vertical
                ? 0f
                : -90f;

        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void RestoreShip()
    {
        orientation = savedOrientation;

        transform.position = savedPosition;
        transform.rotation = savedRotation;
    }

}