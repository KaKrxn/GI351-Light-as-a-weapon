using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Pushable2D : MonoBehaviour
{
    public PhysicsMaterial2D idleMaterial;    // เสียดทานสูง (ไม่ไหลเอง)
    public PhysicsMaterial2D grabbedMaterial; // เสียดทานต่ำ (ลากลื่น)
    public bool freezeRotationWhenGrabbed = true;

    Rigidbody2D rb;
    Collider2D col;
    RigidbodyConstraints2D originalConstraints;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        originalConstraints = rb.constraints;

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public void OnGrab()
    {
        if (grabbedMaterial) col.sharedMaterial = grabbedMaterial;
        if (freezeRotationWhenGrabbed)
            rb.constraints = originalConstraints | RigidbodyConstraints2D.FreezeRotation;
    }

    public void OnRelease()
    {
        if (idleMaterial) col.sharedMaterial = idleMaterial;
        rb.constraints = originalConstraints;
    }
}
