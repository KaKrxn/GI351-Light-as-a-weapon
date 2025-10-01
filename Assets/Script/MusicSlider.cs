using UnityEngine;
using UnityEngine.UI;

public class MusicSlider : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource; // �ҡ BGM AudioSource �����
    [SerializeField] private Slider slider;           // �ҡ Slider �����

    private void Start()
    {
        // ��Ŵ��ҷ����૿���
        float vol = PlayerPrefs.GetFloat("bgm_volume", 0.8f);
        slider.value = vol;
        musicSource.volume = vol;

        // �١ event ���Ң�Ѻ slider
        slider.onValueChanged.AddListener(SetVolume);
    }

    private void OnDestroy()
    {
        slider.onValueChanged.RemoveListener(SetVolume);
    }

    public void SetVolume(float value)
    {
        musicSource.volume = value;              // 0 �֧ 1
        PlayerPrefs.SetFloat("bgm_volume", value); // ૿�����������˹��
    }
}