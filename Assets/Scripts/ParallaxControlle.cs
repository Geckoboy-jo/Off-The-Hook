using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class ParallaxControlle : MonoBehaviour
{
    [Serializable]
    public class Layer
    {
        public Sprite image;
        [Tooltip("Horizontal scrolling: 0 stays in the world; 1 follows the camera. Vertical movement always follows the camera.")]
        [Range(0f, 1f)] public float scrollScale = 0.5f;
        [Min(0.01f)] public float imageScale = 1f;
        public Vector3 offset;
        [Tooltip("Draw order within Background. Pieces with the same order and scroll scale repeat together.")]
        public int sortingOrder = -10;
    }

    public Camera targetCamera;
    [Tooltip("Lit sprite material so 2D lights affect the background in builds. Falls back to the pipeline default.")]
    public Material spriteMaterial;
    public Layer[] layers = Array.Empty<Layer>();

    private class RepeatingLayer
    {
        public Layer settings;
        public Transform root;
        public Vector3 origin;
        public float minX;
        public float maxX;
        public readonly List<Layer> pieces = new List<Layer>();
        public readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();
    }

    private readonly List<RepeatingLayer> repeatingLayers = new List<RepeatingLayer>();
    private Vector3 initialCameraPosition;

    void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null || !targetCamera.orthographic)
        {
            Debug.LogError("Parallax requires an assigned orthographic camera or an orthographic MainCamera.", this);
            enabled = false;
            return;
        }
        initialCameraPosition = targetCamera.transform.position;
        if (layers == null)
        {
            Debug.LogError("Assign parallax layers with images.", this);
            enabled = false;
            return;
        }
        foreach (Layer layer in layers)
        {
            if (layer == null || layer.image == null || layer.imageScale <= 0f)
            {
                Debug.LogError("Each parallax layer requires an image and a positive image scale.", this);
                continue;
            }
            Vector2 size = (Vector2)layer.image.bounds.size * layer.imageScale;
            if (size.x <= 0f || size.y <= 0f)
            {
                Debug.LogError("Parallax images must have non-zero width and height.", this);
                continue;
            }
            RepeatingLayer group = repeatingLayers.Find(candidate =>
                candidate.settings.sortingOrder == layer.sortingOrder &&
                candidate.settings.scrollScale == layer.scrollScale);
            float minX = layer.offset.x - size.x * 0.5f;
            float maxX = layer.offset.x + size.x * 0.5f;
            if (group == null)
            {
                Transform root = new GameObject("Parallax " + layer.image.name).transform;
                // World-space sizing avoids inherited scale changing the repeat spacing.
                root.localScale = Vector3.one;
                group = new RepeatingLayer
                {
                    settings = layer,
                    root = root,
                    origin = transform.position,
                    minX = minX,
                    maxX = maxX
                };
                repeatingLayers.Add(group);
            }
            group.minX = Mathf.Min(group.minX, minX);
            group.maxX = Mathf.Max(group.maxX, maxX);
            group.pieces.Add(layer);
        }
        LateUpdate();
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;
        Vector3 cameraPosition = targetCamera.transform.position;
        float viewHeight = targetCamera.orthographicSize * 2f;
        float viewWidth = viewHeight * targetCamera.aspect;
        foreach (RepeatingLayer layer in repeatingLayers)
        {
            Vector3 origin = layer.origin +
                (cameraPosition - initialCameraPosition) * layer.settings.scrollScale;
            origin.y = layer.origin.y + cameraPosition.y - initialCameraPosition.y;
            origin.z = layer.origin.z;
            float repeatWidth = layer.maxX - layer.minX;
            int columns = Mathf.CeilToInt(viewWidth / repeatWidth) + 3;
            int required = columns * layer.pieces.Count;
            while (layer.tiles.Count < required)
            {
                GameObject tile = new GameObject("Tile");
                tile.transform.SetParent(layer.root, false);
                SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                Layer piece = layer.pieces[layer.tiles.Count % layer.pieces.Count];
                renderer.sprite = piece.image;
                Material lit = spriteMaterial;
                if (lit == null && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
                    lit = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.default2DMaterial;
                if (lit != null) renderer.sharedMaterial = lit;
                renderer.sortingLayerName = "Background";
                renderer.sortingOrder = layer.settings.sortingOrder;
                tile.transform.localScale = Vector3.one * piece.imageScale;
                layer.tiles.Add(renderer);
            }
            int startX = Mathf.FloorToInt(
                (cameraPosition.x - origin.x - layer.minX) / repeatWidth) - columns / 2;
            for (int i = 0; i < layer.tiles.Count; i++)
            {
                SpriteRenderer tile = layer.tiles[i];
                tile.gameObject.SetActive(i < required);
                if (i >= required) continue;
                Layer piece = layer.pieces[i % layer.pieces.Count];
                Vector3 spriteCenter = piece.image.bounds.center * piece.imageScale;
                tile.transform.position = origin + piece.offset + new Vector3(
                    (startX + i / layer.pieces.Count) * repeatWidth,
                    0f, 0f) - spriteCenter;
            }
        }
    }

    void OnDisable()
    {
        foreach (RepeatingLayer layer in repeatingLayers)
            if (layer.root != null) layer.root.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        foreach (RepeatingLayer layer in repeatingLayers)
            if (layer.root != null) layer.root.gameObject.SetActive(true);
    }

    void OnDestroy()
    {
        foreach (RepeatingLayer layer in repeatingLayers)
            if (layer.root != null) Destroy(layer.root.gameObject);
    }
}
