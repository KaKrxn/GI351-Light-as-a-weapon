using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MenuButtonSound : MonoBehaviour
{
    [Header("Assign อย่างใดอย่างหนึ่งพอ")]
    [Tooltip("ถ้ากำหนด จะใช้ PlayOneShot(clip)")]
    public AudioClip clickClip;

    [Tooltip("ถ้าเว้นว่าง สคริปต์จะพยายาม GetComponent ให้เอง")]
    public AudioSource clickSound;

    [Range(0f, 1f)] public float volume = 1f;

    void Awake()
    {
        // ถ้ายังไม่ได้ลากมา ให้หาในตัวเอง
        if (clickSound == null)
        {
            clickSound = GetComponent<AudioSource>();
        }

        // กันพลาด: ให้แน่ใจว่าไม่เล่นเองตอนเริ่ม
        if (clickSound != null) clickSound.playOnAwake = false;

        // ถ้าไม่มีทั้ง clip ภายนอกและ clip ใน AudioSource ให้แจ้งเตือน
        if (clickClip == null && (clickSound == null || clickSound.clip == null))
        {
            Debug.LogWarning("[MenuButtonSound] No clip assigned. " +
                             "Set 'clickClip' or assign an AudioSource with a clip.", this);
        }
    }

    public void PlayClickSound()
    {
        if (clickSound == null)
        {
            Debug.LogError("[MenuButtonSound] AudioSource (clickSound) is NULL. " +
                           "Add/Assign an AudioSource.", this);
            return;
        }

        // ใช้ PlayOneShot ถ้ามีคลิประบุมา
        if (clickClip != null)
        {
            clickSound.PlayOneShot(clickClip, volume);
            return;
        }

        // ถ้าไม่ได้ระบุ clickClip แต่ AudioSource มี clip อยู่แล้ว
        if (clickSound.clip != null)
        {
            clickSound.volume = volume;
            clickSound.Play();
            return;
        }

        Debug.LogWarning("[MenuButtonSound] No clip to play. Assign 'clickClip' or 'clickSound.clip'.", this);
    }
}

