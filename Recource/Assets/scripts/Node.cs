using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// A node on the map: a raw resource hub or a factory (refined production).
/// Up to MaxSlots modules can be built on it (resource-hub-plan Q7/Q8).
/// Owner == null means the node belongs to the government.
/// </summary>
public class Node
{
    public int Id;
    public string Name;
    public readonly List<ResourceType> Produced = new List<ResourceType>();
    public readonly List<ResourceType> Inputs = new List<ResourceType>(); // factories only
    public float BaseProduction; // units per tick per produced resource
    public float BaseCapacity;   // storage this node adds per resource it produces
    public Company Owner;
    public int ProductionModules;
    public int StorageModules;
    public int MaxSlots = 5;
    public Vector2 Position; // flat map position

    public bool IsFactory
    {
        get { return Produced.Count > 0 && MaterialCatalog.IsRefined(Produced[0]); }
    }

    // Each module doubles (resource-hub-plan Q7)
    public float ProductionMultiplier { get { return Mathf.Pow(2f, ProductionModules); } }
    public float CapacityMultiplier { get { return Mathf.Pow(2f, StorageModules); } }

    public int ModuleCount { get { return ProductionModules + StorageModules; } }
    public bool HasFreeSlot { get { return ModuleCount < MaxSlots; } }

    public float ProductionPerTick(ResourceType r)
    {
        if (!Produced.Contains(r)) return 0f;
        return BaseProduction * ProductionMultiplier;
    }

    public string ProducesText()
    {
        var s = new StringBuilder();
        for (int i = 0; i < Produced.Count; i++)
        {
            if (i > 0) s.Append(" + ");
            s.Append(MaterialCatalog.Name(Produced[i]));
        }
        return s.ToString();
    }

    public string InputsText()
    {
        if (Produced.Count == 0) return "";
        var s = new StringBuilder();
        for (int i = 0; i < Inputs.Count; i++)
        {
            if (i > 0) s.Append(" + ");
            s.Append(MaterialCatalog.InputAmount(Produced[0], Inputs[i]).ToString("0"))
             .Append(" ")
             .Append(MaterialCatalog.Name(Inputs[i]));
        }
        return s.ToString();
    }
}
