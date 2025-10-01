using UnityEngine;
using UnityEngine.SceneManagement;

public class BackMenu : MonoBehaviour
{
    public void Play()
    {
        SceneManager.LoadScene("Menu");
    }

    public void Playagian()
    {
        SceneManager.LoadScene("GameScene");
    }
}