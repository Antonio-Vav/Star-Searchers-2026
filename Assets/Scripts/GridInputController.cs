
using UnityEngine;

public class GridInputController : MonoBehaviour
{
    [Header("Grid Reference")]
    [SerializeField] private GridManager gridManager;

    // Receives a selected grid position and processes the attack.
    public AttackResult AttackAt(int x, int y)
    {
        if (gridManager == null)
        {
            Debug.LogError("GridManager is not assigned!");
            return AttackResult.Invalid;
        }

        // Validate the selected coordinates.
        if (!gridManager.IsWithinBounds(x, y))
        {
            Debug.LogWarning(
                $"Position ({x}, {y}) is outside the grid."
            );

            return AttackResult.Invalid;
        }

        // Process the attack using your partner's existing logic.
        AttackResult result = gridManager.AttackCell(x, y);

        // Report the result.
        switch (result)
        {
            case AttackResult.Invalid:
                Debug.Log("Invalid attack position.");
                break;

            case AttackResult.AlreadyAttacked:
                Debug.Log("This position was already attacked.");
                break;

            case AttackResult.Miss:
                Debug.Log($"Miss at ({x}, {y})!");
                break;

            case AttackResult.Hit:
                Debug.Log($"Hit at ({x}, {y})!");
                break;

            case AttackResult.Sunk:
                Debug.Log($"Ship sunk at ({x}, {y})!");
                break;
        }

        return result;
    }
}
