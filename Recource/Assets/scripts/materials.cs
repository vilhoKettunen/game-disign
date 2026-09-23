using System.Collections.Generic;
using UnityEngine;

/// <summary>All resources in the game. 0-3 raw (from nodes), 4-7 refined (from factories).</summary>
public enum ResourceType
{
    Wood = 0,
    Metal,
    Energy,
    Water,
    Chips,
    MechanicalParts,
    BuildingMaterials,
    Food
}

/// <summary>Node modules (resource-hub-plan Q7): ProductionBooster = 2x production, StorageBooster = 2x storage.</summary>
public enum ModuleType
{
    ProductionBooster,
    StorageBooster
}

/// <summary>
/// Static catalog of resources, refining recipes and module build costs.
/// (GDD: trading-system-plan Q1, resource-hub-plan Q6/Q9)
/// All prices are in Political Power (PP) per unit - the government's value unit.
/// </summary>
public static class MaterialCatalog
{
    public const int ResourceCount = 8;

    public static readonly string[] Names =
    {
        "Wood", "Metal", "Energy", "Water",
        "Chips", "Mechanical Parts", "Building Materials", "Food"
    };

    public static readonly string[] Codes = { "W", "M", "E", "Wa", "Ch", "MP", "BM", "F" };

    // Government base prices (PP per unit)
    public static readonly float[] BasePrices = { 10f, 12f, 14f, 8f, 30f, 26f, 24f, 20f };

    public static readonly ResourceType[] Raw =
    {
        ResourceType.Wood, ResourceType.Metal, ResourceType.Energy, ResourceType.Water
    };

    public static readonly ResourceType[] Refined =
    {
        ResourceType.Chips, ResourceType.MechanicalParts, ResourceType.BuildingMaterials, ResourceType.Food
    };

    // Refining recipe: inputs needed per 1 unit of refined output (trading-plan Q1)
    //   Chips: 2 Metal + 1 Energy
    //   Mechanical Parts: 2 Metal + 1 Water
    //   Building Materials: 1 Wood + 1 Energy + 1 Water
    //   Food: 1 Energy + 1 Water
    static readonly float[,] Recipe =
    {
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 2, 1, 0, 0, 0, 0, 0 }, // Chips
        { 0, 2, 0, 1, 0, 0, 0, 0 }, // Mechanical Parts
        { 1, 0, 1, 1, 0, 0, 0, 0 }, // Building Materials
        { 0, 0, 1, 1, 0, 0, 0, 0 }, // Food
    };

    // Module build recipes (resource-hub-plan Q9)
    //   ProductionBooster: 10 Chips + 10 Mechanical Parts
    //   StorageBooster:    10 Building Materials + 10 Food
    static readonly float[,] ModuleRecipe =
    {
        { 0, 0, 0, 0, 10, 10, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 10, 10 },
    };

    public static string Name(ResourceType r) { return Names[(int)r]; }
    public static string Code(ResourceType r) { return Codes[(int)r]; }
    public static bool IsRefined(ResourceType r) { return (int)r >= 4; }
    public static float BasePrice(ResourceType r) { return BasePrices[(int)r]; }

    /// <summary>How much of <paramref name="input"/> is consumed per 1 unit of <paramref name="output"/> (0 = not an input).</summary>
    public static float InputAmount(ResourceType output, ResourceType input)
    { return Recipe[(int)output, (int)input]; }

    /// <summary>How much of resource <paramref name="r"/> a module of type <paramref name="m"/> costs to build.</summary>
    public static float ModuleAmount(ModuleType m, ResourceType r)
    { return ModuleRecipe[(int)m, (int)r]; }

    public static string ModuleRecipeText(ModuleType m)
    {
        var parts = new List<string>();
        for (int i = 0; i < ResourceCount; i++)
        {
            float a = ModuleAmount(m, (ResourceType)i);
            if (a > 0f) parts.Add(a.ToString("0") + " " + Name((ResourceType)i));
        }
        return string.Join(" + ", parts.ToArray());
    }

    /// <summary>Price fluctuation range per tax cycle (trading-plan Q5): raw ±20%, refined ±50%.</summary>
    public static float Fluctuation(ResourceType r) { return IsRefined(r) ? 0.5f : 0.2f; }
}
