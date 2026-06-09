using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenu : MonoBehaviour
{
    public Material weathersMaterial;
    public GameObject networkManager;
    public string loadScene;

    private void Awake()
    {
        weathersMaterial.SetInt("_ID", 0);
    }

    //Start server, then client immediately after
    public void Host()
    {
        StartCoroutine(Load(loadScene));
    }

    //Client
    public void Join()
    {
        StartCoroutine(Load(loadScene));
    }

    private IEnumerator Load(string scene)
    {
        //DontDestroyOnLoad(networkManager);
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scene);
        while (!asyncLoad.isDone) {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            Debug.Log(progress);
            yield return null;
        }
    }
}