using UnityEngine;

/// <summary>
/// Government-set prices in Political Power (PP) per unit (trading-plan Q2/Q4/Q5).
/// Re-rolled every tax cycle:
///   price = base * warDemand(0.75-1.5) * fluctuation(raw ±20% / refined ±50%) * special(2x)
/// One random refined resource gets the 2x "rare shortage" bonus per cycle.
/// </summary>
public class Market
{
    public readonly float[] Prices = new float[MaterialCatalog.ResourceCount];
    public readonly float[] Demand = new float[MaterialCatalog.ResourceCount];
    public ResourceType SpecialResource;

    public float Price(ResourceType r) { return Prices[(int)r]; }

    public void NewCycle()
    {
        // pick one random refined resource (indices Raw.Length .. ResourceCount-1) for the 2x bonus
        int start = MaterialCatalog.Raw.Length;
        int count = MaterialCatalog.ResourceCount - start;
        SpecialResource = (ResourceType)(start + Random.Range(0, count));
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            var r = (ResourceType)i;
            float demand = Random.Range(0.75f, 1.5f);
            float fluct = 1f + Random.Range(-MaterialCatalog.Fluctuation(r), MaterialCatalog.Fluctuation(r));
            float special = (r == SpecialResource) ? 2f : 1f;
            Demand[i] = demand;
            Prices[i] = MaterialCatalog.BasePrice(r) * demand * fluct * special;
        }
    }
}
