using UnityEngine;

public class Manager : MonoBehaviour
{
    public bool inGame = false;
    public float gameTime = 0;
    public DungeonGeneration generation;
    public Material fogMaterial;
    public PlayerController player;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<PlayerController>();
        NextRound();
    }

    private void Update()
    {
        if (!inGame) {
            if (generation.dungeonGenerated) {
                inGame = true;
                gameTime = 0;
                SetFog(generation.dungeon.atmospheres[generation.atmoID].fog);
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
        generation.seed = (int)Random.Range(-2147483648, 2147483647f);
        generation.Routine();
    }

    public void SetFog(FogSettings settings)
    {
        fogMaterial.SetColor("_Colour", settings.Color);
        fogMaterial.SetFloat("_Density", settings.Density);
        fogMaterial.SetFloat("_Raymarch_Distance", settings.Distance);
        fogMaterial.SetFloat("_Raymarch_Distance_Bias", settings.Blend);
    }
}