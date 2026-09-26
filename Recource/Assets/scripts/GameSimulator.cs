// no "using System" here: keeps Random = UnityEngine.Random unambiguous
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core game logic: world state, ticks, production, taxes, trading,
/// AI companies and win/lose (implements all four GDD plans).
///
/// - core-loop: tick every config.TickSeconds (default 60s), tax every 5 ticks,
///   win = own all nodes + all companies gone, lose = 2 failed taxes in a row
/// - trading: resource-for-resource trades accepted only when you offer MORE value,
///   node/company purchases, government prices re-rolled each tax cycle
/// - resource-hub: nodes produce per tick, 5 module slots, 2x production / 2x storage modules
/// - tax: 30% of production value, paid in resources via the player's template,
///   optional extra %, +20% penalty per fail, government seizes bankrupt companies.
///   The bill ACCUMULATES over the whole tax cycle (all ticks until the tax tick);
///   if the inventory value is below the due tax the tax FAILS and the ENTIRE
///   inventory is taken by the government.
/// - metrics: per-resource production/consumption flow per tick + total inventory
///   value (PP) shown in the UI so the player sees how their resources move
///
/// GameView auto-creates a GameSimulator if none exists in the scene.
/// </summary>
public class GameSimulator : MonoBehaviour
{
    public GameConfig config;

    public readonly List<Company> Companies = new List<Company>();
    public readonly List<Node> Nodes = new List<Node>();
    public Market market = new Market();

    public int TickCount;
    public int TicksUntilTax;
    public bool GameOver;
    public string GameOverText = "";
    public Company Winner;
    public readonly List<string> Log = new List<string>();

    /// <summary>Raised on any state change (UI refresh hook).</summary>
    public System.Action OnStateChanged;

    float timeAccumulator;

    public static readonly Color GovernmentColor = new Color(0.55f, 0.55f, 0.55f);
    static readonly Color[] CompanyColors =
    {
        new Color(0.30f, 0.75f, 0.35f), // player: green
        new Color(0.80f, 0.30f, 0.30f), // red
        new Color(0.35f, 0.45f, 0.85f), // blue
        new Color(0.85f, 0.75f, 0.30f)  // yellow
    };

    public Company Player
    {
        get { return (Companies.Count > 0) ? Companies[0] : null; }
    }

    /// <summary>
    /// The color to display for a node's ownership: the owner company's color,
    /// or the government's color when unowned (node.Owner == null).
    /// Every company gets its own fixed color, and the nodes it owns always use it.
    /// </summary>
    public static Color ColorOf(Node n)
    {
        return (n.Owner != null) ? n.Owner.Color : GovernmentColor;
    }

    void Awake()
    {
        if (config == null) config = new GameConfig();
    }

    void Start()
    {
        if (Nodes.Count == 0) NewGame();
    }

    void Update()
    {
        if (GameOver) return;
        timeAccumulator += Time.deltaTime;
        while (timeAccumulator >= config.TickSeconds)
        {
            timeAccumulator -= config.TickSeconds;
            DoTick();
            if (GameOver) break;
        }
    }

    // ================= NEW GAME (core-loop Q8, tax-plan Q6) =================

