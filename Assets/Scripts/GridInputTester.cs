
using UnityEngine;

public class GridInputTester : MonoBehaviour
{
    [Header("Grid Reference")]
    [SerializeField] private GridManager gridManager;

    [Header("Test Coordinates (0-9)")]
    [Range(0, 9)]
    [SerializeField] private int x;

    [Range(0, 9)]
    [SerializeField] private int y;

    [Header("Test Controls")]
    [SerializeField] private bool testAttack;

    private void Update()
    {
        // Run the test when the checkbox is enabled.
        if (testAttack)
        {
            testAttack = false;
            TestInput();
        }
    }

    public void TestInput()
    {
        if (gridManager == null)
        {
            Debug.LogError(
                "GridManager is not assigned! Drag it into the Inspector."
            );
            return;
        }

        Debug.Log($"Testing input at coordinates ({x}, {y})");

        // Retrieve the selected cell.
        GridCell cell = gridManager.GetCell(x, y);

        if (cell == null)
        {
            Debug.LogError($"No grid cell exists at ({x}, {y}).");
            return;
        }

        Debug.Log($"Cell found: {cell.name}");

        // Send the coordinates to the existing attack logic.
        AttackResult result = gridManager.AttackCell(x, y);

        Debug.Log($"Attack result at ({x}, {y}): {result}");
    }
}
