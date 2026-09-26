using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns "map shape + settings + seed" into the node list (map-setup-plan.txt
/// section 2, the clean cut around NewGame). No changes to Node/Company/Market:
/// it just produces Nodes the way GameSimulator.NewGame() used to, but from a
/// MapDefinition (pinned or rolled types) or a fully random grid.
///
/// Deterministic: for a given seed the same map + node types are always produced,
/// so "map + seed" can be shared between players.
/// </summary>
public static class MapGenerator
{
    /// <summary>
    /// Deterministic random-grid layout for a seed: 15..25 slots per row.
    /// (Used by both the generator and the live 3D preview, so they match.)
    /// </summary>
    public static int SlotsForSeed(int seed)
    {
        uint h = (uint)seed;
        h = h * 2654435761u;
        h ^= h >> 13;
        return 15 + (int)(h % 11); // 15..25
    }

    public static void Generate(List<Node> nodes, MapDefinition map, GameSetup setup)
    {
        nodes.Clear();
        if (setup == null) setup = new GameSetup();
        int cap = Mathf.Max(1, setup.nodeCountCap);
        int id = 0;

        if (map != null)
        {
            // premade map: walk row-major, keep the first N cells (the cap)
            for (int r = 0; r < map.Rows && id < cap; r++)
            {
                for (int c = 0; c < map.Cols && id < cap; c++)
                {
                    char cell = map.CellAt(r, c);
                    if (!map.IsNodeCell(cell)) continue;
                    nodes.Add(MakeNode(id, map.CellCenter(r, c), cell, setup));
                    id++;
                }
            }
        }
        else
        {
            // fully random map (map-setup-plan Q2): node count + seed decide the layout
            int slots = SlotsForSeed(setup.seed);
            for (int i = 0; i < cap; i++)
            {
                Vector2 pos = new Vector2(
                    ((i % slots) - (slots - 1) * 0.5f) * MapDefinition.GridSpacing,
                    ((i / slots) - (slots - 1) * 0.5f) * MapDefinition.GridSpacing);
                nodes.Add(MakeNode(id, pos, 'R', setup));
                id++;
            }
        }
    }

    static Node MakeNode(int id, Vector2 pos, char cell, GameSetup setup)
    {
        var n = new Node();
        n.Id = id;
        n.Position = pos;
        n.BaseProduction = 2f;   // == GameConfig.BaseNodeProduction default
        n.BaseCapacity = 10f;    // == GameConfig.BaseNodeCapacity default
        n.MaxSlots = 5;          // == GameConfig.MaxModuleSlots default

        ResourceType? first = null;
        bool factory = false;

        var rawPin = MapDefinitionFixedRaw(cell);
        var refPin = MapDefinitionFixedRefined(cell);
        if (rawPin != null)
        {
            first = rawPin;
        }
        else if (refPin != null)
        {
            first = refPin;
            factory = true;
        }
        else if (cell == 'R')
        {
            if (Roll(setup) >= 4) { first = RollRefined(setup); factory = true; }
            else first = RollRaw(setup);
        }
        else if (cell == '?')
        {
            first = RollRaw(setup);
        }

        if (first == null) first = ResourceType.Wood; // safety (unreachable for valid legends)
        n.Produced.Add(first.Value);

        if (!factory && Random.value < setup.dualResourceHubPct / 100f)
        {
            var other = RollRaw(setup);
            if (other != first) n.Produced.Add(other);
        }

        if (factory)
        {
            for (int k = 0; k < MaterialCatalog.Raw.Length; k++)
            {
                var raw = MaterialCatalog.Raw[k];
                if (MaterialCatalog.InputAmount(first.Value, raw) > 0f) n.Inputs.Add(raw);
            }
        }

        n.Name = (id + 1) + ". " + n.ProducesText()
               + ((n.Inputs.Count > 0) ? " (factory, needs " + n.InputsText() + ")" : "");
        return n;
    }

    static ResourceType? MapDefinitionFixedRaw(char c)
    {
        switch (c)
        {
            case 'W': return ResourceType.Wood;
            case 'M': return ResourceType.Metal;
            case 'E': return ResourceType.Energy;
            case 'a': return ResourceType.Water;
            default: return null;
        }
    }

    static ResourceType? MapDefinitionFixedRefined(char c)
    {
        switch (c)
        {
            case 'C': return ResourceType.Chips;
            case 'P': return ResourceType.MechanicalParts;
            case 'B': return ResourceType.BuildingMaterials;
            case 'F': return ResourceType.Food;
            default: return null;
        }
    }

    /// <summary>Weighted roll over the settings mix: 0-3 = raw types, 4 = "factory".</summary>
    static int Roll(GameSetup setup)
    {
        int[] weights = { setup.rawWoodPct, setup.rawMetalPct, setup.rawEnergyPct, setup.rawWaterPct, setup.factoryPct };
        int total = 0;
        for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0, weights[i]);
        if (total <= 0) return Random.Range(0, MaterialCatalog.ResourceCount);
        float v = Random.value * total;
        for (int i = 0; i < weights.Length; i++)
        {
            v -= Mathf.Max(0, weights[i]);
            if (v <= 0f) return i;
        }
        return 4;
    }

    static ResourceType RollRaw(GameSetup setup)
    {
        for (int i = 0; i < 64; i++)
        {
            int r = Roll(setup);
            if (r >= 0 && r < 4) return (ResourceType)r;
        }
        return ResourceType.Wood; // mix has zero raw weight: fall back
    }

    static ResourceType RollRefined(GameSetup setup)
    {
        for (int i = 0; i < 64; i++)
        {
            int r = Roll(setup);
            if (r >= 4) return (ResourceType)r;
        }
        return ResourceType.Chips; // mix has zero factory weight: fall back
    }
}
