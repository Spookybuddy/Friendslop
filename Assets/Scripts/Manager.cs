using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Manager : MonoBehaviour
{
    public bool inGame = false;
    public float gameTime = 0;
    public DungeonGeneration generation;
    public List<Dungeon> dungeons = new List<Dungeon>();
    private byte dungeonIndex = 0;
    public Material fogMaterial;
    public Material[] grassMaterials = new Material[2];
    public PlayerController player;
    [Header("Interaction Objects")]
    public GameObject dungeonSelectObject;
    public GameObject colorPickerObject;
    public RawImage colorDisplay;
    private Color colorPicked;

    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<PlayerController>();
    }

    private void Update()
    {
        if (!inGame) {
            if (generation.dungeonGenerated) {
                inGame = true;
                gameTime = 0;
                SetFog(generation.dungeon.atmospheres[generation.atmoID].fog);
                player.SetSnowDepth(generation.snowRayLength);
                player.Weather(generation.dungeon.atmospheres[generation.atmoID].weather);
            }
        } else {
            gameTime += Time.deltaTime;
        }

        if (colorPickerObject.activeSelf) {
            if (Physics.Raycast(player.head.position, player.head.forward, out RaycastHit picker, 5, 32)) {
                Texture2D tex = picker.transform.GetComponent<Renderer>().material.mainTexture as Texture2D;
                colorPicked = tex.GetPixelBilinear(picker.textureCoord.x, picker.textureCoord.y);
                colorDisplay.color = colorPicked;
            }
        }
    }

    [ContextMenu("Generate")]
    public void NextRound()
    {
        inGame = false;
        generation.dungeon = dungeons[dungeonIndex];
        generation.seed = (int)Random.Range(-2147483648, 2147483647f);
        generation.Routine();
        grassMaterials[1] = generation.GetGrassMat();
        SetGrass();
    }

    //Change the fog settings
    public void SetFog(FogSettings settings)
    {
        fogMaterial.SetColor("_Colour", settings.Color);
        fogMaterial.SetFloat("_Density", settings.Density);
        fogMaterial.SetFloat("_Raymarch_Distance", settings.Distance);
        fogMaterial.SetFloat("_Raymarch_Distance_Bias", settings.Blend);
    }

    //Update the grass settings for current grass loaded
    public void SetGrass()
    {
        for (byte i = 0; i < grassMaterials.Length; i++) {
            if (grassMaterials[i] == null) continue;
            grassMaterials[i].SetFloat("_BladeSegments", player.grassQuality.value);
            grassMaterials[i].SetFloat("_GrassLODFade", player.grassLod.value / 10.0f);
        }
    }

    public void ToggleDungeonSelect(bool toggle)
    {
        dungeonSelectObject.SetActive(toggle);
    }

    public void ToggleColorPicker(bool toggle)
    {
        colorPickerObject.SetActive(toggle);
    }

    //Change which dungeon is selected
    public void SelectDungeon(byte add)
    {
        //Should check that all players have matching dungeons (some unique value generated per object?)
        dungeonIndex = (byte)((dungeonIndex + add) % 255);
    }

    //Set player color
    public void SelectColor()
    {
        player.ChangeColor(colorPicked);
    }
}