    public void NewGame()
    {
        Companies.Clear();
        Nodes.Clear();
        Log.Clear();
        TickCount = 0;
        TicksUntilTax = config.TaxEveryTicks;
        GameOver = false;
        Winner = null;
        GameOverText = "";
        timeAccumulator = 0f;

        for (int i = 0; i < config.CompanyCount; i++)
        {
            var c = new Company(i, (i == 0) ? "Your Company" : "AI Company " + (i + 1),
                                CompanyColors[i % CompanyColors.Length], i == 0);
            c.PoliticalPower = config.StartingPoliticalPower;
            Companies.Add(c);
        }

        int rawCursor = 0;
        for (int i = 0; i < config.TotalNodes; i++)
        {
            var n = new Node();
            n.Id = i;
            n.BaseProduction = config.BaseNodeProduction;
            n.BaseCapacity = config.BaseNodeCapacity;
            n.MaxSlots = config.MaxModuleSlots;

            if (i % 4 == 3)
            {
                // every 4th node is a FACTORY producing a refined resource (indices Raw.Length .. ResourceCount-1)
                int start = MaterialCatalog.Raw.Length;
                int count = MaterialCatalog.ResourceCount - start;
                var outType = (ResourceType)(start + Random.Range(0, count));
                n.Produced.Add(outType);
                for (int k = 0; k < MaterialCatalog.Raw.Length; k++)
                {
                    var raw = MaterialCatalog.Raw[k];
                    if (MaterialCatalog.InputAmount(outType, raw) > 0f) n.Inputs.Add(raw);
                }
            }
            else
            {
                // raw hub
                n.Produced.Add(MaterialCatalog.Raw[rawCursor % MaterialCatalog.Raw.Length]);
                if (Random.value < 0.2f)
                {
                    // some raw hubs produce 2 types at once (resource-hub-plan Q5)
                    int other = (rawCursor + 1 + Random.Range(0, 3)) % MaterialCatalog.Raw.Length;
                    n.Produced.Add(MaterialCatalog.Raw[other]);
                }
                rawCursor++;
            }

            n.Position = new Vector2((i % 5) * 5f, (i / 5) * 5f);
            n.Name = (i + 1) + ". " + n.ProducesText()
                   + ((n.Inputs.Count > 0) ? " (factory, needs " + n.InputsText() + ")" : "");
            Nodes.Add(n);
        }

        // Every company starts with 1 node; the rest belong to the government (tax-plan Q6)
        for (int i = 0; i < config.CompanyCount && i < Nodes.Count; i++)
        {
            var n = Nodes[i];
            var c = Companies[i];
            n.Owner = c;
            c.Nodes.Add(n);
            foreach (var r in n.Produced) c.SetInventory(r, config.StartingResources);
        }

        market.NewCycle();
        AddLog("=== NEW GAME: " + Companies.Count + " companies, " + Nodes.Count
             + " nodes, " + GovernmentNodeCount() + " government-owned ===");
        StateChanged();
    }

    // ================= TICKS (core-loop Q3) =================

    public void DoTick()
    {
        if (GameOver) return;
        TickCount++;

        foreach (var c in Companies)
            if (c.Alive) Produce(c);

        foreach (var c in Companies)
            if (c.Alive && !c.IsPlayer) AIAct(c);

        TicksUntilTax--;
        if (TicksUntilTax <= 0)
        {
            TicksUntilTax = config.TaxEveryTicks;
            AddLog("--- TAX TICK (tick " + TickCount + ") ---");
            TaxTick();
        }
        else
        {
            AddLog("Tick " + TickCount + ": production. Tax in " + TicksUntilTax + ".");
        }
        CheckEnd();
        StateChanged();
    }

    /// <summary>Manual tick (UI button / testing).</summary>
    public void ForceTick() { DoTick(); }

    /// <summary>Manual tax tick right now (UI button / testing).</summary>
    public void ForceTax()
    {
        if (GameOver) return;
        TicksUntilTax = 0;
        DoTick();
    }

    // ================= PRODUCTION (resource-hub-plan Q4) =================

    void Produce(Company c)
    {
        foreach (var n in c.Nodes)
        {
            foreach (var outR in n.Produced)
            {
                float space = Mathf.Max(0f,
                    c.CapacityFor(outR, config.BaseCapacityPerResource) - c.GetInventory(outR));
                if (space <= 0f) continue;

                float maxOut = Mathf.Min(n.ProductionPerTick(outR), space);

                if (n.Inputs.Count > 0)
                {
                    // factory: limit output by available inputs
                    foreach (var inR in n.Inputs)
                    {
                        float ratio = MaterialCatalog.InputAmount(outR, inR);
                        if (ratio <= 0f) continue;
                        maxOut = Mathf.Min(maxOut, c.GetInventory(inR) / ratio);
                    }
                }
                if (maxOut <= 0.001f) continue;

                foreach (var inR in n.Inputs)
                {
                    float ratio = MaterialCatalog.InputAmount(outR, inR);
                    if (ratio > 0f)
                        c.SetInventory(inR, c.GetInventory(inR) - maxOut * ratio);
                }
                c.SetInventory(outR, c.GetInventory(outR) + maxOut);
                c.ProducedValueThisCycle += maxOut * market.Price(outR);
            }
        }
    }

