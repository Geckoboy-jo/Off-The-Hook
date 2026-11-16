using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public GameObject checkpointEffect;
    [Tooltip("Respawn location. If unassigned, the checkpoint's position is used.")]
    public Transform respawnPoint;
    private bool activated;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead) return;

        player.respawnPoint = respawnPoint != null ? respawnPoint : transform;
        activated = true;
        if (checkpointEffect != null)
        {
            if (checkpointEffect.scene.IsValid())
                checkpointEffect.transform.position = transform.position;
            else
                Instantiate(checkpointEffect, transform.position, checkpointEffect.transform.rotation);
        }
        gameObject.SetActive(false);
    }
}
