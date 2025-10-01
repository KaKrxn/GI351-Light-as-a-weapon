using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("Game/UI/Scene Load Buttons")]
public class SceneLoadButtons : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("�ҡ������Ŵ����͡����� Play Again (��ͧ����� Build Settings)")]
    public string playAgainSceneName = "Level_1";

    [Tooltip("�ҡ������ѡ (��ͧ����� Build Settings)")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Load Options")]
    public LoadSceneMode loadMode = LoadSceneMode.Single;
    [Tooltip("˹�ǧ��͹��Ŵ (������� SFX/͹����ѹ)")]
    public float loadDelay = 0f;
    [Tooltip("�ѹ�����/�Ѻ��Ť�ԡ�����ҧ���ѧ��Ŵ")]
    public bool preventDoubleClick = true;

    [Header("SFX (optional)")]
    public AudioSource sfxSource;
    public AudioClip clickSfx;

    bool isLoading;

    // ���¡�ҡ���� Play Again
    public void PlayAgain()
    {
        if (preventDoubleClick && isLoading) return;
        if (string.IsNullOrEmpty(playAgainSceneName))
        {
            Debug.LogError("[SceneLoadButtons] playAgainSceneName ��ҧ");
            return;
        }
        StartCoroutine(Co_Load(playAgainSceneName));
    }

    // ���¡�ҡ���� Back to Menu
    public void BackToMenu()
    {
        if (preventDoubleClick && isLoading) return;
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("[SceneLoadButtons] mainMenuSceneName ��ҧ");
            return;
        }
        StartCoroutine(Co_Load(mainMenuSceneName));
    }

    IEnumerator Co_Load(string sceneName)
    {
        isLoading = true;

        if (sfxSource && clickSfx)
            sfxSource.PlayOneShot(clickSfx);

        if (loadDelay > 0f)
            yield return new WaitForSeconds(loadDelay);

        SceneManager.LoadScene(sceneName, loadMode);
    }
}
