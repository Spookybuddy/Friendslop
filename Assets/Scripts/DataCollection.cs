using UnityEngine;
using System.IO;

public class DataCollection : MonoBehaviour
{
    [Tooltip("The dungeon generator to gather data from")]
    public DungeonGeneration gen;
    [Tooltip("How many seeds to run through")]
    public uint dataSize = 200;
    [Tooltip("Pick random seeds, or do incrementing seeds")]
    public bool randomSeeds = false;
    private uint index = 0;
    private string range;
    private string output;

    void Start()
    {
        range = $"({gen.dungeon.name}) {gen.seed}-{gen.seed + dataSize}";
        output = $"Seed:\tSize:\tPercentage\tTime:\t \tDungeon Settings:\tTarget area:\tMin rotation:\tMax rotation:\tPath quality:\tPath width:\n";
    }

    void Update()
    {
        //Generate, collect data, then generate next seed
        if (gen.dungeonGenerated) {
            string con = gen.generationTime.ToString();
            output += $"#{string.Format("{0:N}", gen.seed)[..^3]}\t{gen.currentSize}\t{string.Format("{0:0.000}", gen.currentSize / (float)gen.dungeon.targetSurfaceArea * 100)}%\t{con[..^(Mathf.Max(con.Length - 5 - con.LastIndexOf('.'), 0))]}";
            if (index == 0) output += $"\t \t{gen.dungeon.name}\t{gen.dungeon.targetSurfaceArea}\t{gen.dungeon.minRotationVariation}\t{gen.dungeon.maxRotationVariation}\t{gen.dungeon.quality}\t{gen.dungeon.pathWidth}\n";
            else output += "\n";
            index++;
            //Write to file & turn off
            if (index > dataSize) {
                output += $"Success Rate\tCompleted Avg Size\tSuccesses #\tCompleted Avg Time\n" +
                    $"=C{dataSize + 4}/COUNT(C2:C{dataSize + 2})\t=AVERAGEIF(C2:C{dataSize + 2}, \">=1\", B2:B{dataSize + 2})\t=COUNTIF(C2:C{dataSize + 2}, \">=1\")\t=AVERAGEIF(C2:C{dataSize + 2}, \">=1\", D2:D{dataSize + 2})\n" +
                    $" \tFull Avg Size\tFailures #\tFull Avg Time\n" +
                    $" \t=AVERAGE(B2:B{dataSize + 2})\t={dataSize + 1}-C{dataSize + 4}\t=AVERAGE(D2:D{dataSize + 2})";
                Text();
                this.enabled = false;
                return;
            }
            if (randomSeeds) gen.seed = gen.rng.Next();
            else gen.seed++;
            gen.Routine();
        }
    }

    private void Text()
    {
        Debug.Log("<color=#11BB19>Finished gathering data</color>");
        range = System.Environment.ExpandEnvironmentVariables("%userprofile%\\downloads\\") + $"{range}.txt";
        File.WriteAllText(range, output);
    }
}