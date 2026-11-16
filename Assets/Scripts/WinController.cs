using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class WinController : MonoBehaviour
{
    [Tooltip("Win menu object to show when the player enters this trigger.")]
    public GameObject winMenu;

    private bool won;
    private int winFrame;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (won && Time.frameCount > winFrame && keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (won) return;
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead) return;

        if (winMenu == null)
        {
            Debug.LogError("Assign a win menu to WinController to display it on player entry.", this);
            return;
        }

        won = true;
        winFrame = Time.frameCount;
        winMenu.SetActive(true);
    }
}
