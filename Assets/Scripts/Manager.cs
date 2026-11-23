using FishNet.Managing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Manager : MonoBehaviour
{
    [Tooltip("Is a dungeon being generated?")]
    public bool generating = false;
    [Tooltip("Dungeon generation script object.")]
    public DungeonGeneration generation;
    [Tooltip("Fog material to be updated.")]
    public Material fogMaterial;
    [Tooltip("Grass materials, with 0 being grass in the hub, and 1 being set from the dungeon.")]
    public Material[] grassMaterials = new Material[2];
    [HideInInspector]
    public PlayerController player;
    [Tooltip("The server manager.")]
    public Networker manager;
    [Tooltip("Network manager for joining.")]
    public NetworkManager network;
    private LocalConnectionState _serverState = LocalConnectionState.Stopped;
    private LocalConnectionState _clientState = LocalConnectionState.Stopped;
    [Tooltip("Main Menu UI")]
    public GameObject MainUI;
    [Tooltip("Game Menu UI")]
    public GameObject GameUI;

    [Header("Interaction Objects")]
    [Tooltip("Dungeon selection canvas.")]
    public GameObject dungeonSelectObject;
    [Tooltip("Dungeon text display.")]
    public TextMeshProUGUI dungeonInfo;
    [Tooltip("Player color image.")]
    public GameObject colorPickerObject;
    [Tooltip("Player's current color selected.")]
    public RawImage colorDisplay;
    private Color colorPicked;

    private void Start()
    {
        if (network != null) {
            network.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            network.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
        }
    }

    private void Update()
    {
        if (player == null) return;

        //Once dungeon is generated, update clientside visuals & notify networker, which will start game once all players have generated
        if (generating) {
            if (generation.dungeonGenerated) {
                generating = false;
                SetFog(generation.dungeon.atmospheres[generation.atmoID].fog);
                player.SetSnowDepth(generation.snowRayLength);
                player.Weather(generation.dungeon.atmospheres[generation.atmoID].weather);
            }
        }

        //Color picker
        if (colorPickerObject.activeSelf) {
            if (Physics.Raycast(player.head.position, player.head.forward, out RaycastHit picker, 5, 32)) {
                Texture2D tex = picker.transform.GetComponent<Renderer>().material.mainTexture as Texture2D;
                colorPicked = tex.GetPixelBilinear(picker.textureCoord.x, picker.textureCoord.y);
                colorDisplay.color = colorPicked;
            }
        }
    }

    private void OnDestroy()
    {
        if (network == null) return;

        network.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
        network.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
    }

    public void CloseApp()
    {
        Application.Quit();
    }

    #region Server Joining
    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
    {
        _clientState = obj.ConnectionState;
    }

    private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
    {
        _serverState = obj.ConnectionState;
    }

    public void StartServer(bool stop = false)
    {
        if (network == null) return;
        if (stop) {
            StartClient(stop);
            network.ServerManager.StopConnection(true);
        } else {
            network.ServerManager.StartConnection();
            StartClient(stop);
        }
    }

    public void StartClient(bool stop = false)
    {
        if (network == null) return;
        if (stop) network.ClientManager.StopConnection();
        else network.ClientManager.StartConnection();
        HideMainUI(stop);
    }

    public void HideMainUI(bool hide)
    {
        MainUI.SetActive(hide);
        GameUI.SetActive(!hide);
    }
    #endregion

    [ContextMenu("Generate")]
    public void ContextGenerate()
    {
        NextRound((int)Random.Range(-2147483648, 2147483647f));
    }

    public void NextRound(int seed, float time = 0)
    {
        if (generating) return;
        generating = true;
        generation.dungeon = manager.dungeons[manager.dungeonIndex];
        generation.seed = seed;
        generation.Routine();
        grassMaterials[1] = generation.GetGrassMat();
        SetGrass();
    }

    //Delete that shit
    public void Clear()
    {
        Debug.LogError($"Manager wiped");
        generation.ClearDungeon();
        generating = false;
    }

    //Change the fog settings
    public void SetFog(FogSettings settings)
    {
        fogMaterial.SetColor("_Colour", settings.Color);
        fogMaterial.SetFloat("_Density", settings.Density);
        fogMaterial.SetFloat("_Raymarch_Distance", settings.Distance);
        fogMaterial.SetFloat("_Raymarch_Distance_Bias", settings.Blend);
    }

    //Reset fog settings
    public void ClearFog()
    {
        fogMaterial.SetColor("_Colour", Color.clear);
        fogMaterial.SetFloat("_Density", 1);
        fogMaterial.SetFloat("_Raymarch_Distance", 50);
        fogMaterial.SetFloat("_Raymarch_Distance_Bias", 0.5f);
    }

    //Update the grass settings for current grass loaded
    public void SetGrass()
    {
        for (byte i = 0; i < grassMaterials.Length; i++) {
            if (grassMaterials[i] == null) continue;
            grassMaterials[i].SetFloat("_BladeSegments", player.grassQuality.value);
            grassMaterials[i].SetFloat("_GrassLODFade", player.grassLod.value * 0.1f);
            grassMaterials[i].SetFloat("_FrustrumCull", player.fov.value * 0.05f + 2);
        }
    }

    public void Embark()
    {
        if (player.interacting) player.FreePlayer();
        //Make sure to match dungeons lmao
        generation.seed = (int)Random.Range(-2147483648, 2147483647f);
        manager.ShareSeedServer(generation.seed, 0);
        ToggleDungeonSelect(false);
    }

    public void ToggleDungeonSelect(bool toggle)
    {
        manager.DungeonDisplayServer(manager.dungeonIndex, toggle);
        dungeonSelectObject.SetActive(toggle);
    }

    public void NextDungeon(int dir)
    {
        if (manager.dungeons.Count <= 1) manager.DungeonDisplayServer(manager.dungeonIndex, true);
        else manager.DungeonDisplayServer((byte)((manager.dungeonIndex + dir + manager.dungeons.Count) % manager.dungeons.Count), true);
    }

    public void DungeonDisplay(Dungeon dung, bool status)
    {
        dungeonSelectObject.SetActive(dung != null && status);
        dungeonInfo.text = $"{dung.name}\n\n{dung.description}";
    }

    public void ToggleColorPicker(bool toggle)
    {
        colorPickerObject.SetActive(toggle);
    }

    //Set player color
    public void SelectColor()
    {
        player.ChangeColor(colorPicked);
    }

    public Networker PlayerSetup(PlayerController p)
    {
        player = p;
        return manager;
    }
}