using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class DraggableShip : MonoBehaviour
{
    [Header("Ship Settings")]
    [SerializeField] private string shipID = "Destroyer";
    [Min(1)][SerializeField] private int shipLength = 3;

    [Header("References")]
    [SerializeField] private PlayerBoardSetup boardSetup;
    [SerializeField] private Camera inputCamera;

    [Header("Placement Settings")]
    [Tooltip("How far the ship center may be from the nearest valid grid center, in cell spacings.")]
    [Range(0.25f, 1.5f)]
    [SerializeField] private float maxSnapDistanceInCells = 0.75f;

    [SerializeField] private bool logPlacement = true;

    private static DraggableShip activeDrag;

    private GridManager grid;
    private Collider2D shipCollider;

    private bool dragging;
    private bool isPlaced;
    private int placedX;
    private int placedY;

    // These ship sprites point up at rotation zero, like in the original project.
    private ShipOrientation orientation = ShipOrientation.Vertical;
    private ShipOrientation savedOrientation;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private Vector3 dragOffset;

    public bool IsPlaced => isPlaced;

    private void Awake()
    {
        shipCollider = GetComponent<Collider2D>();
        if (inputCamera == null)
            inputCamera = Camera.main;

        if (boardSetup != null)
            grid = boardSetup.PlayerGrid;

        savedPosition = transform.position;
        savedRotation = transform.rotation;
        savedOrientation = orientation;
    }

    private void Start()
    {
        // PlayerBoardSetup / GridManager references may be available after Awake.
        if (boardSetup == null)
            boardSetup = FindFirstObjectByType<PlayerBoardSetup>();

        if (boardSetup != null)
            grid = boardSetup.PlayerGrid;

        if (inputCamera == null)
            inputCamera = Camera.main;

        if (boardSetup == null || grid == null || inputCamera == null)
        {
            Debug.LogError($"{name}: Assign Board Setup, its Player Grid, and a Camera.", this);
        }

        if (string.IsNullOrWhiteSpace(shipID))
            Debug.LogError($"{name}: Ship ID cannot be blank.", this);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (!dragging)
        {
            if (mouse.leftButton.wasPressedThisFrame)
                TryBeginDrag(mouse.position.ReadValue());

            return;
        }

        // Keep rotation/dragging/release within the SAME Input System.
        if (boardSetup == null || boardSetup.SetupComplete || grid == null)
        {
            CancelDrag();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            orientation = orientation == ShipOrientation.Vertical
                ? ShipOrientation.Horizontal
                : ShipOrientation.Vertical;
            UpdateRotation();
        }

        // Read the pointer at release too; OnMouseDrag can miss the last frame.
        Vector2 screenPoint = mouse.position.ReadValue();
        MoveToScreenPoint(screenPoint);

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            FinishDrag();
        }
        else if (!mouse.leftButton.isPressed)
        {
            // Lost mouse capture or focus: don't commit a stale placement.
            CancelDrag();
        }
    }

    private bool TryGetMouseWorldPosition(Vector2 screenPoint, out Vector3 worldPoint)
    {
        worldPoint = transform.position;
        if (inputCamera == null)
            return false;

        Ray mouseRay = inputCamera.ScreenPointToRay(screenPoint);
        Plane shipPlane = new Plane(Vector3.forward,
            new Vector3(0f, 0f, transform.position.z));

        if (!shipPlane.Raycast(mouseRay, out float hitDistance))
            return false;

        worldPoint = mouseRay.GetPoint(hitDistance);
        worldPoint.z = transform.position.z;
        return true;
    }

    private void TryBeginDrag(Vector2 screenPoint)
    {
        if (activeDrag != null || boardSetup == null || boardSetup.SetupComplete ||
            grid == null || inputCamera == null || shipCollider == null)
            return;

        if (screenPoint.x < 0 || screenPoint.y < 0 ||
            screenPoint.x > Screen.width || screenPoint.y > Screen.height)
            return;

        if (!TryGetMouseWorldPosition(screenPoint, out Vector3 mouseWorld))
            return;

        if (!shipCollider.OverlapPoint(new Vector2(mouseWorld.x, mouseWorld.y)))
            return;

        activeDrag = this;
        dragging = true;
        dragOffset = transform.position - mouseWorld;
    }

    private void MoveToScreenPoint(Vector2 screenPoint)
    {
        if (TryGetMouseWorldPosition(screenPoint, out Vector3 mouseWorld))
        {
            Vector3 next = mouseWorld + dragOffset;
            next.z = transform.position.z;
            transform.position = next;
        }
    }

    private void FinishDrag()
    {
        dragging = false;
        if (activeDrag == this)
            activeDrag = null;

        GridCell targetCell = FindBestPlacement(out float distance);
        if (targetCell == null)
        {
            if (logPlacement)
                Debug.LogWarning($"{name}: Drop is too far from logical GridCells. " +
                    $"Nearest center distance: {distance:F3} world units. " +
                    "If the ship visually overlaps the grid, your background " +
                    "image and actual GridCells are not aligned.", this);

            RestoreShip();
            return;
        }

        ShipOrientation attemptedOrientation = orientation;
        bool previouslyPlaced = isPlaced;
        int oldX = placedX;
        int oldY = placedY;
        ShipOrientation oldOrientation = savedOrientation;

      
        if (previouslyPlaced && !grid.RemoveShip(shipID))
        {
            Debug.LogError($"{name}: Could not remove {shipID} for repositioning.", this);
            RestoreShip();
            return;
        }

        bool success = grid.AddShip(shipID, targetCell.x, targetCell.y,
                                    shipLength, orientation);

        if (success)
        {
            isPlaced = true;
            placedX = targetCell.x;
            placedY = targetCell.y;
            SnapToGrid(placedX, placedY);
            savedPosition = transform.position;
            savedRotation = transform.rotation;
            savedOrientation = orientation;

            if (logPlacement)
                Debug.Log($"Placed {shipID} at ({placedX}, {placedY}), " +
                          $"length {shipLength}, {orientation}.", this);
        }
        else
        {
            // If a previously placed ship failed to move, put its exact
            // previous occupancy back. Never leave the visual and grid out of sync.
            if (previouslyPlaced &&
                !grid.AddShip(shipID, oldX, oldY, shipLength, oldOrientation))
            {
                isPlaced = false;
                Debug.LogError($"{name}: Failed to restore previous grid occupancy " +
                               "after an invalid move. Check ship IDs and grid resets.", this);
            }

            RestoreShip();

            if (logPlacement)
                Debug.LogWarning($"Invalid placement for {shipID}. " +
                    $"Proposed start: ({targetCell.x}, {targetCell.y}), " +
                    $"length: {shipLength}, orientation: {attemptedOrientation}. " +
                    "Read the preceding GridManager message for the exact " +
                    "cause (occupied / missing cell / duplicate Ship ID).", this);
        }
    }

    private GridCell FindBestPlacement(out float nearestDistance)
    {
        nearestDistance = float.PositiveInfinity;
        if (grid == null || shipLength <= 0)
            return null;

        GridCell cell00 = grid.GetCell(0, 0);
        GridCell cell10 = grid.GetCell(1, 0);
        GridCell cell01 = grid.GetCell(0, 1);
        if (cell00 == null || cell10 == null || cell01 == null)
        {
            Debug.LogError($"{name}: Grid is missing cells (0,0), (1,0), or (0,1).", this);
            return null;
        }

        float xSpacing = Vector2.Distance(cell00.transform.position,
                                          cell10.transform.position);
        float ySpacing = Vector2.Distance(cell00.transform.position,
                                          cell01.transform.position);
        float allowedDistance = Mathf.Max(xSpacing, ySpacing) * maxSnapDistanceInCells;

        GridCell bestCell = null;
        for (int x = 0; x < grid.GridWidth; x++)
        {
            for (int y = 0; y < grid.GridHeight; y++)
            {
                int endX = x + (orientation == ShipOrientation.Horizontal ? shipLength - 1 : 0);
                int endY = y + (orientation == ShipOrientation.Vertical ? shipLength - 1 : 0);

                GridCell first = grid.GetCell(x, y);
                GridCell last = grid.GetCell(endX, endY);
                if (first == null || last == null)
                    continue;

                Vector3 center = (first.transform.position + last.transform.position) * 0.5f;
                float distance = Vector2.Distance(transform.position, center);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    bestCell = first;
                }
            }
        }

        return nearestDistance <= allowedDistance ? bestCell : null;
    }

    private void SnapToGrid(int x, int y)
    {
        GridCell first = grid.GetCell(x, y);
        GridCell last = grid.GetCell(
            x + (orientation == ShipOrientation.Horizontal ? shipLength - 1 : 0),
            y + (orientation == ShipOrientation.Vertical ? shipLength - 1 : 0));

        if (first == null || last == null)
            return;

        Vector3 center = (first.transform.position + last.transform.position) * 0.5f;
        center.z = transform.position.z;
        transform.position = center;
        UpdateRotation();
    }

    private void UpdateRotation()
    {
        float angle = orientation == ShipOrientation.Vertical ? 0f : -90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void RestoreShip()
    {
        orientation = savedOrientation;
        transform.position = savedPosition;
        transform.rotation = savedRotation;
    }

    private void CancelDrag()
    {
        if (!dragging)
            return;

        dragging = false;
        if (activeDrag == this)
            activeDrag = null;
        RestoreShip();
    }

    private void OnDisable()
    {
        CancelDrag();
    }
}
