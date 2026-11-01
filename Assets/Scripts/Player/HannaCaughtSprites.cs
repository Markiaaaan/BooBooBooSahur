using UnityEngine;

/// <summary>Build-safe references to Stylyaha's detached hat and hatless caught frames.</summary>
public class HannaCaughtSprites : ScriptableObject
{
    public Sprite hat;
    public Sprite[] caught = new Sprite[0];
    Sprite glasses;
    Sprite[] bareFrames;

    public Sprite Glasses { get { PrepareGlasses(); return glasses; } }
    public Sprite[] WithoutGlasses { get { PrepareGlasses(); return bareFrames ?? caught; } }

    // Preserve the supplied art on disk. Separate the glasses from the caught
    // frames at runtime, including their white highlights, so they cannot remain
    // floating on the dissolving body after the accessory has fallen away.
    void PrepareGlasses()
    {
        if (bareFrames != null || caught.Length != 6 || caught[0] == null) return;
        var reference = caught[0];
        var glassesPixels = new Color[32 * 32];
        bareFrames = new Sprite[caught.Length];
        for (int frame = 0; frame < caught.Length; frame++)
        {
            var source = caught[frame];
            var rect = source.rect;
            var pixels = source.texture.GetPixels((int)rect.x, (int)rect.y, 32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                int row = 31 - y;
                bool lens = row == 12 ? x == 7 || x == 22
                    : row == 13 ? (x >= 7 && x <= 13) || (x >= 16 && x <= 22)
                    : row == 14 ? x >= 7 && x <= 22
                    : row == 15 || row == 16 ? (x >= 8 && x <= 13) || (x >= 16 && x <= 21)
                    : row == 17 && ((x >= 9 && x <= 12) || (x >= 17 && x <= 20));
                if (!lens) continue;
                int i = y * 32 + x;
                if (frame == 0) glassesPixels[i] = pixels[i];
                pixels[i] = Color.clear;
            }
            bareFrames[frame] = MakeSprite(pixels, source, "_without_glasses");
        }
        glasses = MakeSprite(glassesPixels, reference, "_glasses");
    }

    static Sprite MakeSprite(Color[] pixels, Sprite source, string suffix)
    {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        texture.SetPixels(pixels);
        texture.Apply();
        var result = Sprite.Create(texture, new Rect(0, 0, 32, 32), source.pivot / 32f,
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        result.name = source.name + suffix;
        result.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return result;
    }
}