    // ================= METRICS (inventory value + resource flow) =================

    /// <summary>
    /// How many units of resource r the company's owned nodes PRODUCE per tick
    /// (raw hubs + refined output of its factories, boosted by modules).
    /// </summary>
    public float ProducedPerTick(Company c, ResourceType r)
    {
        float sum = 0f;
        foreach (var n in c.Nodes) sum += n.ProductionPerTick(r);
        return sum;
    }

    /// <summary>
    /// How many units of resource r are CONSUMED per tick by the company's owned
    /// factories (full-rate demand; the factory may run lower when it is starved).
    /// </summary>
    public float ConsumedPerTick(Company c, ResourceType r)
    {
        float sum = 0f;
        foreach (var n in c.Nodes)
            if (n.IsFactory) sum += MaterialCatalog.InputAmount(n.Produced[0], r);
        return sum;
    }

    /// <summary>
    /// The tax bill that will be due at the next tax tick: 30% of the accumulated
    /// production value of THIS tax cycle (+ penalty, + extra tax %). The bill
    /// accumulates over the whole tax cycle, not just the last tick.
    /// </summary>
    public float UpcomingTaxQuota(Company c)
    {
        float penalty = Mathf.Pow(1f + config.PenaltyPerFail, c.FailedTaxesInARow);
        return c.ProducedValueThisCycle * config.TaxRate * penalty * (1f + c.ExtraTaxPct);
    }

    // ================= TAXES (tax-plan Q1/Q3) =================

    void TaxTick()
    {
        market.NewCycle();
        AddLog("New government prices. Special (2x) this cycle: " + MaterialCatalog.Name(market.SpecialResource));

        foreach (var c in Companies)
        {
            if (!c.Alive) { c.ResetTaxCycle(); continue; }

            float baseQuota = c.ProducedValueThisCycle * config.TaxRate *
                Mathf.Pow(1f + config.PenaltyPerFail, c.FailedTaxesInARow);
            float quota = baseQuota * (1f + c.ExtraTaxPct); // == UpcomingTaxQuota(c)
            c.LastTaxQuota = quota;

            // Payment template: player picks the order (tax-plan Q1);
            // AI companies pay with their most valuable resources.
            List<ResourceType> order;
            if (c.IsPlayer)
            {
                order = c.PaymentPriority;
            }
            else
            {
                order = new List<ResourceType>();
                for (int i = 0; i < MaterialCatalog.ResourceCount; i++) order.Add((ResourceType)i);
                order.Sort(delegate(ResourceType a, ResourceType b)
                {
                    return market.Price(b).CompareTo(market.Price(a));
                });
            }

            float paid = 0f;
            foreach (var r in order)
            {
                if (paid >= quota - 0.001f) break;
                float price = market.Price(r);
                if (price <= 0f) continue;
                float have = c.GetInventory(r);
                float take = Mathf.Min(have, (quota - paid) / price);
                if (take <= 0f) continue;
                c.SetInventory(r, have - take);
                paid += take * price;
            }
            c.LastTaxPaid = paid;

            if (quota <= 0.01f)
            {
                // nothing produced, nothing to pay
                c.FailedTaxesInARow = 0;
                c.ResetTaxCycle();
                continue;
            }

            if (paid >= quota - 0.001f)
            {
                c.FailedTaxesInARow = 0;
                float bonus = Mathf.Max(0f, paid - baseQuota) * config.ExtraTaxBonus;
                c.PoliticalPower += paid + bonus;
                AddLog(c.Name + " paid " + paid.ToString("0") + " PP tax -> +"
                     + (paid + bonus).ToString("0") + " political power");
            }
            else
            {
                c.FailedTaxesInARow++;
                AddLog("!! " + c.Name + " FAILED tax: paid " + paid.ToString("0")
                     + " of " + quota.ToString("0") + " -> WHOLE inventory seized (fail " + c.FailedTaxesInARow + "/"
                     + config.BankruptAtFails + ", next bill +"
                     + Mathf.RoundToInt(config.PenaltyPerFail * 100f) + "%)");
                if (c.FailedTaxesInARow >= config.BankruptAtFails) Bankrupt(c);
            }
            c.ResetTaxCycle();
        }
    }

