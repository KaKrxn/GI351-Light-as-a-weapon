using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class DarknessWave : MonoBehaviour
{
    [Header("Movement")]
    public float pushSpeed = 1.2f;                 
    [Tooltip("..)")]
    public Vector2 manualDirection = Vector2.right;
    [Tooltip("moveDirection Transform ")]
    public Transform moveDirectionRef;

    [Header("Damage")]
    public PlayerLight playerLight;
    public PlayerHealth playerHealth;
    public float damagePerSecond = 15f;

    Rigidbody2D rb;
    Vector2 dir;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        //rb.isKinematic = true; 
    }

    void OnEnable() { ComputeDir(); }

    void ComputeDir()
    {
        // ใช้ทิศจาก Reference ถ้า valid
        if (moveDirectionRef)
        {
            Vector2 d = (Vector2)moveDirectionRef.position - (Vector2)transform.position;
            if (d.sqrMagnitude > 0.0001f) { dir = d.normalized; return; }
        }
        // Fallback: ใช้ manualDirection หรือ +X
        dir = (manualDirection.sqrMagnitude > 0.0001f) ? manualDirection.normalized : Vector2.right;
    }

    void FixedUpdate()
    {
        
        if (moveDirectionRef)
        {
            Vector2 d = (Vector2)moveDirectionRef.position - rb.position;
            if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
        }

        rb.MovePosition(rb.position + dir * pushSpeed * Time.fixedDeltaTime);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!playerLight || !playerHealth) return;
        if (!other.CompareTag("Player")) return;

        float dist = Vector2.Distance(other.transform.position, playerLight.transform.position);
        bool inLight = dist <= (playerLight.CurrentRadius - 0.1f);
        if (!inLight) playerHealth.TakeDamage(damagePerSecond);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Vector2 gdir = Vector2.right;
        if (moveDirectionRef)
            gdir = ((Vector2)moveDirectionRef.position - (Vector2)transform.position);
        else if (manualDirection.sqrMagnitude > 0.0001f)
            gdir = manualDirection;

        if (gdir.sqrMagnitude > 0.0001f)
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)(gdir.normalized * 3f));
    }
}
