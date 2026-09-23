using System;

/// <summary>
/// All tunable game values. Edit them on the GameSimulator in the Inspector
/// (core-loop Q3: "allow me to edit tick speed in the editor for testing")
/// or change the defaults here.
/// </summary>
[Serializable]
public class GameConfig
{
    // Time (core-loop Q3): 1 tick = 1 minute, tax every 5 ticks
    public float TickSeconds = 60f;
    public int TaxEveryTicks = 5;

    // Tax (tax-plan Q1/Q3)
    public float TaxRate = 0.30f;        // 30% of production value
    public float PenaltyPerFail = 0.20f; // each failed tax raises the next bill by 20%
    public int BankruptAtFails = 2;      // 2 fails in a row = bankruptcy
    public float ExtraTaxBonus = 0.5f;   // the extra portion of a bill is worth 1.5x PP

    // World (tax-plan Q6/Q7): 20 nodes, 4 companies, 1 node each at start
    public int TotalNodes = 20;
    public int CompanyCount = 4;
    public float StartingPoliticalPower = 400f; // enough for the first ~2 nodes (core-loop Q8)
    public float StartingResources = 10f;

    // Production & storage (resource-hub-plan)
    public float BaseCapacityPerResource = 20f; // player's default limit per resource
    public float BaseNodeProduction = 2f;       // units per tick per produced resource
    public float BaseNodeCapacity = 10f;        // storage added by a node
    public int MaxModuleSlots = 5;              // resource-hub-plan Q8

    // Node prices (core-loop Q8: the more nodes you own, the more the next one costs)
    public float NodeBaseValue = 100f;
    public float NodeCostGrowth = 0.5f;

    // AI aggressiveness (core-loop Q7: simple actions, expand like the player)
    public float AIExpansionChance = 0.35f;
    public float AITradeChance = 0.25f;
}