    void Bankrupt(Company c)
    {
        c.Alive = false;
        AddLog("== " + c.Name + " is BANKRUPT. The government seized its " + c.OwnedNodeCount + " nodes.");
        foreach (var n in c.Nodes) n.Owner = null;
        c.Nodes.Clear();
        c.PoliticalPower = 0f;
        if (c.IsPlayer)
        {
            GameOver = true;
            Winner = null;
            GameOverText = "BANKRUPT - you failed your tax quota twice in a row.\nThe government took your company.";
        }
    }

    // ================= NODE COSTS & PURCHASES (core-loop Q8, tax-plan Q6/Q8) =================

    public int GovernmentNodeCount()
    {
        int count = 0;
        foreach (var n in Nodes) if (n.Owner == null) count++;
        return count;
    }

    public Node FirstGovernmentNode()
    {
        foreach (var n in Nodes) if (n.Owner == null) return n;
        return null;
    }

    /// <summary>Cost to buy a government node - scales with how many nodes you already own.</summary>
    public float GovernmentNodeCost(Company c)
    {
        return config.NodeBaseValue * (1f + config.NodeCostGrowth * c.OwnedNodeCount);
    }

    /// <summary>Value of a company-owned node (what a buyer must offer more than).</summary>
    public float CompanyNodeValue(Node n)
    {
        var s = n.Owner;
        if (s == null) return config.NodeBaseValue;
        return config.NodeBaseValue * (1f + config.NodeCostGrowth * s.OwnedNodeCount);
    }

    public bool BuyGovernmentNode(Company c, Node n, out string result)
    {
        if (!c.Alive) { result = "Company is gone"; return false; }
        if (n.Owner != null) { result = "Not a government node"; return false; }
        float cost = GovernmentNodeCost(c);
        if (c.PoliticalPower < cost)
        {
            result = "Not enough political power: need " + cost.ToString("0") + " PP";
            return false;
        }
        c.PoliticalPower -= cost;
        n.Owner = c;
        c.Nodes.Add(n);
        AddLog(c.Name + " took over " + n.Name + " from the government for " + cost.ToString("0") + " PP");
        CheckEnd();
        StateChanged();
        result = "ok";
        return true;
    }

    public bool BuyNode(Company buyer, Node n, ResourceType offerR, float offerAmt, out string result)
    {
        if (buyer == null || !buyer.Alive) { result = "Invalid buyer"; return false; }
        if (n.Owner == null) { result = "This is a government node - use the government purchase"; return false; }
        if (n.Owner == buyer) { result = "You already own this node"; return false; }
        if (offerAmt <= 0f) { result = "Amount must be > 0"; return false; }
        if (buyer.GetInventory(offerR) < offerAmt - 0.001f)
        {
            result = "Not enough " + MaterialCatalog.Name(offerR);
            return false;
        }

        float value = offerAmt * market.Price(offerR);
        float need = CompanyNodeValue(n);
        if (value <= need)
        {
            result = "Offer rejected: " + value.ToString("0") + " PP is not MORE than node value " + need.ToString("0") + " PP";
            return false;
        }

        buyer.SetInventory(offerR, buyer.GetInventory(offerR) - offerAmt);
        var seller = n.Owner;
        seller.Nodes.Remove(n);
        n.Owner = buyer;
        buyer.Nodes.Add(n);
        AddLog(buyer.Name + " bought " + n.Name + " from " + seller.Name
             + " for " + value.ToString("0") + " PP worth of " + MaterialCatalog.Name(offerR));
        CheckEnd();
        StateChanged();
        result = "ok";
        return true;
    }

