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
    }

    void Update()
    {
        if (intensityProp == null) return;
        float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
        intensityProp.SetValue(light2D, baseIntensity * (1f + n * 2f * amount));
    }
}
