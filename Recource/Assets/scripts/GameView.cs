using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

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
    Renderer[][] nodeMarkerRenderers;
    TextMesh[] nodeLabels;

    string statusMsg = "";

    // ---- node prefabs (node-prefabs-camera-plan section 3; author your art, drag it in) ----
    [Header("Node Prefabs (empty slot = placeholder shape from NodeShapes)")]
    public bool usePrefabs = true;
    [Tooltip("Raw hubs: Wood / Metal / Energy / Water (dual hubs show the two types in a checkerboard).")]
    public GameObject prefabWood;
    public GameObject prefabMetal;
    public GameObject prefabEnergy;
    public GameObject prefabWater;
    [Tooltip("Factories: Chips / Mech Parts / Building Materials / Food.")]
    public GameObject prefabChips;
    public GameObject prefabMechParts;
    public GameObject prefabBuilding;
    public GameObject prefabFood;

    // ---- map terrain (map-setup-plan Q8: ground for empty slots, water for lakes) ----
    [Header("Map Terrain (map-setup-plan Q8)")]
    [Tooltip("Render ground under every non-water cell so the map shape reads (off = nodes only).")]
    public bool renderGround = true;
    public Color groundColor = new Color(0.30f, 0.34f, 0.32f);
    public Color waterColor = new Color(0.14f, 0.26f, 0.42f);

    // map generation state (menu -> MapGenerator -> this scene)
    MapDefinition currentMap;
    List<Node> currentSpecs;
    Vector2 currentExtent;

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

    // ---- node info UI (click a node on the map -> panel on the right) ----
    int selectedNode = -1;   // node index whose info panel is open (-1 = none)
    int hoverNode = -1;      // node index under the cursor (highlighted)
    int nodeOfferRes = 0;    // resource offered when buying a company-owned node
    float nodeOfferAmt = 20f;

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
        // boot flow (map-setup-plan Q3/Q4): the menu scene is the start screen;
        // the game scene only auto-starts a game when the menu started it.
        if (!GameFlow.CameFromMenu)
        {
            if (Application.isBatchMode)
            {
                var s = new GameSetup();
                GameFlow.LastSetup = s;
                GameFlow.LastMap = null;
            }
            else
            {
                GameFlow.OpenMenu();
                return;
            }
        }
        if (GameFlow.LastSetup == null)
            GameFlow.LastSetup = new GameSetup();

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
        if (S.Nodes.Count == 0)
        {
            // map-setup-plan Q4: start from the menu's choice (map + seed + settings)
            Random.InitState(GameFlow.LastSetup.seed);
            MapGenerator.Generate(currentSpecs = new List<Node>(),
                                  GameFlow.LastMap, GameFlow.LastSetup);
            S.NewGameWithSpecs(currentSpecs, GameFlow.LastSetup);
        }
        currentMap = (GameFlow.LastMap != null) ? GameFlow.LastMap : null;
        BuildMap();
        SetupCamera();
        SetupCameraController();
    }

    /// <summary>
    /// Node picking (the "click a node -> node info UI" feature):
    ///   - LEFT-CLICK a node on the map opens its info panel (buy / build modules / buy out).
    ///   - LEFT-CLICK empty map space closes the panel.
    ///   - Hovering a node highlights its ownership area so the cursor has feedback.
    /// Uses the new Input System (Mouse). Picking is a simple raycast onto the y=0 map
    /// plane; the closest node within a pick radius wins. While an IMGUI control is
    /// being pressed (GUIUtility.hotControl != 0) picking is suppressed, exactly like
    /// CameraController does, so clicking the panels never selects a node behind them.
    /// </summary>
    void Update()
    {
        if (S == null || S.Nodes.Count == 0) return;
        var cam = Camera.main;
        if (cam == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        bool uiBusy = (GUIUtility.hotControl != 0);
        Vector2 m = mouse.position.value;

        // raycast onto the flat map plane (y = 0)
        Ray ray = cam.ScreenPointToRay(m);
        if (Mathf.Abs(ray.direction.y) < 1e-5f) return;
        float t = -cam.transform.position.y / ray.direction.y;
        if (t < 0f) return;
        Vector3 hit = ray.origin + ray.direction * t;

        // nearest node within the pick radius (~2.4 matches the ~4.5-unit node footprint,
        // and stays under half the 5-unit grid spacing so clicks in the gap don't select)
        int best = -1;
        float bestD = 2.4f;
        for (int i = 0; i < S.Nodes.Count; i++)
        {
            var n = S.Nodes[i];
            float d = Vector3.Distance(hit, new Vector3(n.Position.x, 0f, n.Position.y));
            if (d < bestD) { bestD = d; best = i; }
        }
        hoverNode = best;

        // visual feedback: reset every area to its base ownership color, then brighten
        // the hovered node and the selected node (so leaving a node fades it back out)
        if (mapRoot != null && nodeAreaRenderers != null)
        {
            for (int i = 0; i < S.Nodes.Count && i < nodeAreaRenderers.Length; i++)
            {
                Color c = GameSimulator.ColorOf(S.Nodes[i]);
                if (i == selectedNode) c = Brighten(c, 1.6f);
                else if (i == hoverNode) c = Brighten(c, 1.25f);
                nodeAreaRenderers[i].material.color = c;
            }
        }

        if (uiBusy) return; // a panel control is being used -> don't pick

        if (mouse.leftButton.wasPressedThisFrame)
        {
            selectedNode = (best >= 0) ? best : -1;
        }
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

        // ---- map terrain (map-setup-plan Q8): ground for empty slots, water for "0" cells ----
        BuildTerrain(mapRoot.transform);

        int count = S.Nodes.Count;
        nodeAreaRenderers = new Renderer[count];
        nodeMarkerRenderers = new Renderer[count][];
        nodeLabels = new TextMesh[count];

        for (int i = 0; i < count; i++)
        {
            var n = S.Nodes[i];

            // colored plain = ownership area (Q2: the prefab/shape carries the identity)
            var area = GameObject.CreatePrimitive(PrimitiveType.Plane);
            area.name = "Area_" + n.Id;
            area.transform.SetParent(mapRoot.transform, false);
            area.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            area.transform.position = new Vector3(n.Position.x, 0f, n.Position.y);
            RemoveCollider(area);
            SetColor(area, GameSimulator.ColorOf(n));
            nodeAreaRenderers[i] = area.GetComponent<Renderer>();

            // node body: your prefab if assigned (slot = Produced[0] + IsFactory),
            // else the placeholder shapes (node-prefabs-camera-plan Q10)
            var nodeGo = new GameObject("Node_" + n.Id);
            nodeGo.transform.SetParent(area.transform, false);
            nodeGo.transform.localPosition = Vector3.zero;
            nodeMarkerRenderers[i] = BuildNodeVisual(nodeGo.transform, n);

            // floating label above the node (Q8: type + production + owner color)
            var labelGo = new GameObject("Label_" + n.Id);
            labelGo.transform.SetParent(area.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 3.6f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            // Unity 6: "Arial.ttf" is no longer a valid built-in font -> use "LegacyRuntime.ttf"
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) tm.font = f;
            tm.text = ShortName(n);
            tm.fontSize = 40;
            tm.color = Brighten(GameSimulator.ColorOf(n), 1.25f);
            tm.anchor = TextAnchor.UpperCenter;
            tm.alignment = TextAlignment.Center;
            nodeLabels[i] = tm;
        }

        UpdateMapBounds();
    }

    /// <summary>
    /// Ground + water tiles for the current map (map-setup-plan Q8). Random
    /// maps have no shape -> a ground rectangle just large enough for the nodes.
    /// </summary>
    void BuildTerrain(Transform parent)
    {
        if (!renderGround && currentMap == null) return;

        if (currentMap != null)
        {
            float g = MapDefinition.GridSpacing;
            for (int r = 0; r < currentMap.Rows; r++)
            {
                for (int c = 0; c < currentMap.Cols; c++)
                {
                    char cell = currentMap.CellAt(r, c);
                    bool water = currentMap.IsWater(cell);
                    if (!water && !renderGround) continue;

                    var go = GameObject.CreatePrimitive(water ? PrimitiveType.Cube : PrimitiveType.Plane);
                    go.name = (water ? "Water_" : "Ground_") + r + "_" + c;
                    go.transform.SetParent(parent, false);
                    if (water)
                        go.transform.localScale = new Vector3(g * 0.98f, 0.2f, g * 0.98f);
                    else
                        go.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
                    go.transform.position = new Vector3(
                        (c - (currentMap.Cols - 1) * 0.5f) * g,
                        water ? -0.11f : (water ? 0f : 0.02f),
                        (r - (currentMap.Rows - 1) * 0.5f) * g);
                    RemoveCollider(go);
                    SetColor(go, water ? waterColor : groundColor);
                }
            }
        }
        else if (renderGround)
        {
            // random map: one big ground sheet under the node field
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < S.Nodes.Count; i++)
            {
                var p = S.Nodes[i].Position;
                min = new Vector2(Mathf.Min(min.x, p.x), Mathf.Min(min.y, p.y));
                max = new Vector2(Mathf.Max(max.x, p.x), Mathf.Max(max.y, p.y));
            }
            float w = Mathf.Max(MapDefinition.GridSpacing, max.x - min.x + 2f * MapDefinition.GridSpacing);
            float d = Mathf.Max(MapDefinition.GridSpacing, max.y - min.y + 2f * MapDefinition.GridSpacing);
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground_Random";
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(w / 10f, d / 10f, 1f);
            go.transform.position = new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);
            RemoveCollider(go);
            SetColor(go, groundColor);
        }
    }

    /// <summary>
    /// The node's body: your prefab if the slot is filled, else the placeholder
    /// shapes. Returns every part's renderer so Refresh() can tint ownership.
    /// </summary>
    Renderer[] BuildNodeVisual(Transform parent, Node n)
    {
        GameObject body = null;
        if (usePrefabs)
        {
            var p = PrefabFor(n);
            if (p != null)
            {
                body = Instantiate(p, parent);
                body.name = "Prefab_" + n.Id;
                body.transform.localPosition = Vector3.zero;
                // authoring notes (plan section 4): no colliders/scripts on the prefab;
                // strip defensively so the node stays click-transparent
                foreach (var c in body.GetComponents<Collider>()) Destroy(c);
            }
        }
        if (body == null)
            body = NodeShapes.Build(parent, n);

        var parts = body.GetComponentsInChildren<Renderer>();
        var list = new Renderer[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].sharedMaterial == null) continue;
            parts[i].material.color = Brighten(GameSimulator.ColorOf(n), 1.25f);
            list[i] = parts[i];
        }
        return list;
    }

    /// <summary>Slot = Produced[0] + IsFactory (node-prefabs-camera-plan section 3).</summary>
    GameObject PrefabFor(Node n)
    {
        if (n.Produced.Count == 0) return null;
        var r = n.Produced[0];
        if (n.IsFactory)
        {
            switch (r)
            {
                case ResourceType.Chips: return prefabChips;
                case ResourceType.MechanicalParts: return prefabMechParts;
                case ResourceType.BuildingMaterials: return prefabBuilding;
                case ResourceType.Food: return prefabFood;
            }
        }
        else
        {
            switch (r)
            {
                case ResourceType.Wood: return prefabWood;
                case ResourceType.Metal: return prefabMetal;
                case ResourceType.Energy: return prefabEnergy;
                case ResourceType.Water: return prefabWater;
            }
        }
        return null;
    }

    void UpdateMapBounds()
    {
        var cc = CameraController.Instance;
        if (cc == null) return;
        if (S == null || S.Nodes.Count == 0)
        {
            cc.SetMapBounds(Vector2.zero, new Vector2(10f, 10f));
            return;
        }
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < S.Nodes.Count; i++)
        {
            var p = S.Nodes[i].Position;
            min = new Vector2(Mathf.Min(min.x, p.x), Mathf.Min(min.y, p.y));
            max = new Vector2(Mathf.Max(max.x, p.x), Mathf.Max(max.y, p.y));
        }
        if (currentMap != null)
        {
            // include the shape edges (water + ground read as map)
            Vector2 emin = currentMap.CellCenter(0, 0);
            Vector2 emax = currentMap.CellCenter(currentMap.Rows - 1, currentMap.Cols - 1);
            min = new Vector2(Mathf.Min(min.x, emin.x - 2.5f), Mathf.Min(min.y, emin.y - 2.5f));
            max = new Vector2(Mathf.Max(max.x, emax.x + 2.5f), Mathf.Max(max.y, emax.y + 2.5f));
        }
        currentExtent = max - min;
        cc.SetMapBounds(min, max);
    }

    void Refresh()
    {
        if (S == null || mapRoot == null) return;
        for (int i = 0; i < S.Nodes.Count && i < nodeAreaRenderers.Length; i++)
        {
            var n = S.Nodes[i];
            Color c = GameSimulator.ColorOf(n);
            nodeAreaRenderers[i].material.color = c;
            if (nodeMarkerRenderers[i] != null)
            {
                for (int p = 0; p < nodeMarkerRenderers[i].Length; p++)
                    if (nodeMarkerRenderers[i][p] != null)
                        nodeMarkerRenderers[i][p].material.color = Brighten(c, 1.25f);
            }
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
            // menu -> game scene: the game hasn't started yet (map-setup-plan Q4)
            GUILayout.Label("Starting game...");
            if (GUILayout.Button("BACK TO MENU", GUILayout.Width(160))) GameFlow.OpenMenu();
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
        // map-setup-plan Q4: "back to menu" goes back to the setup screen
        if (GUILayout.Button("MENU", GUILayout.Width(70))) GameFlow.OpenMenu();
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
        // The node info panel is a separate docked panel: it shows whenever a node is
        // selected on the map, independent of the Company/Prices/Nodes toggles, so it
        // survives the later removal of the old [Nodes] section.
        bool nodeUiOpen = (selectedNode >= 0 && selectedNode < S.Nodes.Count);
        if (showCompany || showPrices || showNodes || nodeUiOpen)
        {
            GUILayout.BeginHorizontal();
            if (showCompany) CompanyPanel();
            if (showPrices)  MarketPanel();
            if (showNodes)   NodePanel();
            if (nodeUiOpen)  NodeUiPanel(S.Nodes[selectedNode]);
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

    /// <summary>
    /// Node info panel - generated when a node is clicked on the map (the
    /// "node-specific UI" that will eventually replace the old [Nodes] section).
    /// Shows the node's identity + owner, and the actions available for that owner:
    ///   - government node  -> buy with PP
    ///   - your node        -> build Speed/Storage modules (upgrade)
    ///   - another company  -> buy with a resource offer
    /// </summary>
    void NodeUiPanel(Node n)
    {
        string owner = n.Owner != null ? n.Owner.Name : "GOVERNMENT";
        Color ownerColor = GameSimulator.ColorOf(n);

        GUILayout.BeginVertical("box", GUILayout.Width(300));

        GUILayout.BeginHorizontal();
        GUILayout.Label("NODE " + (n.Id + 1), Bold);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("✕", "minibutton", GUILayout.Width(24))) selectedNode = -1;
        GUILayout.EndHorizontal();

        GUILayout.Label(n.ProducesText() + (n.IsFactory ? "  (FACTORY)" : "  (RAW HUB)"));
        GUILayout.Label("Production: " + n.ProductionPerTick(n.Produced[0]).ToString("0") + " per tick"
            + (n.Produced.Count > 1 ? " (each)" : ""));
        if (n.Inputs.Count > 0)
            GUILayout.Label("Consumes: " + n.InputsText(), Small);
        GUILayout.Label("Modules: " + n.ModuleCount + "/" + n.MaxSlots + " slots  (Speed x2 = "
            + Mathf.Pow(2f, n.ProductionModules).ToString("0") + "x, Storage x2 = "
            + Mathf.Pow(2f, n.StorageModules).ToString("0") + "x)");
        GUILayout.Label("Owner: " + owner, BoldTinted(ownerColor));

        GUILayout.Space(8);

        // ---- action area: depends on who owns the node ----
        var p = S.Player;

        if (n.Owner == null)
        {
            // government node: buy with PP
            float cost = S.GovernmentNodeCost(p);
            GUILayout.Label("Buy from the government for " + cost.ToString("0") + " PP.", Small);
            if (p.PoliticalPower < cost - 0.001f)
                GUILayout.Label("Not enough PP (you have " + p.PoliticalPower.ToString("0") + " PP).", RedStyle());
            if (GUILayout.Button("BUY NODE  (" + cost.ToString("0") + " PP)", GUILayout.Height(34)))
            {
                string res;
                if (!S.BuyGovernmentNode(p, n, out res)) statusMsg = res;
            }
        }
        else if (n.Owner == p)
        {
            // your node: upgrade it
            GUILayout.Label("UPGRADE (each module doubles its effect)", Small);
            if (GUILayout.Button("BUILD SPEED BOOSTER  (2x production)", GUILayout.Height(30)))
            {
                string res;
                if (!S.BuildModule(p, n, ModuleType.ProductionBooster, out res)) statusMsg = res;
            }
            if (GUILayout.Button("BUILD STORAGE BOOSTER  (2x storage)", GUILayout.Height(30)))
            {
                string res;
                if (!S.BuildModule(p, n, ModuleType.StorageBooster, out res)) statusMsg = res;
            }
            if (!n.HasFreeSlot)
                GUILayout.Label("No free slots left - cannot build more modules.", RedStyle());
            GUILayout.Space(4);
            GUILayout.Label("Cost  Speed: " + MaterialCatalog.ModuleRecipeText(ModuleType.ProductionBooster), Small);
            GUILayout.Label("       Storage: " + MaterialCatalog.ModuleRecipeText(ModuleType.StorageBooster), Small);
        }
        else
        {
            // another company's node: buy with a resource offer worth MORE than its value
            GUILayout.Label("Buy from " + n.Owner.Name + " with a resource offer worth MORE than its value.", Small);
            nodeOfferRes = ResourceRow("Offer:", nodeOfferRes);
            nodeOfferAmt = AmountRow("", nodeOfferAmt);
            float v = nodeOfferAmt * S.market.Price((ResourceType)nodeOfferRes);
            float need = S.CompanyNodeValue(n);
            float have = p.GetInventory((ResourceType)nodeOfferRes);
            GUILayout.Label("Offer " + v.ToString("0") + " PP vs node " + need.ToString("0") + " PP  "
                + ((v > need) ? "ACCEPTED" : "TOO LOW"), Bold);
            if (nodeOfferAmt > have + 0.001f)
                GUILayout.Label("You only have " + have.ToString("0") + " " + MaterialCatalog.Name((ResourceType)nodeOfferRes) + ".", RedStyle());
            if (GUILayout.Button("SEND OFFER", GUILayout.Height(34)))
            {
                string res;
                if (!S.BuyNode(p, n, (ResourceType)nodeOfferRes, nodeOfferAmt, out res)) statusMsg = res;
                else selectedNode = -1;
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
            // same map + seed as this run (map-setup-plan Q4)
            if (GameFlow.LastSetup != null)
            {
                Random.InitState(GameFlow.LastSetup.seed);
                MapGenerator.Generate(currentSpecs = new List<Node>(),
                                      GameFlow.LastMap, GameFlow.LastSetup);
                S.NewGameWithSpecs(currentSpecs, GameFlow.LastSetup);
            }
            else S.NewGame();
            statusMsg = "";
        }
        if (GUILayout.Button("MAIN MENU", GUILayout.Height(32)))
        {
            GameFlow.OpenMenu();
            return;
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
