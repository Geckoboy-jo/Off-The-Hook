using UnityEngine;

public class Soundmanager : MonoBehaviour
{
    public static Soundmanager Instance { get; private set; }

    [Header("Background Music")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float backgroundMusicVolume = 0.5f;

    [Header("Grapple")]
    public AudioClip grappleExtend;
    [Range(0f, 1f)] public float grappleExtendVolume = 1f;
    public AudioClip grappleRetract;
    [Range(0f, 1f)] public float grappleRetractVolume = 1f;
    public AudioClip shoot;
    [Range(0f, 1f)] public float shootVolume = 1f;
    public AudioClip grappleHitTarget;
    [Range(0f, 1f)] public float grappleHitTargetVolume = 1f;
    public AudioClip detachRope;
    [Range(0f, 1f)] public float detachRopeVolume = 1f;

    private AudioSource musicSource;
    private AudioSource extendSource;
    private AudioSource retractSource;
    private AudioSource sfxSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        musicSource = CreateSource(true);
        extendSource = CreateSource(true);
        retractSource = CreateSource(false);
        sfxSource = CreateSource(false);
    }

    void Start()
    {
        if (Instance != this || backgroundMusic == null) return;
        musicSource.clip = backgroundMusic;
        musicSource.volume = backgroundMusicVolume;
        musicSource.Play();
    }

    void Update()
    {
        if (Instance != this) return;
        // Lets the sliders be adjusted live in the inspector.
        musicSource.volume = backgroundMusicVolume;
        extendSource.volume = grappleExtendVolume;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    public void PlayShoot() => PlayOneShot(shoot, shootVolume);
    public void PlayGrappleHitTarget() => PlayOneShot(grappleHitTarget, grappleHitTargetVolume);
    public void PlayDetachRope() => PlayOneShot(detachRope, detachRopeVolume);

    public void PlayGrappleRetract()
    {
        if (grappleRetract == null) return;
        retractSource.clip = grappleRetract;
        retractSource.volume = grappleRetractVolume;
        retractSource.Play();
    }

    public void StopGrappleRetract()
    {
        if (retractSource.isPlaying) retractSource.Stop();
    }

    public void StartGrappleExtend()
    {
        if (grappleExtend == null) return;
        extendSource.clip = grappleExtend;
        extendSource.volume = grappleExtendVolume;
        extendSource.Play();
    }

    public void StopGrappleExtend()
    {
        if (extendSource.isPlaying) extendSource.Stop();
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, volume);
    }
}
