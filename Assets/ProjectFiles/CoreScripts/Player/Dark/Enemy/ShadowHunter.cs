using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ShadowHunter : MonoBehaviour
{
    [Header("Target / Player")]
    public Transform target;                
    public PlayerLight playerLight;         
    public PlayerHealth playerHealth;       

    [Header("Movement")]
    public float chaseSpeed = 2.5f;         
    public float retreatSpeed = 3.5f;       
    public float keepDistance = 0.6f;      

    [Header("Combat")]
    public float damagePerSecond = 10f;     
    public float fearMargin = 0.15f;        

    [Header("FX")]
    public SpriteRenderer body;             
    public float minAlpha = 0.25f, maxAlpha = 0.85f;

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!body) body = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (!target || !playerLight) return;

        float distToPlayer = Vector2.Distance(transform.position, target.position);
        float lightR = playerLight.CurrentRadius;

        // อยู่ “ในแสง” ถ้าเข้าใกล้กว่า (รัศมีแสง - margin)
        bool inLight = distToPlayer <= (lightR - fearMargin);

        Vector2 dir = (target.position - transform.position).normalized;

        if (!inLight)
        {
            // chase
            Vector2 desired = dir * chaseSpeed;
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desired, 0.2f);
            SetAlpha(Mathf.Lerp(GetAlpha(), maxAlpha, 0.1f));
        }
        else
        {
            // fall back
            Vector2 desired = -dir * retreatSpeed;
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desired, 0.25f);
            SetAlpha(Mathf.Lerp(GetAlpha(), minAlpha, 0.15f));
        }

        
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
        }

        //take damage
        if (playerHealth && !inLight && distToPlayer <= keepDistance + 0.25f)
        {
            playerHealth.TakeDamage(damagePerSecond);
        }
    }

    float GetAlpha() => body ? body.color.a : 1f;
    void SetAlpha(float a)
    {
        if (!body) return;
        var c = body.color; c.a = Mathf.Clamp01(a); body.color = c;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(transform.position, keepDistance);
    }
}
