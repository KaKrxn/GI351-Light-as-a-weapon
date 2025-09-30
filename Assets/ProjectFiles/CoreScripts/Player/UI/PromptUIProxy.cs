using UnityEngine;

/// <summary>
/// ใช้เป็น 'promptUI' แทน GameObject UI เดิม:
/// - เมื่อ GameObject นี้ถูก SetActive(true) → โชว์ HUD กลาง
/// - เมื่อ SetActive(false) → ซ่อน HUD กลาง
/// ไม่ต้องแก้สคริปต์ Lever/Pushable ที่มี SetActive อยู่แล้ว
/// </summary>
public class PromptUIProxy : MonoBehaviour
{
    [TextArea]
    public string message = "[E] Interact";
    public Transform followTarget;     // ถ้าเว้นว่าง จะใช้พาเรนต์
    public Vector3 extraOffset;

    void OnEnable()
    {
        var hud = SharedInteractPromptHUD.Instance;
        if (!hud) return;

        var follow = followTarget ? followTarget : transform.parent ? transform.parent : transform;
        hud.Show(this, message, follow);
        hud.worldOffset += extraOffset;
    }

    void OnDisable()
    {
        var hud = SharedInteractPromptHUD.Instance;
        if (!hud) return;
        hud.Hide(this);
    }
}
