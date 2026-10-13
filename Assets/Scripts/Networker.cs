using UnityEngine;
using TMPro;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Transporting;

public class Networker : NetworkBehaviour
{
    //The manager for netowkorking
    [Tooltip("The current seed.")]
    public int seed;
    [Tooltip("Is in a game?")]
    public bool roundStarted = false;
    [Tooltip("Dungeons available to be played.")]
    public List<Dungeon> dungeons = new List<Dungeon>();
    private byte dungeonIndex = 0;
    [Tooltip("Time elapsed from the day.")]
    public float gameTime = 0;
    public TextMeshProUGUI timer;
    public Transform daylight;
    public Manager manager;

    /*
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!base.IsOwner) this.enabled = false;
    }
    */

    private void Update()
    {
        if (roundStarted) {
            gameTime += Time.deltaTime;
            float tim = gameTime * 1.25f;
            timer.text = $"{string.Format("{0:00}", (Mathf.Floor(tim / 60) + 9) % 13 + Mathf.Floor(tim / 780))}:{string.Format("{0:00}", tim % 60)}\n({string.Format("{0:000.0}", gameTime)})";
            float ang = 0.027777778f * tim * (Mathf.Pow((9 - tim * 0.016666667f) / 9, 2) + 0.95f);
            daylight.eulerAngles = Vector3.right * Mathf.Clamp(ang * 10 - 45, -45, 200);
        }
    }

    #region Colored Debug Message
    public void ColorDebug(string msg, string hex)
    {
        if (ColorUtility.TryParseHtmlString($"#{hex}", out _)) Debug.Log($"<color=#{hex}>{msg}</color>");
    }

    public void ColorDebug(string msg, Color col)
    {
        Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(col)}>{msg}</color>");
    }
    #endregion

    [ServerRpc]
    public void ShareSeedServer(int seed, float currentTime)
    {
        ShareSeedClient(seed, currentTime);
    }

    [ObserversRpc]
    public void ShareSeedClient(int seed, float currentTime)
    {
        ColorDebug($"Seed: {seed}", "995512");
        manager.NextRound(seed, currentTime);
    }
}