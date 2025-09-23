using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class JointReleaseOnDestroy2D : MonoBehaviour
{
    [Header("Target (optional)")]
    [Tooltip("ถ้าเว้นว่างจะใช้ Rigidbody2D บน GameObject เดียวกัน")]
    public Rigidbody2D targetRb;

    [Header("Behavior")]
    [Tooltip("ทำลายจอยน์เลย (true) หรือแค่ปิด (false)")]
    public bool destroyJoints = true;

    [Tooltip("ให้ทำงานตอนถูก Disable ด้วย (SetActive(false))")]
    public bool alsoRunOnDisable = true;

    [Header("Safety Tweaks (optional)")]
    [Tooltip("ปลด Freeze Y / เปิดแรงโน้มถ่วงให้ฝั่งที่เคยเชื่อมกับเรา (ถ้าจำเป็น)")]
    public bool nudgeConnectedBodiesPhysics = true;

    [Header("Robust Mode")]
    [Tooltip("เฝ้าดูต่อเนื่องและแคชรายการจอยน์ที่เชื่อมกับเรา (กันเคสทำลายเฉพาะ Rigidbody2D)")]
    public bool watchContinuously = true;

    [Tooltip("ความถี่ในการรีเฟรชรายการจอยน์ (วินาที)")]
    public float rescanInterval = 0.25f;

    // ===== runtime =====
    private readonly HashSet<Joint2D> trackedJoints = new HashSet<Joint2D>();
    private Coroutine watchCo;

    void Reset()
    {
        targetRb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (!targetRb) targetRb = GetComponent<Rigidbody2D>();
        if (watchContinuously) watchCo = StartCoroutine(WatchLoop());
    }

    void OnDisable()
    {
        if (watchCo != null) { StopCoroutine(watchCo); watchCo = null; }
        if (alsoRunOnDisable) ReleaseTracked(); // ปล่อยตอนโดนปิดใช้งานด้วย
    }

    void OnDestroy()
    {
        // กรณี Destroy ทั้ง GameObject → ปล่อยก่อนหาย
        ReleaseTracked();
    }

    /// <summary>เรียกเองก่อน Destroy ถ้าอยากชัวร์</summary>
    public void ReleaseNow()
    {
        RefreshTracked(); // อัปเดตรายการเผื่อเพิ่งเชื่อม
        ReleaseTracked();
    }

    IEnumerator WatchLoop()
    {
        // โหมดเฝ้าดู: แคชรายชื่อจอยน์ และคอยเช็กว่า targetRb ยังอยู่ไหม
        while (true)
        {
            // ถ้า targetRb ถูก Destroy (หรือโดนถอดออก) → ปล่อยจอยน์ที่แคชไว้แล้วจบ
            if (!targetRb)
            {
                ReleaseTracked();
                yield break;
            }

            RefreshTracked();
            yield return new WaitForSeconds(rescanInterval);
        }
    }

    void RefreshTracked()
    {
        if (!targetRb) return;

        // เก็บ Joint2D ที่เชื่อมกับเรา (connectedBody == targetRb)
        var all = Object.FindObjectsOfType<Joint2D>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var j = all[i];
            if (!j) continue;
            if (j.connectedBody == targetRb)
                trackedJoints.Add(j);
        }

        // ล้างตัวที่ถูกลบออกไปแล้ว
        trackedJoints.RemoveWhere(j => j == null);
    }

    void ReleaseTracked()
    {
        if (trackedJoints.Count == 0) return;

        foreach (var j in trackedJoints)
        {
            if (!j) continue;

            var otherRb = j.attachedRigidbody; // ฝั่งหิน/ของหนัก

            if (destroyJoints) Destroy(j);
            else j.enabled = false;

            if (nudgeConnectedBodiesPhysics && otherRb)
            {
                if (otherRb.bodyType != RigidbodyType2D.Dynamic)
                    otherRb.bodyType = RigidbodyType2D.Dynamic;
                if (otherRb.gravityScale <= 0f)
                    otherRb.gravityScale = 1f;
                otherRb.constraints &= ~RigidbodyConstraints2D.FreezePositionY;
                otherRb.WakeUp();
            }
        }

        trackedJoints.Clear();
    }
}
