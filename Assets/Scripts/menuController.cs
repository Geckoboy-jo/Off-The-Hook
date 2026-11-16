using UnityEngine;

public class menuController : MonoBehaviour
{
    public GameObject mainMenu;
    public GameObject levelMenu;
    public GameObject playerPrefab;
    public Transform startPoint;
    [Tooltip("Camera that follows the spawned player. Defaults to the MainCamera's CameraControlle.")]
    public CameraControlle cameraController;

    private GameObject spawnedPlayer;

    private void Start()
    {
        if (levelMenu != null) levelMenu.SetActive(false);
        if (mainMenu != null) mainMenu.SetActive(true);
    }

    public void PlayGame()
    {
        if (levelMenu == null)
        {
            Debug.LogError("Assign a level menu to menuController before playing.", this);
            return;
        }

        levelMenu.SetActive(true);
        if (mainMenu != null) mainMenu.SetActive(false);
    }

    public void LevelOne()
    {
        if (spawnedPlayer != null) return;
        if (playerPrefab == null || startPoint == null)
        {
            Debug.LogError("Level One requires a player prefab and start point.", this);
            return;
        }
        if (playerPrefab.GetComponent<PlayerController>() == null)
        {
            Debug.LogError("The player prefab must have a PlayerController on its root.", this);
            return;
        }

        if (cameraController == null && Camera.main != null)
            cameraController = Camera.main.GetComponent<CameraControlle>();
        if (cameraController == null)
        {
            Debug.LogError("Assign a CameraControlle so the camera can follow the spawned player.", this);
            return;
        }

        spawnedPlayer = Instantiate(playerPrefab, startPoint.position, startPoint.rotation);
        spawnedPlayer.GetComponent<PlayerController>().respawnPoint = startPoint;
        cameraController.target = spawnedPlayer;
        if (mainMenu != null) mainMenu.SetActive(false);
        if (levelMenu != null) levelMenu.SetActive(false);
    }

    public void Exit()
    {
        Application.Quit();
    }
}
