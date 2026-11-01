using System.Reflection;
using UnityEngine;

/// <summary>
/// Мерехтіння вогню: смолоскипи й свічки злегка міняють яскравість світла (Light2D на цьому ж об'єкті).
/// У 3D-вигляді це теж видно, бо RetroView бере яскравість із тих самих ламп.
/// </summary>
public class TorchFlicker : MonoBehaviour
{
    [Range(0f, 1f)] [SerializeField] float amount = 0.25f;
    [SerializeField] float speed = 8f;

    Component light2D;
    PropertyInfo intensityProp;
    System.Action<float> setIntensity;   // делегат замість PropertyInfo.SetValue: без сміття в пам'яті щокадру
    float baseIntensity;
    float seed;

    void Awake()
    {
        foreach (var c in GetComponents<Component>())
        {
            if (c != null && c.GetType().Name == "Light2D")
            {
                light2D = c;
                break;
            }
        }
        if (light2D == null) return;

        intensityProp = light2D.GetType().GetProperty("intensity");
        if (intensityProp == null || !intensityProp.CanWrite)
        {
            intensityProp = null;
            return;
        }
        baseIntensity = (float)intensityProp.GetValue(light2D);
        seed = Random.value * 100f;
        try
        {
            setIntensity = (System.Action<float>)System.Delegate.CreateDelegate(
                typeof(System.Action<float>), light2D, intensityProp.GetSetMethod());
        }
        catch (System.Exception)
        {
            var prop = intensityProp; var target = light2D;
            setIntensity = v => prop.SetValue(target, v);
        }
    }

    void Update()
    {
        if (setIntensity == null) return;
        float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
        setIntensity(baseIntensity * (1f + n * 2f * amount));
    }
}
