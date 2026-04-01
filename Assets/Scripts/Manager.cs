using UnityEngine;

public class Manager : MonoBehaviour
{
    public bool inGame = false;
    public float gameTime = 0;
    public DungeonGeneration generation;
    public Material fogMaterial;
    public Material[] grassMaterials = new Material[2];
    public PlayerController player;

    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<PlayerController>();
    }

    private void Start()
    {
        NextRound();
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
    }

    [ContextMenu("Generate")]
    public void NextRound()
    {
        inGame = false;
        //generation.seed = (int)Random.Range(-2147483648, 2147483647f);
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
}