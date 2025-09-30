using UnityEngine;

public class TakeDamage : MonoBehaviour
{
    [SerializeField] private float damage;
    

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            collision.gameObject.GetComponent<CharacterController2D>().ApplyDamage(damage, transform.position);
        }
    }
}
