using UnityEngine;

/// <summary>
/// Що нижче на екрані, те малюється поверх. Потрібно для вигляду зверху,
/// щоб скелет правильно заходив за полиці й виходив з-за них.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class YSort : MonoBehaviour
{
    [SerializeField] int offset;

    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    float lastY = float.NaN;

    void LateUpdate()
    {
        // більшість предметів стоїть на місці: порядок рахуємо лише коли об'єкт зрушив по вертикалі
        float y = transform.position.y;
        if (y == lastY) return;
        lastY = y;
        sr.sortingOrder = offset + Mathf.RoundToInt(-y * 10f);
    }
}
