using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A company (the player's company or one of the AI companies).
/// Owns nodes, holds an inventory, pays taxes and spends political power.
/// </summary>
public class Company
{
    public int Id;
    public string Name;
    public Color Color;
    public bool IsPlayer;
    public bool Alive = true;

    public float PoliticalPower;

    public readonly Dictionary<ResourceType, float> Inventory =
        new Dictionary<ResourceType, float>();

    // ---- tax state (tax-system-plan) ----
    public float ExtraTaxPct; // 0..1, player-chosen extra tax % (tax-plan Q3)
    public List<ResourceType> PaymentPriority; // which resources to pay the tax with, in order (tax-plan Q1)
    public int FailedTaxesInARow;
    public float LastTaxQuota;
    public float LastTaxPaid;
    public float ProducedValueThisCycle; // PP value of production since the last tax tick

    public readonly List<Node> Nodes = new List<Node>();

    public Company(int id, string name, Color color, bool isPlayer)
    {
        Id = id;
        Name = name;
        Color = color;
        IsPlayer = isPlayer;
        PaymentPriority = new List<ResourceType>();
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            Inventory.Add((ResourceType)i, 0f);
            PaymentPriority.Add((ResourceType)i);
        }
    }

    public int OwnedNodeCount { get { return Nodes.Count; } }

    public float GetInventory(ResourceType r)
    {
        float v;
        if (Inventory.TryGetValue(r, out v)) return v;
        return 0f;
    }

    public void SetInventory(ResourceType r, float amount)
    {
        if (amount < 0f) amount = 0f;
        Inventory[r] = amount;
    }

    /// <summary>Total value (PP) of the inventory at current market prices.</summary>
    public float InventoryValue(Market m)
    {
        float v = 0f;
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
            v += GetInventory((ResourceType)i) * m.Price((ResourceType)i);
        return v;
    }

    /// <summary>
    /// Total storage capacity for a resource = base + every owned node producing it
    /// (resource-hub-plan Q7: default limit always increased by amount/type of nodes owned,
    /// storage modules double a node's capacity).
    /// </summary>
    public float CapacityFor(ResourceType r, float baseCapacity)
    {
        float cap = baseCapacity;
        foreach (var n in Nodes)
        {
            if (n.Produced.Contains(r))
                cap += n.BaseCapacity * n.CapacityMultiplier;
        }
        return cap;
    }

    public void ResetTaxCycle()
    {
        ProducedValueThisCycle = 0f;
        LastTaxQuota = 0f;
        LastTaxPaid = 0f;
    }
}
