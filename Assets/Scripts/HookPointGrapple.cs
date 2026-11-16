using UnityEngine;

public class HookPointGrapple : MonoBehaviour
{
    [Tooltip("Distance from the player at which this point releases the grapple.")]
    [Min(0.01f)] public float releaseDistance = 0.3048f;

    public void Consume(PlayerController player)
    {
        if (player == null)
        {
            Debug.LogError("Consuming a grapple point requires a PlayerController to restore it on death.", this);
            return;
        }
        player.TrackConsumedHookPoint(this);
        gameObject.SetActive(false);
    }

    internal void Restore()
    {
        gameObject.SetActive(true);
    }
}
