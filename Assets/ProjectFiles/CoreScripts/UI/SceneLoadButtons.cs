using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("Game/UI/Scene Load Buttons")]
public class SceneLoadButtons : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("ฉากที่จะโหลดเมื่อกดปุ่ม Play Again (ต้องอยู่ใน Build Settings)")]
    public string playAgainSceneName = "Level_1";

    [Tooltip("ฉากเมนูหลัก (ต้องอยู่ใน Build Settings)")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Load Options")]
    public LoadSceneMode loadMode = LoadSceneMode.Single;
    [Tooltip("หน่วงก่อนโหลด (เผื่อเล่น SFX/อนิเมชัน)")]
    public float loadDelay = 0f;
    [Tooltip("กันกดซ้ำ/ดับเบิลคลิกระหว่างกำลังโหลด")]
    public bool preventDoubleClick = true;

    [Header("SFX (optional)")]
    public AudioSource sfxSource;
    public AudioClip clickSfx;

    bool isLoading;

    // เรียกจากปุ่ม Play Again
    public void PlayAgain()
    {
        if (preventDoubleClick && isLoading) return;
        if (string.IsNullOrEmpty(playAgainSceneName))
        {
            Debug.LogError("[SceneLoadButtons] playAgainSceneName ว่าง");
            return;
        }
        StartCoroutine(Co_Load(playAgainSceneName));
    }

    // เรียกจากปุ่ม Back to Menu
    public void BackToMenu()
    {
        if (preventDoubleClick && isLoading) return;
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("[SceneLoadButtons] mainMenuSceneName ว่าง");
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
