using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Скіни привида. Animator завжди програє класичні кадри, а цей компонент щокадру (у LateUpdate,
/// тобто вже після Animator) підміняє їх відповідними кадрами вибраного скіна (GameSettings.Skin).
/// Кадри кожного скіна йдуть підряд у тому ж порядку, що й classicFrames:
/// idle 0-3, move 0-3, caught 0-5 (14 штук). Скін 0 = класичний.
/// Список скінів - у GhostSkinLibrary. Масиви заповнює GameBuilder; якщо вони порожні,
/// у редакторі кадри підвантажуються прямо з папок.
/// </summary>
[DefaultExecutionOrder(100)]
public class GhostSkin : MonoBehaviour
{
    public const int FramesPerSkin = 14;

    [SerializeField] Sprite[] classicFrames = new Sprite[0];
    [Tooltip("Усі скіни підряд, по 14 кадрів на скін")]
    [SerializeField] Sprite[] skinFrames = new Sprite[0];
    [SerializeField] string[] skinNames = new string[0];

    SpriteRenderer spriteRenderer;
    readonly Dictionary<Sprite, int> index = new Dictionary<Sprite, int>();
    int skin;
    GhostAnimator ghostAnimator;
    PlayerMovement movement;
    FirstPersonMovement firstPerson;
    string animationState = "appear";
    float animationTime;
    SpriteRenderer hatRenderer;
    GameObject droppedHat;
    bool hatDropped;
    public SpriteRenderer HatVisual => hatRenderer != null ? hatRenderer : droppedHat != null ? droppedHat.GetComponent<SpriteRenderer>() : null;
    public bool HatIsDropped => hatDropped;
    public Vector3 HatDropOrigin { get; private set; }
    SpriteRenderer glassesRenderer;
    GameObject droppedGlasses;
    bool glassesDropped;
    public SpriteRenderer GlassesVisual => glassesRenderer != null ? glassesRenderer : droppedGlasses != null ? droppedGlasses.GetComponent<SpriteRenderer>() : null;
    public bool GlassesAreDropped => glassesDropped;
    public Vector3 GlassesDropOrigin { get; private set; }

    /// <summary>Скін привида в поточній сцені (для інтерфейсу).</summary>
    public static GhostSkin Current { get; private set; }

    public int SkinCount => skinFrames.Length / FramesPerSkin;
    public int CurrentSkin => skin;

    /// <summary>Перший кадр вибраного скіна (іконка в інтерфейсі).</summary>
    public Sprite Portrait => Frame(skin, 0);

    /// <summary>Кадри польоту вибраного скіна (для екрана переходу між рівнями).</summary>
    public Sprite[] MoveFrames => GhostSkinLibrary.MoveFrames(skinFrames, skin);

    void Awake()
    {
        Current = this;
        spriteRenderer = GetComponent<SpriteRenderer>();
        ghostAnimator = GetComponent<GhostAnimator>();
        movement = GetComponent<PlayerMovement>();
        firstPerson = GetComponent<FirstPersonMovement>();

#if UNITY_EDITOR
        if (skinFrames.Length < FramesPerSkin || classicFrames.Length == 0)
        {
            skinFrames = GhostSkinLibrary.LoadAllInEditor();
            classicFrames = new Sprite[FramesPerSkin];
            System.Array.Copy(skinFrames, classicFrames, FramesPerSkin);
            skinNames = GhostSkinLibrary.Names;
        }
#endif

        for (int i = 0; i < classicFrames.Length; i++)
            if (classicFrames[i] != null && !index.ContainsKey(classicFrames[i])) index[classicFrames[i]] = i;

        // У 3D камера нічого не малює, тож Animator не має "засинати", коли привида не видно
        var animator = GetComponent<Animator>();
        if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        SetSkin(GameSettings.Skin);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    public void SetSkin(int s)
    {
        if (droppedHat != null) Destroy(droppedHat);
        if (droppedGlasses != null) Destroy(droppedGlasses);
        hatDropped = false;
        glassesDropped = false;
        if (glassesRenderer != null) glassesRenderer.enabled = false;
        skin = SkinCount > 0 ? Mathf.Clamp(s, 0, SkinCount - 1) : 0;
        animationState = "appear";
        animationTime = 0f;
        Apply();
        if (hatRenderer != null) hatRenderer.enabled = false;
        if (hatRenderer != null) hatRenderer.sprite = GhostSkinLibrary.HatFrame(skin);
        if (glassesRenderer != null) glassesRenderer.sprite = GhostSkinLibrary.GlassesFrame(skin);
    }

    Sprite Frame(int s, int i)
    {
        int k = s * FramesPerSkin + i;
        if (k >= 0 && k < skinFrames.Length && skinFrames[k] != null) return skinFrames[k];
        return i < classicFrames.Length ? classicFrames[i] : null;
    }

    void LateUpdate()
    {
        if (ghostAnimator != null && ghostAnimator.IsCaught && !GhostSkinLibrary.HasExtendedAnimations(skin)
            && GhostSkinLibrary.HasCaughtAccessories(skin) && spriteRenderer != null)
        {
            if (animationState != "dissolve") { animationState = "dissolve"; animationTime = 0f; }
            UpdateCaughtAccessories();
            var body = GhostSkinLibrary.CaughtFrame(skin, animationTime);
            if (body != null) spriteRenderer.sprite = body;
            UpdateHat();
            UpdateGlasses();
            animationTime += Time.deltaTime;
            return;
        }
        if (GhostSkinLibrary.HasExtendedAnimations(skin) && spriteRenderer != null)
        {
            UpdateExtendedAnimation();
            return;
        }
        Apply();
    }

    void UpdateExtendedAnimation()
    {
        bool caught = ghostAnimator != null && ghostAnimator.IsCaught;
        bool flying = (movement != null && movement.enabled && movement.IsMoving)
            || (firstPerson != null && firstPerson.enabled && firstPerson.IsMoving);
        float appearDuration = GhostSkinLibrary.AnimationFrames(skin, "appear").Length / GhostSkinLibrary.AnimationFps;
        string next;
        if (caught) next = "dissolve";
        else if (animationState == "appear" && animationTime < appearDuration) next = "appear";
        // Світло змінює лише прозорість через GhostVisibility, як у решти скінів.
        else next = flying ? "move" : "idle";
        if (next != animationState) { animationState = next; animationTime = 0f; }
        if (caught) UpdateCaughtAccessories();
        var frame = caught ? GhostSkinLibrary.CaughtFrame(skin, animationTime) : null;
        if (frame == null) frame = GhostSkinLibrary.AnimatedFrame(skin, animationState, animationTime,
            animationState != "appear" && animationState != "dissolve");
        if (frame != null) spriteRenderer.sprite = frame;
        UpdateHat();
        UpdateGlasses();
        animationTime += Time.deltaTime;
    }

    void EnsureGlasses()
    {
        if (glassesDropped || glassesRenderer != null || GhostSkinLibrary.GlassesFrame(skin) == null) return;
        var child = new GameObject("Glasses");
        child.transform.SetParent(transform, false);
        glassesRenderer = child.AddComponent<SpriteRenderer>();
        glassesRenderer.sprite = GhostSkinLibrary.GlassesFrame(skin);
        glassesRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
        UpdateGlasses();
    }

    void UpdateCaughtAccessories()
    {
        if (GhostSkinLibrary.HatFrame(skin) != null)
        {
            EnsureHat();
            if (animationTime >= 1f / GhostSkinLibrary.AnimationFps) DropHat();
        }
        EnsureGlasses();
        if (animationTime >= 2f / GhostSkinLibrary.AnimationFps) DropGlasses();
    }

    void UpdateGlasses()
    {
        if (glassesRenderer == null || glassesDropped || spriteRenderer == null) return;
        glassesRenderer.enabled = animationState == "dissolve" && GhostSkinLibrary.GlassesFrame(skin) != null;
        glassesRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        glassesRenderer.sortingOrder = spriteRenderer.sortingOrder + 2;
        glassesRenderer.color = spriteRenderer.color;
        glassesRenderer.flipX = spriteRenderer.flipX;
        glassesRenderer.flipY = spriteRenderer.flipY;
    }

    public void DropGlasses()
    {
        if (glassesDropped || spriteRenderer == null || GhostSkinLibrary.GlassesFrame(skin) == null) return;
        EnsureGlasses();
        glassesDropped = true;
        droppedGlasses = glassesRenderer.gameObject;
        GlassesDropOrigin = droppedGlasses.transform.position;
        droppedGlasses.name = "DroppedGlasses";
        droppedGlasses.transform.SetParent(null, true);
        glassesRenderer.enabled = true;
        glassesRenderer.color = Color.white;
        droppedGlasses.AddComponent<DroppedGhostHat>().Throw(glassesRenderer);
        glassesRenderer = null;
    }

    void EnsureHat()
    {
        if (hatDropped || hatRenderer != null) return;
        var existing = transform.Find("Hat");
        var child = existing != null ? existing.gameObject : new GameObject("Hat");
        child.transform.SetParent(transform, false);
        hatRenderer = child.GetComponent<SpriteRenderer>();
        if (hatRenderer == null) hatRenderer = child.AddComponent<SpriteRenderer>();
        hatRenderer.sprite = GhostSkinLibrary.HatFrame(skin);
        hatRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
        UpdateHat();
    }

    void UpdateHat()
    {
        if (hatRenderer == null || spriteRenderer == null || hatDropped) return;
        hatRenderer.enabled = animationState == "dissolve" && GhostSkinLibrary.HatFrame(skin) != null;
        hatRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        hatRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        hatRenderer.color = spriteRenderer.color;
        hatRenderer.flipX = spriteRenderer.flipX;
        hatRenderer.flipY = spriteRenderer.flipY;
    }

    public void DropHat()
    {
        if (hatDropped || GhostSkinLibrary.HatFrame(skin) == null || spriteRenderer == null) return;
        EnsureHat();
        hatDropped = true;
        droppedHat = hatRenderer.gameObject;
        HatDropOrigin = droppedHat.transform.position;
        droppedHat.name = "DroppedGhostHat";
        droppedHat.transform.SetParent(null, true);
        hatRenderer.enabled = true;
        hatRenderer.color = Color.white;
        droppedHat.AddComponent<DroppedGhostHat>().Throw(hatRenderer);
        hatRenderer = null;
    }

    void Apply()
    {
        if (skin == 0 || spriteRenderer == null || spriteRenderer.sprite == null) return;
        if (index.TryGetValue(spriteRenderer.sprite, out int i))
        {
            var s = Frame(skin, i);
            if (s != null) spriteRenderer.sprite = s;
        }
    }

    // Якщо на привиді зі старого префаба немає GhostSkin - додати автоматично.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachMissing();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachMissing();

    static void AttachMissing()
    {
        foreach (var ghost in FindObjectsByType<GhostAnimator>(FindObjectsSortMode.None))
            if (ghost.GetComponent<GhostSkin>() == null) ghost.gameObject.AddComponent<GhostSkin>();
    }
}
