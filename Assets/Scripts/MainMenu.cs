using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public Material weathersMaterial;

    private void Awake()
    {
        weathersMaterial.EnableKeyword("_WEATHER_NONE");
    }

    public void Singleplayer()
    {
        Debug.Log("Singleplayer");
    }

    public void Multiplayer()
    {
        Debug.Log("Connect to server");
    }
}