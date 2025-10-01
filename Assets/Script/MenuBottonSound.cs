using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MenuButtonSound : MonoBehaviour
{
    [Header("Assign ���ҧ����ҧ˹�觾�")]
    [Tooltip("��ҡ�˹� ���� PlayOneShot(clip)")]
    public AudioClip clickClip;

    [Tooltip("��������ҧ ʤ�Ի��о����� GetComponent ����ͧ")]
    public AudioSource clickSound;

    [Range(0f, 1f)] public float volume = 1f;

    void Awake()
    {
        // ����ѧ������ҡ�� �����㹵���ͧ
        if (clickSound == null)
        {
            clickSound = GetComponent<AudioSource>();
        }

        // �ѹ��Ҵ: ����������������ͧ�͹�����
        if (clickSound != null) clickSound.playOnAwake = false;

        // �������շ�� clip ��¹͡��� clip � AudioSource �������͹
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

        // �� PlayOneShot ����դ�Ի�к���
        if (clickClip != null)
        {
            clickSound.PlayOneShot(clickClip, volume);
            return;
        }

        // ���������к� clickClip �� AudioSource �� clip ��������
        if (clickSound.clip != null)
        {
            clickSound.volume = volume;
            clickSound.Play();
            return;
        }

        Debug.LogWarning("[MenuButtonSound] No clip to play. Assign 'clickClip' or 'clickSound.clip'.", this);
    }
}

