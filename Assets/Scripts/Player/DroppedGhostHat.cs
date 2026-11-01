using UnityEngine;

/// <summary>Cosmetic throw in the top-down plane, with no gameplay collider.</summary>
public class DroppedGhostHat : MonoBehaviour
{
    Rigidbody2D body;
    SpriteRenderer visual;
    float floorY;
    bool landed;

    public void Throw(SpriteRenderer renderer)
    {
        visual = renderer;
        floorY = transform.position.y - 0.65f * Mathf.Abs(transform.lossyScale.y);
        body = gameObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 1.2f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.AddForce(new Vector2(Random.value < 0.5f ? -0.6f : 0.6f, 2.5f), ForceMode2D.Impulse);
        body.angularVelocity = Random.value < 0.5f ? -85f : 85f;
        Destroy(gameObject, 5f);
    }

    void FixedUpdate()
    {
        if (body == null || landed || body.linearVelocity.y > 0f || body.position.y > floorY) return;
        body.position = new Vector2(body.position.x, floorY);
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.gravityScale = 0f;
        body.bodyType = RigidbodyType2D.Kinematic;
        landed = true;
        if (visual != null) visual.sortingOrder = Mathf.RoundToInt(-floorY * 10f) + 6;
    }
}