    public bool BuyOutCompany(Company buyer, Company seller, ResourceType offerR, float offerAmt, out string result)
    {
        if (buyer == null || !buyer.Alive) { result = "Invalid buyer"; return false; }
        if (seller == null || seller == buyer) { result = "Invalid target"; return false; }
        if (!seller.Alive) { result = "That company is gone"; return false; }
        if (seller.Nodes.Count == 0) { result = "That company has no nodes"; return false; }
        if (offerAmt <= 0f) { result = "Amount must be > 0"; return false; }
        if (buyer.GetInventory(offerR) < offerAmt - 0.001f)
        {
            result = "Not enough " + MaterialCatalog.Name(offerR);
            return false;
        }

        float need = 0f;
        foreach (var n in seller.Nodes) need += CompanyNodeValue(n);
        float value = offerAmt * market.Price(offerR);
        if (value <= need)
        {
            result = "Offer rejected: " + value.ToString("0") + " PP is not MORE than company value " + need.ToString("0") + " PP";
            return false;
        }

        buyer.SetInventory(offerR, buyer.GetInventory(offerR) - offerAmt);
        foreach (var n in seller.Nodes)
        {
            n.Owner = buyer;
            buyer.Nodes.Add(n);
        }
        seller.Nodes.Clear();
        seller.Alive = false;
        AddLog(buyer.Name + " BOUGHT OUT " + seller.Name
             + " for " + value.ToString("0") + " PP worth of " + MaterialCatalog.Name(offerR));
        CheckEnd();
        StateChanged();
        result = "ok";
        return true;
    }

    // ================= MODULES (resource-hub-plan Q6-Q9) =================

