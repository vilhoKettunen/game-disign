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
    [Header("Camera (node-prefabs-camera-plan section 5)")]
    [Tooltip("Optional: leave empty to auto-find/create one on the main camera.")]
    CameraController cameraController;

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

    GUIStyle _small;
    GUIStyle Small
    {
        get
        {
            if (_small == null)
                _small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            return _small;
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
        SetupCameraController();
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
            SetColor(area, GameSimulator.ColorOf(n));
            nodeAreaRenderers[i] = area.GetComponent<Renderer>();

            // node marker: cylinder = raw hub, cube = factory
            var marker = GameObject.CreatePrimitive(n.IsFactory ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            marker.name = "Node_" + n.Id;
            marker.transform.SetParent(area.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            marker.transform.localScale = new Vector3(0.7f, 1.4f, 0.7f);
            RemoveCollider(marker);
            SetColor(marker, Brighten(GameSimulator.ColorOf(n), 1.25f));
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
            tm.color = Brighten(GameSimulator.ColorOf(n), 1.25f);
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
            Color c = GameSimulator.ColorOf(n);
            nodeAreaRenderers[i].material.color = c;
            nodeMarkerRenderers[i].material.color = Brighten(c, 1.25f);
            string shortName = ShortName(n);
            if (nodeLabels[i] != null)
            {
                nodeLabels[i].color = Brighten(c, 1.25f);
                if (nodeLabels[i].text != shortName)
                    nodeLabels[i].text = shortName;
            }
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
        // initial framing only - CameraController (node-prefabs-camera-plan
        // section 5) drives the camera from here on
        cam.transform.position = new Vector3(10f, 32f, -14f);
        cam.transform.LookAt(new Vector3(10f, 0f, 7.5f));
        cam.nearClipPlane = 0.5f;
    }

    /// <summary>
    /// Camera movement (node-prefabs-camera-plan.txt section 5): find or create
    /// a CameraController and hand it the map extent so pan/zoom stay inside
    /// the map. The controller auto-creates itself, so nothing needs wiring.
    /// </summary>
    void SetupCameraController()
    {
        var cc = (cameraController != null) ? cameraController : CameraController.FindOrCreate();
        if (cc == null) return;

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < S.Nodes.Count; i++)
        {
            var p = S.Nodes[i].Position;
            min = new Vector2(Mathf.Min(min.x, p.x), Mathf.Min(min.y, p.y));
            max = new Vector2(Mathf.Max(max.x, p.x), Mathf.Max(max.y, p.y));
        }
        cc.SetMapBounds(min, max);
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
        LoadToggles();
        GUILayout.BeginHorizontal("box");
        GUILayout.Label("Tick " + S.TickCount, GUILayout.Width(70));
        GUILayout.Label("Tax in " + S.TicksUntilTax, GUILayout.Width(70));
        GUILayout.Label("Speed " + S.config.TickSeconds.ToString("0") + "s", GUILayout.Width(70));
        S.config.TickSeconds = Mathf.Clamp(
            GUILayout.HorizontalSlider(S.config.TickSeconds, 1f, 120f, GUILayout.Width(160)), 1f, 120f);
        if (GUILayout.Button("TICK", GUILayout.Width(55))) S.ForceTick();
        if (GUILayout.Button("TAX NOW", GUILayout.Width(80))) S.ForceTax();
        if (GUILayout.Button("RESTART", GUILayout.Width(80))) S.NewGame();
        GUILayout.Space(12);

        // panel toggles (choice is remembered between games)
        bool prevC = showCompany, prevP = showPrices, prevN = showNodes;
        showCompany = GUILayout.Toggle(showCompany, "Company", GUILayout.Width(84));
        showPrices  = GUILayout.Toggle(showPrices,  "Prices",  GUILayout.Width(70));
        showNodes   = GUILayout.Toggle(showNodes,   "Nodes",   GUILayout.Width(70));
        if (showCompany != prevC || showPrices != prevP || showNodes != prevN) SaveToggles();
        if (GUILayout.Button(showInfo ? "INFO ✕" : "INFO ?", GUILayout.Width(75)))
            showInfo = !showInfo;
        GUILayout.EndHorizontal();

        if (statusMsg != "")
            GUILayout.Label(statusMsg, GUILayout.Width(1000));

        // ---- main panels (only the visible ones) ----
        if (showCompany || showPrices || showNodes)
        {
            GUILayout.BeginHorizontal();
            if (showCompany) CompanyPanel();
            if (showPrices)  MarketPanel();
            if (showNodes)   NodePanel();
            GUILayout.EndHorizontal();
        }

        // ---- log ----
        LogPanel();

        if (S.GameOver) GameOverPanel();
        else if (showInfo) InfoOverlay();
    }

    void CompanyPanel()
    {
        var p = S.Player;
        if (p == null) return;

        GUILayout.BeginVertical("box", GUILayout.Width(330));
        GUILayout.Label("YOUR COMPANY", BoldTinted(p.Color));
        GUILayout.Label("Political Power: " + p.PoliticalPower.ToString("0") + " PP");
        GUILayout.Label("Nodes owned: " + p.OwnedNodeCount);

        float quota = S.UpcomingTaxQuota(p);
        float invValue = p.InventoryValue(S.market);
        GUILayout.Label("Production value this cycle: " + p.ProducedValueThisCycle.ToString("0") + " PP");
        GUILayout.Label("Inventory value: ~" + invValue.ToString("0") + " PP");
        GUILayout.Label("TAX DUE AT NEXT CYCLE: ~" + quota.ToString("0") + " PP  (30% of production this cycle)");
        if (invValue < quota - 0.001f)
            GUILayout.Label("  !! Inventory below tax due -> tax will FAIL and your WHOLE inventory is taken", RedStyle());
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
            if (GUILayout.Button("▲", "minibutton", GUILayout.Width(28)))
            {
                if (idx > 0)
                {
                    var tmp = p.PaymentPriority[idx - 1];
                    p.PaymentPriority[idx - 1] = r;
                    p.PaymentPriority[idx] = tmp;
                }
            }
            if (GUILayout.Button("▼", "minibutton", GUILayout.Width(28)))
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
        GUILayout.Label("INVENTORY  (prod/con per tick | stock/limit)");
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            var r = (ResourceType)i;
            float prod = S.ProducedPerTick(p, r);
            float cons = S.ConsumedPerTick(p, r);
            string flow = (prod > 0f || cons > 0f)
                ? "  " + prod.ToString("0") + "/" + cons.ToString("0") + " per tick"
                : "  - (unused)";
            GUILayout.Label("  " + MaterialCatalog.Name(r) + flow + "   " + p.GetInventory(r).ToString("0")
                + " / " + p.CapacityFor(r, S.config.BaseCapacityPerResource).ToString("0"));
        }
        GUILayout.Label("Total inventory value: ~" + p.InventoryValue(S.market).ToString("0") + " PP  (tax is taken from this)");
        GUILayout.Label("Gov node cost now: " + S.GovernmentNodeCost(p).ToString("0") + " PP");
        GUILayout.EndVertical();
    }

    void MarketPanel()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(280));
        GUILayout.Label("GOVERNMENT PRICES (PP/unit)", Bold);
        GUILayout.Label("red = cheap, green = expensive", Small);
        GUILayout.Label("Special this cycle: " + MaterialCatalog.Name(S.market.SpecialResource) + " (2x)");
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            var r = (ResourceType)i;
            string star = (r == S.market.SpecialResource) ? "  *" : "";
            GUILayout.Label("  " + MaterialCatalog.Name(r) + ": " + S.market.Price(r).ToString("0") + star, HeatPriceStyle(i));
        }

        GUILayout.Space(6);
        GUILayout.Label("COMPANIES  (each color = the company and its nodes)", Small);
        for (int i = 0; i < S.Companies.Count; i++)
        {
            var c = S.Companies[i];
            string state = c.Alive ? "alive" : "GONE";
            GUILayout.Label("  " + c.Name + " [" + state + "]  " + c.OwnedNodeCount
                + " nodes, " + c.PoliticalPower.ToString("0") + " PP",
                (c.Alive) ? CompanyColorStyle(c.Color) : GUI.skin.label);
        }
        GUILayout.Space(4);
        GUILayout.Label("Free government nodes: " + S.GovernmentNodeCount());
        GUILayout.EndVertical();
    }

    void NodePanel()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(470));

        GUILayout.Label("NODES  (color = owner)", Bold);
        for (int i = 0; i < S.Nodes.Count; i++)
        {
            var n = S.Nodes[i];
            string owner = n.Owner != null ? n.Owner.Name : "GOVERNMENT";
            Color ownerColor = GameSimulator.ColorOf(n);
            string info = (n.Id + 1) + ". " + n.ProducesText()
                + "  " + n.ProductionPerTick(n.Produced[0]).ToString("0") + "/tick";
            GUILayout.BeginHorizontal();
            GUILayout.Label(info, GUILayout.MinWidth(230));
            GUILayout.Label(owner, CompanyColorStyle(ownerColor), GUILayout.Width(96));

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
                GUILayout.BeginHorizontal();
                GUILayout.Label("BUY NODE " + (n.Id + 1) + " from ", Bold);
                GUILayout.Label(n.Owner.Name, BoldTinted(n.Owner.Color));
                GUILayout.EndHorizontal();
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
        if (S.Winner != null && S.Winner.IsPlayer)
        {
            style.normal.textColor = Brighten(S.Winner.Color, 1.4f);
            GUILayout.Label(S.GameOverText, style);
            style.normal.textColor = Color.white;
        }
        else if (S.Winner != null)
        {
            int idx = S.GameOverText.IndexOf(S.Winner.Name, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                GUILayout.Label(S.GameOverText, style);
            else
            {
                style.normal.textColor = Brighten(S.Winner.Color, 1.4f);
                GUILayout.Label(S.GameOverText.Substring(0, idx), style);
                GUILayout.Label(S.Winner.Name, style);
                GUILayout.Label(S.GameOverText.Substring(idx + S.Winner.Name.Length), style);
                style.normal.textColor = Color.white;
            }
        }
        else
        {
            GUILayout.Label(S.GameOverText, style);
        }
        if (GUILayout.Button("PLAY AGAIN", GUILayout.Height(40)))
        {
            S.NewGame();
            statusMsg = "";
        }
        GUILayout.EndArea();
    }

    // ================= UI helpers =================

    /// <summary>Brightens a company color when it's too dark to read as text on the UI background.</summary>
    Color ReadableColor(Color c)
    {
        Color t = c;
        float max = Mathf.Max(t.r, Mathf.Max(t.g, t.b));
        if (max < 0.6f)
            t = Brighten(t, 0.6f / max);
        return t;
    }

    /// <summary>Label style tinted with a company's color (darker colors are brightened for readability).</summary>
    GUIStyle CompanyColorStyle(Color c)
    {
        if (_companyStyle == null) _companyStyle = new GUIStyle(GUI.skin.label);
        _companyStyle.normal.textColor = ReadableColor(c);
        return _companyStyle;
    }

    /// <summary>Bold label style tinted with a company's color.</summary>
    GUIStyle BoldTinted(Color c)
    {
        var s = new GUIStyle(Bold);
        s.normal.textColor = ReadableColor(c);
        return s;
    }

    GUIStyle _companyStyle;

    GUIStyle _redStyle;
    /// <summary>Bold red label style for warnings (e.g. inventory below the upcoming tax).</summary>
    GUIStyle RedStyle()
    {
        if (_redStyle == null)
        {
            _redStyle = new GUIStyle(Bold);
            _redStyle.normal.textColor = new Color(1f, 0.35f, 0.3f);
        }
        return _redStyle;
    }

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
                GUILayout.Label(c.Name + " (gone)", CompanyColorStyle(c.Color), GUILayout.Width(110));
                continue;
            }
            if (GUILayout.Button(c.Name, (i == current) ? "button" : "minibutton", GUILayout.Width(110)))
                current = i;
        }
        GUILayout.EndHorizontal();
        return current;
    }

    // ================= PANEL TOGGLES (persisted across restarts) =================

    static readonly string KeyCompany = "showCompany";
    static readonly string KeyPrices  = "showPrices";
    static readonly string KeyNodes   = "showNodes";

    bool showCompany = true;
    bool showPrices  = true;
    bool showNodes   = true;
    bool showInfo    = false;

    void LoadToggles()
    {
        if (PlayerPrefs.HasKey(KeyCompany)) showCompany = PlayerPrefs.GetInt(KeyCompany, 1) == 1;
        if (PlayerPrefs.HasKey(KeyPrices))  showPrices  = PlayerPrefs.GetInt(KeyPrices,  1) == 1;
        if (PlayerPrefs.HasKey(KeyNodes))   showNodes   = PlayerPrefs.GetInt(KeyNodes,   1) == 1;
    }

    void SaveToggles()
    {
        PlayerPrefs.SetInt(KeyCompany, showCompany ? 1 : 0);
        PlayerPrefs.SetInt(KeyPrices,  showPrices  ? 1 : 0);
        PlayerPrefs.SetInt(KeyNodes,   showNodes   ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ================= PRICE HEAT COLORS =================

    /// <summary>t: 0 = cheapest price this cycle, 1 = most expensive. Red = cheap, green = expensive, white = middle.</summary>
    Color HeatColor(float t)
    {
        t = Mathf.Clamp01(t);
        if (t <= 0.5f)
        {
            float r = 1f - t * 2f; // 1 at cheapest -> 0 at middle
            return Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), r);
        }
        float g = (t - 0.5f) * 2f; // 0 at middle -> 1 at most expensive
        return Color.Lerp(Color.white, new Color(0.3f, 1f, 0.35f), g);
    }

    GUIStyle _priceStyle;

    /// <summary>Label style with the heat color for resource <paramref name="res"/> (red=cheap, green=expensive).</summary>
    GUIStyle HeatPriceStyle(int res)
    {
        if (_priceStyle == null) _priceStyle = new GUIStyle(GUI.skin.label);
        _priceStyle.normal.textColor = HeatColor(PriceHeat((ResourceType)res));
        return _priceStyle;
    }

    GUIStyle _heatStyle;
    /// <summary>Label style colored by a fixed normalized heat value <paramref name="t"/> (0 cheap .. 1 expensive).</summary>
    GUIStyle HeatColorStyle(float t)
    {
        if (_heatStyle == null) _heatStyle = new GUIStyle(GUI.skin.label);
        _heatStyle.normal.textColor = HeatColor(t);
        return _heatStyle;
    }

    /// <summary>Normalised position of a resource's price this cycle: 0 = cheapest, 1 = most expensive.</summary>
    float PriceHeat(ResourceType res)
    {
        float minP = float.MaxValue, maxP = float.MinValue;
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            float pr = S.market.Price((ResourceType)i);
            if (pr < minP) minP = pr;
            if (pr > maxP) maxP = pr;
        }
        float p = S.market.Price(res);
        if (maxP - minP < 0.0001f) return 0.5f;
        return Mathf.Clamp01((p - minP) / (maxP - minP));
    }

    // ================= INFO / TUTORIAL OVERLAY (two tabs) =================

    int infoTab; // 0 = Gameplay, 1 = Economy
    Vector2 infoScroll;

    GUIStyle _infoHead;
    GUIStyle InfoHead
    {
        get
        {
            if (_infoHead == null)
                _infoHead = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            return _infoHead;
        }
    }

    GUIStyle _infoDark;
    GUIStyle InfoDark
    {
        get
        {
            if (_infoDark == null)
            {
                _infoDark = new GUIStyle("box")
                {
                    padding = { left = 10, right = 10, top = 8, bottom = 8 },
                    margin = { top = 4, bottom = 4 },
                };
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, new Color(0.13f, 0.14f, 0.17f));
                tex.Apply();
                _infoDark.normal.background = tex;
            }
            return _infoDark;
        }
    }

    void InfoOverlay()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = Mathf.Min(780f, Screen.width - 40f);
        float h = Mathf.Min(560f, Screen.height - 60f);
        GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), "box");

        // ---- tab buttons ----
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("  GAMEPLAY  ", infoTab == 0 ? "button" : "label", GUILayout.Width(120)))
            infoTab = 0;
        if (GUILayout.Button("  ECONOMY  ", infoTab == 1 ? "button" : "label", GUILayout.Width(120)))
            infoTab = 1;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(8);

        // ---- tab content (own dark, readable background; scrollable so it's always fully visible) ----
        var oldSkin = GUI.skin.scrollView;
        GUI.skin.scrollView = InfoDark;
        infoScroll = GUILayout.BeginScrollView(infoScroll, false, false);
        if (infoTab == 0) GameplayInfoTab();
        else EconomyInfoTab();
        GUILayout.EndScrollView();
        GUI.skin.scrollView = oldSkin;

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("CLOSE", GUILayout.Width(140)))
            showInfo = false;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    // ================= INFO TAB 1: GAMEPLAY =================

    void GameplayInfoTab()
    {
        GUILayout.Label("GAMEPLAY - WHAT YOU DO EVERY TICK", InfoHead);
        GUILayout.Space(4);

        GUILayout.Label("GOAL: own every node on the map and make every other company disappear.", Bold);
        GUILayout.Space(4);

        GUILayout.Label("1.  Each TICK your nodes produce resources straight into your inventory.");
        GUILayout.Label("2.  COLORS = OWNERSHIP. Every company has one fixed color: its name in the UI, its");
        GUILayout.Label("    nodes on the map, its entries in the panels, and the colored areas around them all");
        GUILayout.Label("    match. Green = you, red / blue / yellow = the AI companies, gray = the government.");
        GUILayout.Label("3.  RAW HUBS (cylinders) produce raw resources: Wood, Metal, Energy, Water.");
        GUILayout.Label("    FACTORIES (cubes) consume raw resources each tick and turn them into refined goods -");
        GUILayout.Label("    see the ECONOMY tab for the exact conversion rates.");
        GUILayout.Label("4.  TAXES are due every " + S.config.TaxEveryTicks + " ticks (see 'Tax in', top bar). The quota ACCUMULATES: it is "
            + Mathf.RoundToInt(S.config.TaxRate * 100f) + "% of ALL the production value of the whole tax cycle.");
        GUILayout.Label("    You pay with resources in YOUR priority order - use the up/down arrows in the [Company] panel to reorder it.");
        GUILayout.Label("5.  EXTRA TAX (slider in [Company]) = pay a little more now and earn bonus Political Power (PP) for it.");
        GUILayout.Label("6.  INVENTORY metrics: prod/con per tick shows how each resource moves (e.g. 4/2 = +4 in, -2 out, net +2 per tick);");
        GUILayout.Label("    stock/limit shows how full you are; TOTAL INVENTORY VALUE is what the tax can be taken from.");
        GUILayout.Label("    If your inventory value is BELOW the tax due, the tax FAILS and the government takes your WHOLE inventory.");
        GUILayout.Label("7.  Spend PP on government nodes, or offer resources worth MORE than a node / company is worth to buy it");
        GUILayout.Label("    from its owner ([Nodes] panel).");
        GUILayout.Label("8.  FAIL a tax " + S.config.BankruptAtFails + " times in a row and the government seizes your company - game over.");
        GUILayout.Space(4);
        GUILayout.Label("Tip: the top-bar checkboxes hide/show panels - your choice is remembered between games.", Small);
    }

    // ================= INFO TAB 2: ECONOMY =================

    void EconomyInfoTab()
    {
        GUILayout.Label("ECONOMY - MONEY, PRICES, RATES & UPGRADES", InfoHead);
        GUILayout.Space(4);

        // ---- money ----
        GUILayout.Label("MONEY: POLITICAL POWER (PP)", Bold);
        GUILayout.Label("PP is the government's value unit. You EARN it by paying taxes and SPEND it on");
        GUILayout.Label("government nodes and on out-bidding rival companies.");
        GUILayout.Space(4);

        // ---- taxes ----
        GUILayout.Label("TAXES (due every " + S.config.TaxEveryTicks + " ticks)", Bold);
        GUILayout.Label("quota = (value of what you produced THIS WHOLE tax cycle) x "
            + Mathf.RoundToInt(S.config.TaxRate * 100f) + "% x penalty x (1 + extra tax %)  -- the bill accumulates tick by tick");
        GUILayout.Label("paid from your INVENTORY (its total PP value). Inventory value < tax due = FAILED tax:");
        GUILayout.Label("the government takes your WHOLE inventory, and the next bill gets +"
            + Mathf.RoundToInt(S.config.PenaltyPerFail * 100f) + "% more");
        GUILayout.Label(S.config.BankruptAtFails + " fails in a row = bankruptcy. Paid tax returns 1 PP per PP (+1.5x on the extra part).");
        GUILayout.Space(4);

        // ---- prices & colors ----
        GUILayout.Label("MARKET PRICES (re-rolled every tax cycle)", Bold);
        GUILayout.Label("price = base x demand (0.75-1.5) x fluctuation (raw +/-20%, refined +/-50%) x special (2x)");
        GUILayout.Label("One random REFINED resource gets the 2x 'rare shortage' bonus each cycle (marked *).");
        GUILayout.BeginHorizontal();
        GUILayout.Label("  PRICE COLORS:  ", Bold);
        GUILayout.Label("CHEAP", HeatColorStyle(0f));
        GUILayout.Space(18);
        GUILayout.Label("AVERAGE", HeatColorStyle(0.5f));
        GUILayout.Space(18);
        GUILayout.Label("EXPENSIVE", HeatColorStyle(1f));
        GUILayout.EndHorizontal();
        for (int i = 0; i < MaterialCatalog.ResourceCount; i++)
        {
            var r = (ResourceType)i;
            string star = (r == S.market.SpecialResource) ? "   * 2x this cycle" : "";
            GUILayout.Label("   " + MaterialCatalog.Name(r) + ":  base " + MaterialCatalog.BasePrice(r).ToString("0")
                + "   |   now " + S.market.Price(r).ToString("0") + " PP/unit" + star, HeatPriceStyle(i));
        }
        GUILayout.Space(4);

        // ---- conversion rates ----
        GUILayout.Label("CONVERSION RATES - factories, per 1 refined unit produced", Bold);
        GUILayout.Label("   Chips                <=  2 Metal + 1 Energy");
        GUILayout.Label("   Mechanical Parts     <=  2 Metal + 1 Water");
        GUILayout.Label("   Building Materials   <=  1 Wood  + 1 Energy + 1 Water");
        GUILayout.Label("   Food                 <=  1 Energy + 1 Water");
        GUILayout.Space(4);

        // ---- upgrades ----
        GUILayout.Label("UPGRADES - MODULES (each node/factory has " + S.config.MaxModuleSlots + " slots, one use each)", Bold);
        GUILayout.Label("   PRODUCTION BOOSTER   costs  10 Chips + 10 Mechanical Parts   ->   2x production");
        GUILayout.Label("   STORAGE BOOSTER      costs  10 Building Materials + 10 Food   ->   2x storage on that node");
        GUILayout.Label("   Each module doubles (they stack: 1 = 2x, 2 = 4x, ...). Slots are permanent - no selling back.");
        GUILayout.Space(4);

        // ---- acquisition rules ----
        GUILayout.Label("ACQUIRING NODES & COMPANIES", Bold);
        GUILayout.Label("   Government node:  " + S.config.NodeBaseValue.ToString("0")
            + " PP + " + Mathf.RoundToInt(S.config.NodeCostGrowth * 100f) + "% per node you already own (it grows as you expand)");
        GUILayout.Label("   Company node:     offer resources worth MORE than the node's value, at today's prices");
        GUILayout.Label("   Company buyout:   offer resources worth MORE than ALL its nodes combined");
        GUILayout.Label("   Trades:           accepted only when you offer MORE value (PP) than you request");
    }
}
