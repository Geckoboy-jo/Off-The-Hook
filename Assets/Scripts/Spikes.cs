using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Spikes : MonoBehaviour
{
    private Color randomColor;
    private void Awake()
    {
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        Light2D light = GetComponentInChildren<Light2D>();
        if (sprite == null || light == null)
        {
            Debug.LogError("Spikes requires a SpriteRenderer and a Light2D on itself or a child.", this);
            return;
        }

        randomColor = new Color(Random.value, Random.value, Random.value);
        sprite.color = randomColor;
        light.color = randomColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        KillPlayer(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        KillPlayer(collision.collider);
    }

    private void KillPlayer(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null) player.die();
    }
}
