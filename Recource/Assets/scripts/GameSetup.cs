using System;

/// <summary>
/// Everything the setup screen edits (map-setup-plan.txt section 4): map mode,
/// premade map, seed, node count cap, node type mix, dual-hub chance, company
/// count, starting PP, tax settings and tick length.
///
/// Serializable + JSON (System.Text.Json, built into Unity 2022) so players can
/// save presets and import/export a setup as a plain, shareable text file.
/// </summary>
[Serializable]
public class GameSetup
{
    public int version = 1;

    // map (map-setup-plan Q2/Q6)
    public int mapMode = 0;        // 0 = premade map, 1 = fully random
    public int premadeMap = 0;     // index into the MapDefinition assets

    // generation
    public int seed = 12345;       // same seed = same random map + node types (shareable!)
    public int nodeCountCap = 20;
    public int dualResourceHubPct = 20;

    // type mix (weights, normalized when rolled)
    public int rawWoodPct = 25;
    public int rawMetalPct = 25;
    public int rawEnergyPct = 25;
    public int rawWaterPct = 25;
    public int factoryPct = 20;

    // companies & economy
    public int companyCount = 4;
    public float startingPP = 400f;
    public float nodeBaseValue = 100f;   // first node cost (PP)
    public float nodeCostGrowthMult = 1.4f; // exponential: next = base * mult^nodesOwned
    public float taxEveryTicks = 5f;
    public float taxRatePct = 30f;
    public float tickSeconds = 60f;

    public GameSetup Clone()
    {
        var s = new GameSetup();
        s.version = version;
        s.mapMode = mapMode;
        s.premadeMap = premadeMap;
        s.seed = seed;
        s.nodeCountCap = nodeCountCap;
        s.dualResourceHubPct = dualResourceHubPct;
        s.rawWoodPct = rawWoodPct;
        s.rawMetalPct = rawMetalPct;
        s.rawEnergyPct = rawEnergyPct;
        s.rawWaterPct = rawWaterPct;
        s.factoryPct = factoryPct;
        s.companyCount = companyCount;
        s.startingPP = startingPP;
        s.nodeBaseValue = nodeBaseValue;
        s.nodeCostGrowthMult = nodeCostGrowthMult;
        s.taxEveryTicks = taxEveryTicks;
        s.taxRatePct = taxRatePct;
        s.tickSeconds = tickSeconds;
        return s;
    }

    // ================= JSON (presets / import / export) =================
    // Hand-rolled (Unity 6 runs .NET Standard - no System.Text.Json). The format
    // is plain "key=value" lines, so it is easy to read, edit and share.

    public string ToJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("version=").Append(version).Append('\n');
        sb.Append("mapMode=").Append(mapMode).Append('\n');
        sb.Append("premadeMap=").Append(premadeMap).Append('\n');
        sb.Append("seed=").Append(seed).Append('\n');
        sb.Append("nodeCountCap=").Append(nodeCountCap).Append('\n');
        sb.Append("dualResourceHubPct=").Append(dualResourceHubPct).Append('\n');
        sb.Append("rawWoodPct=").Append(rawWoodPct).Append('\n');
        sb.Append("rawMetalPct=").Append(rawMetalPct).Append('\n');
        sb.Append("rawEnergyPct=").Append(rawEnergyPct).Append('\n');
        sb.Append("rawWaterPct=").Append(rawWaterPct).Append('\n');
        sb.Append("factoryPct=").Append(factoryPct).Append('\n');
        sb.Append("companyCount=").Append(companyCount).Append('\n');
        sb.Append("startingPP=").Append(startingPP.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("nodeBaseValue=").Append(nodeBaseValue.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("nodeCostGrowthMult=").Append(nodeCostGrowthMult.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("taxEveryTicks=").Append(taxEveryTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("taxRatePct=").Append(taxRatePct.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("tickSeconds=").Append(tickSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        return sb.ToString();
    }

    public static GameSetup FromJson(string json)
    {
        if (json == null) throw new System.ArgumentException("empty setup file");
        var s = new GameSetup();
        foreach (string raw in json.Split('\n'))
        {
            string line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string k = line.Substring(0, eq).Trim();
            string v = line.Substring(eq + 1).Trim();
            int i; float f;
            switch (k)
            {
                case "version": if (int.TryParse(v, out i)) s.version = i; break;
                case "mapMode": if (int.TryParse(v, out i)) s.mapMode = i; break;
                case "premadeMap": if (int.TryParse(v, out i)) s.premadeMap = i; break;
                case "seed": if (int.TryParse(v, out i)) s.seed = i; break;
                case "nodeCountCap": if (int.TryParse(v, out i)) s.nodeCountCap = i; break;
                case "dualResourceHubPct": if (int.TryParse(v, out i)) s.dualResourceHubPct = i; break;
                case "rawWoodPct": if (int.TryParse(v, out i)) s.rawWoodPct = i; break;
                case "rawMetalPct": if (int.TryParse(v, out i)) s.rawMetalPct = i; break;
                case "rawEnergyPct": if (int.TryParse(v, out i)) s.rawEnergyPct = i; break;
                case "rawWaterPct": if (int.TryParse(v, out i)) s.rawWaterPct = i; break;
                case "factoryPct": if (int.TryParse(v, out i)) s.factoryPct = i; break;
                case "companyCount": if (int.TryParse(v, out i)) s.companyCount = i; break;
                case "startingPP": if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) s.startingPP = f; break;
                case "nodeBaseValue": if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) s.nodeBaseValue = f; break;
                case "nodeCostGrowthMult": if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) s.nodeCostGrowthMult = f; break;
                case "taxEveryTicks": if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) s.taxEveryTicks = f; break;
                case "taxRatePct": if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) s.taxRatePct = f; break;
                case "tickSeconds": if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) s.tickSeconds = f; break;
            }
        }
        return s;
    }
}
