using System.Globalization;
using UnityEngine;

/// <summary>
/// Visual + UI layer. Attach to ANY GameObject in the scene (it auto-creates
/// a GameSimulator if the scene has none).
///
/// - flat country map with colored ownership areas per company (core-loop Q4:
///   "colored plain area of what a company owns... expands according to what
///   nodes a company owns")
/// - IMGUI panels: your company (tax quota, extra tax, payment template),
///   government market (prices, companies), nodes (buy / build modules),
///   trade forms (trading-plan Q8), log, game-over screen
/// </summary>
public class GameView : MonoBehaviour
{
    [SerializeField] GameSimulator sim;

    GameSimulator S;
    GameObject mapRoot;
    Renderer[] nodeAreaRenderers;
    Renderer[] nodeMarkerRenderers;
    TextMesh[] nodeLabels;

    string statusMsg = "";

    // trade form state
    int tradeTarget = 1;
    int tradeOfferRes = 0;
    float tradeOfferAmt = 10f;
    int tradeReqRes = 1;
    float tradeReqAmt = 10f;

    int buyNodeIndex = -1;
    int buyNodeOfferRes = 0;
    float buyNodeOfferAmt = 20f;

    int buyOutIndex = 1;
    int buyOutOfferRes = 0;
    float buyOutOfferAmt = 100f;

    GUIStyle _bold;
    GUIStyle Bold
    {
        get
        {
            if (_bold == null)
                _bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            return _bold;
        }
    }

    void Awake()
    {
        S = sim;
        if (S == null) S = GetComponent<GameSimulator>();
        if (S == null)
        {
            var go = new GameObject("GameSimulator");
            go.AddComponent<GameSimulator>();
            S = go.GetComponent<GameSimulator>();
        }
        if (S.OnStateChanged != null) S.OnStateChanged -= Refresh;
        S.OnStateChanged += Refresh;
    }

    void Start()
    {
        if (S.Nodes.Count == 0) S.NewGame();
        BuildMap();
        SetupCamera();
    }

    void OnDestroy()
    {
        if (S != null && S.OnStateChanged != null) S.OnStateChanged -= Refresh;
    }

    // ================= MAP (core-loop Q4) =================

    void BuildMap()
    {
        if (mapRoot != null) Destroy(mapRoot);
        mapRoot = new GameObject("NodeMap");

        int count = S.Nodes.Count;
        nodeAreaRenderers = new Renderer[count];
        nodeMarkerRenderers = new Renderer[count];
        nodeLabels = new TextMesh[count];

        for (int i = 0; i < count; i++)
        {
            var n = S.Nodes[i];

            // colored plain = ownership area
            var area = GameObject.CreatePrimitive(PrimitiveType.Plane);
            area.name = "Area_" + n.Id;
            area.transform.SetParent(mapRoot.transform, false);
            area.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            area.transform.position = new Vector3(n.Position.x, 0f, n.Position.y);
            RemoveCollider(area);
            SetColor(area, n.Owner != null ? n.Owner.Color : GameSimulator.GovernmentColor);
            nodeAreaRenderers[i] = area.GetComponent<Renderer>();

            // node marker: cylinder = raw hub, cube = factory
            var marker = GameObject.CreatePrimitive(n.IsFactory ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            marker.name = "Node_" + n.Id;
            marker.transform.SetParent(area.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            marker.transform.localScale = new Vector3(0.7f, 1.4f, 0.7f);
            RemoveCollider(marker);
            SetColor(marker, Brighten(n.Owner != null ? n.Owner.Color : GameSimulator.GovernmentColor, 1.25f));
            nodeMarkerRenderers[i] = marker.GetComponent<Renderer>();

            // label
            var labelGo = new GameObject("Label_" + n.Id);
            labelGo.transform.SetParent(area.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            var f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null) tm.font = f;
            tm.text = ShortName(n);
            tm.fontSize = 40;
            tm.color = Color.white;
            tm.anchor = TextAnchor.UpperCenter;
            tm.alignment = TextAlignment.Center;
            nodeLabels[i] = tm;
        }
    }

    void Refresh()
    {
        if (S == null || mapRoot == null) return;
        for (int i = 0; i < S.Nodes.Count && i < nodeAreaRenderers.Length; i++)
        {
            var n = S.Nodes[i];
            Color c = n.Owner != null ? n.Owner.Color : GameSimulator.GovernmentColor;
            nodeAreaRenderers[i].material.color = c;
            nodeMarkerRenderers[i].material.color = Brighten(c, 1.25f);
            string shortName = ShortName(n);
            if (nodeLabels[i] != null && nodeLabels[i].text != shortName)
                nodeLabels[i].text = shortName;
        }
    }

    string ShortName(Node n)
    {
        string s = (n.Id + 1) + " ";
        for (int i = 0; i < n.Produced.Count && i < 2; i++)
        {
            if (i > 0) s += "+";
            s += MaterialCatalog.Code(n.Produced[i]);
        }
        if (n.IsFactory) s += "F";
        return s;
    }

    void RemoveCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
    }

    void SetColor(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var mat = r.sharedMaterial;
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            mat = new Material(sh);
            r.sharedMaterial = mat;
        }
        mat.color = c;
    }

