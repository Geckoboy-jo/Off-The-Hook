using UnityEngine;

public class HookPointGrapple : MonoBehaviour
{
    [Tooltip("Distance from the player at which this point releases the grapple.")]
    [Min(0.01f)] public float releaseDistance = 0.3048f;

    private readonly System.Collections.Generic.List<GameObject> attachedHooks =
        new System.Collections.Generic.List<GameObject>();

    internal void RegisterHook(GameObject hook)
    {
        if (hook != null && !attachedHooks.Contains(hook)) attachedHooks.Add(hook);
    }

    void OnDisable()
    {
        foreach (GameObject hook in attachedHooks)
            if (hook != null) Destroy(hook);
        attachedHooks.Clear();
    }

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
