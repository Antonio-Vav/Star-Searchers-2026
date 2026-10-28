using UnityEngine;

public class PlayerBoardSetup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager playerGrid;
    [SerializeField] private DraggableShip[] ships;
    [SerializeField] private GameObject confirmButton;

    public bool SetupComplete { get; private set; } = false;

    public GridManager PlayerGrid => playerGrid;

    public void ConfirmBoard()
    {
        if (SetupComplete)
            return;

        if (playerGrid == null || ships == null ||
            ships.Length == 0)
        {
            Debug.LogError("Missing board setup references!");
            return;
        }

        foreach (DraggableShip ship in ships)
        {
            if (ship == null || !ship.IsPlaced)
            {
                Debug.LogWarning("Not all ships are placed!");
                return;
            }
        }

        SetupComplete = true;

        if (confirmButton != null)
            confirmButton.SetActive(false);

        Debug.Log("Player board confirmed!");
    }

}