    Color Brighten(Color c, float f)
    {
        return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), 1f);
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }
        // map is 5 columns x 4 rows, spacing 5 -> center at (10, 0, 7.5)
        cam.transform.position = new Vector3(10f, 32f, -14f);
        cam.transform.LookAt(new Vector3(10f, 0f, 7.5f));
        cam.nearClipPlane = 0.5f;
    }

    // ================= UI =================

    void OnGUI()
    {
        if (S == null || S.Nodes.Count == 0)
        {
            GUILayout.Label("Starting game...");
            return;
        }

        // ---- top bar ----
        GUILayout.BeginHorizontal("box");
        GUILayout.Label("Tick " + S.TickCount, GUILayout.Width(70));
        GUILayout.Label("Tax in " + S.TicksUntilTax, GUILayout.Width(70));
        GUILayout.Label("Speed " + S.config.TickSeconds.ToString("0") + "s", GUILayout.Width(70));
        S.config.TickSeconds = Mathf.Clamp(
            GUILayout.HorizontalSlider(S.config.TickSeconds, 1f, 120f, GUILayout.Width(160)), 1f, 120f);
        if (GUILayout.Button("TICK", GUILayout.Width(55))) S.ForceTick();
        if (GUILayout.Button("TAX NOW", GUILayout.Width(80))) S.ForceTax();
        if (GUILayout.Button("RESTART", GUILayout.Width(80))) S.NewGame();
        GUILayout.EndHorizontal();

        if (statusMsg != "")
            GUILayout.Label(statusMsg, GUILayout.Width(1000));

        // ---- main panels ----
        GUILayout.BeginHorizontal();
        CompanyPanel();
        MarketPanel();
        NodePanel();
        GUILayout.EndHorizontal();

        // ---- log ----
        LogPanel();

        if (S.GameOver) GameOverPanel();
    }

    void CompanyPanel()
    {
        var p = S.Player;
        if (p == null) return;

        GUILayout.BeginVertical("box", GUILayout.Width(330));
        GUILayout.Label("YOUR COMPANY", Bold);
        GUILayout.Label("Political Power: " + p.PoliticalPower.ToString("0") + " PP");
        GUILayout.Label("Nodes owned: " + p.OwnedNodeCount);

        float penalty = Mathf.Pow(1f + S.config.PenaltyPerFail, p.FailedTaxesInARow);
        float baseQuota = p.ProducedValueThisCycle * S.config.TaxRate * penalty;
        float quota = baseQuota * (1f + p.ExtraTaxPct);
        GUILayout.Label("Production value this cycle: " + p.ProducedValueThisCycle.ToString("0") + " PP");
        GUILayout.Label("TAX DUE AT NEXT TICK: ~" + quota.ToString("0") + " PP");
        GUILayout.Label("Last tax: paid " + p.LastTaxPaid.ToString("0") + " / quota " + p.LastTaxQuota.ToString("0"));
        GUILayout.Label("Failed taxes in a row: " + p.FailedTaxesInARow + " / " + S.config.BankruptAtFails);

        GUILayout.Space(6);
        GUILayout.Label("EXTRA TAX: " + Mathf.RoundToInt(p.ExtraTaxPct * 100f) + "% (bonus points)");
        p.ExtraTaxPct = Mathf.Clamp01(
            GUILayout.HorizontalSlider(p.ExtraTaxPct, 0f, 1f, GUILayout.Width(280)));

        GUILayout.Space(6);
        GUILayout.Label("PAY TAX WITH (priority order):");
        for (int idx = 0; idx < p.PaymentPriority.Count; idx++)
        {
            var r = p.PaymentPriority[idx];
            GUILayout.BeginHorizontal();
            GUILayout.Label((idx + 1) + ". " + MaterialCatalog.Name(r), GUILayout.Width(200));
            if (GUILayout.Button("<", "minibutton", GUILayout.Width(28)))
            {
                if (idx > 0)
                {
                    var tmp = p.PaymentPriority[idx - 1];
                    p.PaymentPriority[idx - 1] = r;
                    p.PaymentPriority[idx] = tmp;
                }
            }
            if (GUILayout.Button(">", "minibutton", GUILayout.Width(28)))
            {
                if (idx < p.PaymentPriority.Count - 1)
                {
                    var tmp = p.PaymentPriority[idx + 1];
                    p.PaymentPriority[idx + 1] = r;
                    p.PaymentPriority[idx] = tmp;
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);
        GUILayout.Label("INVENTORY (limit):");
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            var r = (ResourceType)i;
            GUILayout.Label("  " + MaterialCatalog.Name(r) + ": " + p.GetInventory(r).ToString("0")
                + " / " + p.CapacityFor(r, S.config.BaseCapacityPerResource).ToString("0"));
        }
        GUILayout.Label("Gov node cost now: " + S.GovernmentNodeCost(p).ToString("0") + " PP");
        GUILayout.EndVertical();
    }

    void MarketPanel()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(280));
        GUILayout.Label("GOVERNMENT PRICES (PP/unit)", Bold);
        GUILayout.Label("Special this cycle: " + MaterialCatalog.Name(S.market.SpecialResource) + " (2x)");
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            var r = (ResourceType)i;
            string star = (r == S.market.SpecialResource) ? "  *" : "";
            GUILayout.Label("  " + MaterialCatalog.Name(r) + ": " + S.market.Price(r).ToString("0") + star);
        }

        GUILayout.Space(6);
        GUILayout.Label("COMPANIES", Bold);
        for (int i = 0; i < S.Companies.Count; i++)
        {
            var c = S.Companies[i];
            string state = c.Alive ? "alive" : "GONE";
            GUILayout.Label("  " + c.Name + " [" + state + "]  " + c.OwnedNodeCount
                + " nodes, " + c.PoliticalPower.ToString("0") + " PP");
        }
        GUILayout.Space(4);
        GUILayout.Label("Free government nodes: " + S.GovernmentNodeCount());
        GUILayout.EndVertical();
    }

    void NodePanel()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(470));

        GUILayout.Label("NODES", Bold);
        for (int i = 0; i < S.Nodes.Count; i++)
        {
            var n = S.Nodes[i];
            string owner = n.Owner != null ? n.Owner.Name : "GOVERNMENT";
            string info = (n.Id + 1) + ". " + n.ProducesText()
                + "  " + n.ProductionPerTick(n.Produced[0]).ToString("0") + "/tick  " + owner;
            GUILayout.BeginHorizontal();
            GUILayout.Label(info, GUILayout.MinWidth(250));

            if (n.Owner == null)
            {
                var p = S.Player;
                float cost = S.GovernmentNodeCost(p);
                if (GUILayout.Button("Buy " + cost.ToString("0") + "PP", GUILayout.Width(110)))
                {
                    string res;
                    if (!S.BuyGovernmentNode(p, n, out res)) statusMsg = res;
                }
            }
            else if (n.Owner == S.Player)
            {
                if (GUILayout.Button("Speed 2x", "minibutton", GUILayout.Width(70)))
                {
                    string res;
                    if (!S.BuildModule(S.Player, n, ModuleType.ProductionBooster, out res)) statusMsg = res;
                }
                if (GUILayout.Button("Store 2x", "minibutton", GUILayout.Width(70)))
                {
                    string res;
                    if (!S.BuildModule(S.Player, n, ModuleType.StorageBooster, out res)) statusMsg = res;
                }
                GUILayout.Label(n.ModuleCount + "/" + n.MaxSlots + " slots", GUILayout.Width(80));
            }
            else
            {
                if (GUILayout.Button("Buy...", GUILayout.Width(60)))
                {
                    buyNodeIndex = i;
                    statusMsg = "Selected node " + (n.Id + 1) + " - use the BUY NODE form below.";
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4);
        GUILayout.Label("MODULE COSTS  Speed: " + MaterialCatalog.ModuleRecipeText(ModuleType.ProductionBooster)
            + "   |   Storage: " + MaterialCatalog.ModuleRecipeText(ModuleType.StorageBooster));

        GUILayout.Space(8);
        GUILayout.Label("TRADE RESOURCES (offer MORE value than you request)", Bold);
        tradeTarget = CompanyRow(tradeTarget);
        tradeOfferRes = ResourceRow("Offer:", tradeOfferRes);
        tradeOfferAmt = AmountRow("", tradeOfferAmt);
        tradeReqRes = ResourceRow("Get:", tradeReqRes);
        tradeReqAmt = AmountRow("", tradeReqAmt);
        if (S.Companies.Count > tradeTarget)
        {
            var t = S.Companies[tradeTarget];
            float ov = tradeOfferAmt * S.market.Price((ResourceType)tradeOfferRes);
            float rv = tradeReqAmt * S.market.Price((ResourceType)tradeReqRes);
            GUILayout.Label("Offer " + ov.ToString("0") + " PP vs request " + rv.ToString("0") + " PP  "
                + ((ov > rv) ? "ACCEPTED" : "TOO LOW"), Bold);
            if (GUILayout.Button("SEND TRADE REQUEST", GUILayout.Width(160)))
            {
                string res;
                if (!S.RequestTrade(S.Player, t, (ResourceType)tradeOfferRes, tradeOfferAmt,
                                    (ResourceType)tradeReqRes, tradeReqAmt, out res))
                    statusMsg = res;
            }
        }

        if (buyNodeIndex >= 0 && buyNodeIndex < S.Nodes.Count)
        {
            var n = S.Nodes[buyNodeIndex];
            if (n.Owner != null && n.Owner != S.Player)
            {
                GUILayout.Space(8);
                GUILayout.Label("BUY NODE " + (n.Id + 1) + " from " + n.Owner.Name, Bold);
                buyNodeOfferRes = ResourceRow("Offer:", buyNodeOfferRes);
                buyNodeOfferAmt = AmountRow("", buyNodeOfferAmt);
                float v = buyNodeOfferAmt * S.market.Price((ResourceType)buyNodeOfferRes);
                float need = S.CompanyNodeValue(n);
                GUILayout.Label("Offer " + v.ToString("0") + " PP vs node " + need.ToString("0") + " PP  "
                    + ((v > need) ? "ACCEPTED" : "TOO LOW"), Bold);
                if (GUILayout.Button("SEND OFFER", GUILayout.Width(160)))
                {
                    string res;
                    if (!S.BuyNode(S.Player, n, (ResourceType)buyNodeOfferRes, buyNodeOfferAmt, out res))
                        statusMsg = res;
                    else buyNodeIndex = -1;
                }
            }
        }

        GUILayout.Space(8);
        GUILayout.Label("BUY OUT A COMPANY", Bold);
        buyOutIndex = CompanyRow(buyOutIndex);
        buyOutOfferRes = ResourceRow("Offer:", buyOutOfferRes);
        buyOutOfferAmt = AmountRow("", buyOutOfferAmt);
        if (S.Companies.Count > buyOutIndex)
        {
            var seller = S.Companies[buyOutIndex];
            if (seller.Alive && seller != S.Player)
            {
                float v = buyOutOfferAmt * S.market.Price((ResourceType)buyOutOfferRes);
                float need = 0f;
                foreach (var nn in seller.Nodes) need += S.CompanyNodeValue(nn);
                GUILayout.Label("Offer " + v.ToString("0") + " PP vs company " + need.ToString("0") + " PP  "
                    + ((v > need) ? "ACCEPTED" : "TOO LOW"), Bold);
                if (GUILayout.Button("SEND OFFER", GUILayout.Width(160)))
                {
                    string res;
                    if (!S.BuyOutCompany(S.Player, seller, (ResourceType)buyOutOfferRes,
                                         buyOutOfferAmt, out res))
                        statusMsg = res;
                }
            }
        }
        GUILayout.EndVertical();
    }

    void LogPanel()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(1000));
        var L = S.Log;
        int from = Mathf.Max(0, L.Count - 12);
        for (int i = from; i < L.Count; i++)
            GUILayout.Label(L[i]);
        GUILayout.EndVertical();
    }

    void GameOverPanel()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(Screen.width / 2f - 260f, Screen.height / 2f - 110f, 520, 220), "box");
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        GUILayout.Label(S.GameOverText, style);
        if (GUILayout.Button("PLAY AGAIN", GUILayout.Height(40)))
        {
            S.NewGame();
            statusMsg = "";
        }
        GUILayout.EndArea();
    }

    // ================= UI helpers =================

    int ResourceRow(string label, int current)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(55));
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            if (GUILayout.Button(MaterialCatalog.Code((ResourceType)i),
                (i == current) ? "button" : "minibutton", GUILayout.Width(34)))
                current = i;
        }
        GUILayout.EndHorizontal();
        return current;
    }

    float AmountRow(string label, float current)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(55));
        string txt = GUILayout.TextField(current.ToString("0"), GUILayout.Width(60));
        float v;
        if (float.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0f)
            current = v;
        GUILayout.EndHorizontal();
        return current;
    }

    int CompanyRow(int current)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("With:", GUILayout.Width(55));
        for (int i = 0; i < S.Companies.Count; i++)
        {
            var c = S.Companies[i];
            if (c.IsPlayer) continue;
            if (!c.Alive)
            {
                GUILayout.Label(c.Name + " (gone)", GUILayout.Width(110));
                continue;
            }
            if (GUILayout.Button(c.Name, (i == current) ? "button" : "minibutton", GUILayout.Width(110)))
                current = i;
        }
        GUILayout.EndHorizontal();
        return current;
    }
}