    public bool BuildModule(Company c, Node n, ModuleType m, out string result)
    {
        if (n.Owner != c) { result = "Not your node"; return false; }
        if (!n.HasFreeSlot) { result = "No free module slots left (" + n.MaxSlots + " max)"; return false; }

        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            var r = (ResourceType)i;
            float need = MaterialCatalog.ModuleAmount(m, r);
            if (need > 0f && c.GetInventory(r) < need - 0.001f)
            {
                result = "Not enough " + MaterialCatalog.Name(r) + " (need " + need.ToString("0") + ")";
                return false;
            }
        }
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            var r = (ResourceType)i;
            float need = MaterialCatalog.ModuleAmount(m, r);
            if (need > 0f) c.SetInventory(r, c.GetInventory(r) - need);
        }

        if (m == ModuleType.ProductionBooster) n.ProductionModules++;
        else n.StorageModules++;
        AddLog(c.Name + " built " + m.ToString() + " on " + n.Name
             + " (" + n.ModuleCount + "/" + n.MaxSlots + " slots)");
        StateChanged();
        result = "ok";
        return true;
    }

    // ================= TRADING (trading-plan Q3/Q4/Q7) =================
    // Companies accept a trade only if you offer MORE value (in government PP prices)
    // than you request.

    public bool RequestTrade(Company from, Company to, ResourceType offerR, float offerAmt,
                             ResourceType reqR, float reqAmt, out string result)
    {
        if (from == null || to == null || from == to) { result = "Invalid trade"; return false; }
        if (!from.Alive || !to.Alive) { result = "A company is gone"; return false; }
        if (offerAmt <= 0f || reqAmt <= 0f) { result = "Amounts must be > 0"; return false; }
        if (from.GetInventory(offerR) < offerAmt - 0.001f)
        {
            result = "Not enough " + MaterialCatalog.Name(offerR);
            return false;
        }

        float offerValue = offerAmt * market.Price(offerR);
        float reqValue = reqAmt * market.Price(reqR);
        if (offerValue <= reqValue)
        {
            result = to.Name + " rejected: you offer " + offerValue.ToString("0")
                   + " PP but request " + reqValue.ToString("0") + " PP (you must offer MORE)";
            return false;
        }

        float space = Mathf.Max(0f,
            to.CapacityFor(reqR, config.BaseCapacityPerResource) - to.GetInventory(reqR));
        float take = Mathf.Min(reqAmt, space);
        if (take <= 0.001f)
        {
            result = to.Name + " has no storage space for " + MaterialCatalog.Name(reqR);
            return false;
        }

        from.SetInventory(offerR, from.GetInventory(offerR) - offerAmt);
        to.SetInventory(reqR, to.GetInventory(reqR) + take);
        AddLog(from.Name + " traded " + offerAmt.ToString("0") + " " + MaterialCatalog.Name(offerR)
             + " (" + offerValue.ToString("0") + " PP) for " + take.ToString("0") + " "
             + MaterialCatalog.Name(reqR) + " (" + reqValue.ToString("0") + " PP) with " + to.Name);
        StateChanged();
        result = "ok";
        return true;
    }

    // ================= AI (core-loop Q7) =================
    // Simple actions: expand (buy government nodes) and trade for resources they lack.

    void AIAct(Company ai)
    {
        // 1) expand: buy a government node when they can afford it
        if (GovernmentNodeCount() > 0 && Random.value < config.AIExpansionChance)
        {
            var node = FirstGovernmentNode();
            if (node != null)
            {
                string res;
                BuyGovernmentNode(ai, node, out res);
            }
        }

        // 2) trade: ask for a raw resource they lack, offer one they have plenty of
        if (Random.value < config.AITradeChance)
        {
            ResourceType need = MaterialCatalog.Raw[0];
            ResourceType have = MaterialCatalog.Raw[0];
            float bestNeed = float.MaxValue;
            float worstHave = float.MinValue;
            for (int i = 0; i < MaterialCatalog.Raw.Length; i++)
            {
                var r = MaterialCatalog.Raw[i];
                float cap = ai.CapacityFor(r, config.BaseCapacityPerResource);
                float ratio = ai.GetInventory(r) / cap;
                if (ratio < bestNeed) { bestNeed = ratio; need = r; }
                if (ratio > worstHave) { worstHave = ratio; have = r; }
            }
            if (need == have) return;
            if (ai.GetInventory(have) < 2f) return;

            Company target = null;
            foreach (var c in Companies)
            {
                if (c != ai && c.Alive && c.GetInventory(need) > 0.5f) { target = c; break; }
            }
            if (target == null) return;

            float reqAmt = 5f;
            float offerAmt = (reqAmt * market.Price(need) + 1f) / market.Price(have);
            offerAmt = Mathf.Min(offerAmt, ai.GetInventory(have));
            if (offerAmt * market.Price(have) <= reqAmt * market.Price(need)) return;
            string res;
            RequestTrade(ai, target, have, offerAmt, need, reqAmt, out res);
        }
    }

    // ================= WIN / LOSE (core-loop Q5/Q6/Q9) =================

    public void CheckEnd()
    {
        if (GameOver) return;
        foreach (var c in Companies)
        {
            if (!c.Alive) continue;
            int owned = 0;
            foreach (var n in Nodes) if (n.Owner == c) owned++;
            if (owned < Nodes.Count) continue;

            bool allGone = true;
            foreach (var o in Companies)
                if (o != c && o.Alive) { allGone = false; break; }
            if (!allGone) continue;

            GameOver = true;
            Winner = c;
            if (c.IsPlayer)
                GameOverText = "YOU WIN!\nYou own every node on the map - your company is the last monopoly in the country.";
            else
                GameOverText = c.Name + " became the monopoly and owns everything.\nYou lost.";
            AddLog("=== " + GameOverText.Replace("\n", " ") + " ===");
            return;
        }
    }

    // ================= HELPERS =================

    public void AddLog(string msg)
    {
        Log.Add(msg);
        if (Log.Count > 400) Log.RemoveRange(0, Log.Count - 400);
    }

    void StateChanged()
    {
        if (OnStateChanged != null) OnStateChanged();
    }
}
