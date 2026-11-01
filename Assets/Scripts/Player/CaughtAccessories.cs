using UnityEngine;

/// <summary>Accessories and body frames used exclusively after capture.</summary>
public class CaughtAccessories : ScriptableObject
{
    public Sprite hat;
    public bool hairOnly;
    public bool curlyHairOnly;
    public bool hatBakedIntoFrames;
    public Sprite glassesMask;
    public Sprite glassesOverride;
    public bool glassesFromFirstFrame;
    public Sprite fadingEyesMask;
    public Sprite[] caught = new Sprite[0];
    [System.NonSerialized] Sprite glasses;
    [System.NonSerialized] Sprite hair;
    [System.NonSerialized] Sprite[] bodies;
    [System.NonSerialized] System.Collections.Generic.List<Sprite> generated = new System.Collections.Generic.List<Sprite>();
    public Sprite Glasses { get { if (glassesOverride != null) return glassesOverride; Prepare(); return glasses; } }
    public Sprite[] Bodies { get { Prepare(); return bodies; } }

    public void ResetRuntimeCache()
    {
        if (generated != null)
        {
            foreach (var sprite in generated)
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
            generated.Clear();
        }
        glasses = null;
        hair = null;
        bodies = null;
    }
    public Sprite Hat
    {
        get
        {
            if ((!hairOnly && !curlyHairOnly) || hat == null) return hat;
            if (hair != null) return hair;
            var pixels = hat.texture.GetPixels(0, 0, 32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                int row = 31 - y;
                var color = pixels[y * 32 + x];
                // Rocker's upper streaks belong to the hair; the pale central
                // patch lower down is forehead, included in the supplied hat.
                bool highlight = row < 5 || (row == 5 && x <= 12);
                bool forehead = curlyHairOnly
                    ? row >= 9 && Mathf.Min(color.r, Mathf.Min(color.g, color.b)) > 190f / 255f
                    : !highlight && Mathf.Max(color.r, Mathf.Max(color.g, color.b)) > 90f / 255f;
                if (forehead)
                    pixels[y * 32 + x] = Color.clear;
            }
            hair = Make(pixels, "DetachedHair");
            return hair;
        }
    }

    void Prepare()
    {
        if (bodies != null) return;
        var mask = Mask(glassesMask);
        var fadingEyes = Mask(fadingEyesMask);
        var hatMask = hatBakedIntoFrames ? Mask(hat) : new bool[1024];
        var validFrames = new System.Collections.Generic.List<Sprite>();
        if (caught != null)
            foreach (var frame in caught)
                if (frame != null && frame.texture != null) validFrames.Add(frame);
        var prepared = new Sprite[validFrames.Count];
        for (int f = 0; f < validFrames.Count; f++)
        {
            // Raw 32px canvas preserves the shared pivot even for cropped source sprites.
            var pixels = validFrames[f].texture.GetPixels(0, 0, 32, 32);
            // Classic's face shifts down during caught. Isolate the glasses in
            // each frame rather than applying the final frame's offset to skin.
            var frameMask = glassesFromFirstFrame ? Mask(validFrames[f], true) : mask;
            if (f == 0 && glassesMask != null && glassesOverride == null)
            {
                // The final frame is already the isolated, correctly coloured
                // accessory. Sampling the first body frame at its position can
                // pick up white skin pixels when the face moves during caught.
                var piece = glassesFromFirstFrame ? new Color[1024] : glassesMask.texture.GetPixels(0, 0, 32, 32);
                if (glassesFromFirstFrame)
                    for (int i = 0; i < piece.Length; i++) if (frameMask[i]) piece[i] = pixels[i];
                glasses = Make(piece, "Glasses");
            }
            for (int i = 0; i < pixels.Length; i++)
            {
                if (frameMask[i] || hatMask[i]) pixels[i] = Color.clear;
                else if (fadingEyes[i]) pixels[i].a *= 1f - f / (float)Mathf.Max(1, validFrames.Count - 1);
            }
            prepared[f] = Make(pixels, "CaughtBody_" + f);
        }
        bodies = prepared;
    }

    static bool[] Mask(Sprite source, bool onlyDark = false)
    {
        var result = new bool[1024];
        if (source == null) return result;
        var pixels = source.texture.GetPixels(0, 0, 32, 32);
        // Fill each lens independently, keeping highlights attached to the glasses.
        for (int y = 0; y < 32; y++)
        for (int half = 0; half < 2; half++)
        {
            int first = 32, last = -1;
            for (int x = half * 16; x < half * 16 + 16; x++)
            {
                var pixel = pixels[y * 32 + x];
                if (pixel.a > 0f && (!onlyDark || Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) < 0.2f))
                { first = Mathf.Min(first, x); last = x; }
            }
            for (int x = first; x <= last; x++) result[y * 32 + x] = true;
        }
        return result;
    }

    Sprite Make(Color[] pixels, string name)
    {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, hideFlags = HideFlags.DontUnloadUnusedAsset };
        texture.SetPixels(pixels);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32f, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        if (generated == null) generated = new System.Collections.Generic.List<Sprite>();
        generated.Add(sprite);
        return sprite;
    }
}
