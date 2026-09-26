using System.Collections.Generic;
using UnityEngine;
using System.IO;

/// <summary>
/// The start screen (map-setup-plan.txt Q3/Q4): choose a premade map or a fully
/// random one, edit every setting (Q5), watch it in a TEXT preview and a LIVE 3D
/// PREVIEW (Q9: both, updating as you edit), save presets, and import/export
/// the setup as a shareable JSON file. Then START GAME.
///
/// Attach to any GameObject in MenuScene. It finds/creates a CameraController
/// and auto-frames the preview on the map, so nothing needs wiring.
///
/// Sharing (Q5): a preset is the setup + map name stored in PlayerPrefs.
/// EXPORT writes a .json file you send to a friend; IMPORT reads one back.
/// </summary>
public class MenuView : MonoBehaviour
{
    [Header("Premade maps (map-setup-plan Q6: 5 maps)")]
    [Tooltip("Drag the MapDefinition assets here: Classic 5x4, Medium 10x10, Large 15x15, Tall 5x10, Finland.")]
    public MapDefinition[] premadeMaps;

    [Header("Live 3D preview")]
    public bool show3DPreview = true;
    public Color previewWaterColor = new Color(0.14f, 0.26f, 0.42f);
    public Color previewGroundColor = new Color(0.30f, 0.34f, 0.32f);

    [Header("Ownership colors (match the in-game colors)")]
    public Color playerColor = new Color(0.30f, 0.75f, 0.35f);
    public Color aiColor = new Color(0.80f, 0.30f, 0.30f);
    public Color govColor = new Color(0.55f, 0.55f, 0.55f);

    GameSetup setup = new GameSetup();
    string presetName = "My preset";
    string importJson = "";
    string msg = "";
    Vector2 rightScroll;
    string previewError = "";
    List<Node> previewNodes = new List<Node>();
    GameObject previewRoot;
    bool rebuildQueued;

    // ================= BOOT =================

    void Awake()
    {
        if (GameFlow.LastSetup != null) setup = GameFlow.LastSetup.Clone();
        if (GameFlow.LastMap != null && premadeMaps != null)
        {
            int idx = System.Array.IndexOf(premadeMaps, GameFlow.LastMap);
            if (idx >= 0) { setup.premadeMap = idx; setup.mapMode = 0; }
        }
        GameFlow.StartMenu();
    }

    void Start()
    {
        var cc = CameraController.FindOrCreate();
        if (cc != null)
        {
            cc.smoothing = 14f;
            cc.clampToMap = true;
            cc.boundsMargin = 8f;
            cc.enableWASD = true;
            cc.enableLeftDrag = true;
            cc.enableRightDrag = true;
        }
        QueueRebuild();
    }

    void OnDestroy() { DestroyPreview(); }

    void Update()
    {
        if (rebuildQueued)
        {
            rebuildQueued = false;
            RebuildPreview();
        }
    }

    void QueueRebuild() { rebuildQueued = true; }

    // ================= LIVE 3D PREVIEW (Q9) =================

    void RebuildPreview()
    {
        DestroyPreview();
        previewError = "";
        previewNodes.Clear();

        MapDefinition map = (setup.mapMode == 0) ? PreviewMap() : null;
        Random.InitState(setup.seed);
        MapGenerator.Generate(previewNodes, map, setup);

        if (previewNodes.Count == 0)
        {
            previewError = "No nodes to preview - pick a map or raise the node count cap.";
            return;
        }

        int companies = Mathf.Clamp(setup.companyCount, 1, 8);
        for (int i = 0; i < previewNodes.Count; i++)
        {
            Color c = (i < companies) ? ((i == 0) ? playerColor : aiColor) : govColor;
            BuildPreviewNode(previewNodes[i], c);
        }
        if (map != null) BuildPreviewTerrain(map);

        // frame the camera on the map extent
        Vector2 min = Extent(previewNodes, map);
        Vector2 max = ExtentMax(previewNodes, map);
        var cc = CameraController.Instance;
        if (cc != null)
        {
            cc.SetMapBounds(min, max);
            cc.FocusOn(new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f));
        }
    }

    Vector2 Extent(List<Node> nodes, MapDefinition map)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < nodes.Count; i++)
        {
            var p = nodes[i].Position;
            min = new Vector2(Mathf.Min(min.x, p.x), Mathf.Min(min.y, p.y));
            max = new Vector2(Mathf.Max(max.x, p.x), Mathf.Max(max.y, p.y));
        }
        if (map != null)
        {
            Vector2 em = map.CellCenter(0, 0);
            Vector2 eM = map.CellCenter(map.Rows - 1, map.Cols - 1);
            min = new Vector2(Mathf.Min(min.x, em.x - 2.5f), Mathf.Min(min.y, em.y - 2.5f));
            max = new Vector2(Mathf.Max(max.x, eM.x + 2.5f), Mathf.Max(max.y, eM.y + 2.5f));
        }
        return min;
    }

    Vector2 ExtentMax(List<Node> nodes, MapDefinition map)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < nodes.Count; i++)
        {
            var p = nodes[i].Position;
            min = new Vector2(Mathf.Min(min.x, p.x), Mathf.Min(min.y, p.y));
            max = new Vector2(Mathf.Max(max.x, p.x), Mathf.Max(max.y, p.y));
        }
        if (map != null)
        {
            Vector2 em = map.CellCenter(0, 0);
            Vector2 eM = map.CellCenter(map.Rows - 1, map.Cols - 1);
            min = new Vector2(Mathf.Min(min.x, em.x - 2.5f), Mathf.Min(min.y, em.y - 2.5f));
            max = new Vector2(Mathf.Max(max.x, eM.x + 2.5f), Mathf.Max(max.y, eM.y + 2.5f));
        }
        return max;
    }

    void BuildPreviewNode(Node n, Color owner)
    {
        if (!show3DPreview) return;
        if (previewRoot == null) previewRoot = new GameObject("PreviewMap");
        var body = NodeShapes.Build(previewRoot.transform, n);
        body.transform.position = new Vector3(n.Position.x, 0f, n.Position.y);
        foreach (var r in body.GetComponentsInChildren<Renderer>())
            r.material.color = owner;
    }

    void BuildPreviewTerrain(MapDefinition map)
    {
        if (!show3DPreview || previewRoot == null) return;
        float g = MapDefinition.GridSpacing;
        for (int r = 0; r < map.Rows; r++)
        {
            for (int c = 0; c < map.Cols; c++)
            {
                char cell = map.CellAt(r, c);
                bool water = map.IsWater(cell);
                if (water)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "PrevWater_" + r + "_" + c;
                    go.transform.SetParent(previewRoot.transform, false);
                    go.transform.localScale = new Vector3(g * 0.98f, 0.2f, g * 0.98f);
                    go.transform.position = new Vector3((c - (map.Cols - 1) * 0.5f) * g, -0.11f, (r - (map.Rows - 1) * 0.5f) * g);
                    StripCollider(go);
                    go.GetComponent<Renderer>().sharedMaterial.color = previewWaterColor;
                }
                else
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    go.name = "PrevGround_" + r + "_" + c;
                    go.transform.SetParent(previewRoot.transform, false);
                    go.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
                    go.transform.position = new Vector3((c - (map.Cols - 1) * 0.5f) * g, 0.02f, (r - (map.Rows - 1) * 0.5f) * g);
                    StripCollider(go);
                    go.GetComponent<Renderer>().sharedMaterial.color = previewGroundColor;
                }
            }
        }
    }

    void StripCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
    }

    void DestroyPreview()
    {
        if (previewRoot != null) { Destroy(previewRoot); previewRoot = null; }
    }

    MapDefinition PreviewMap()
    {
        if (setup.mapMode == 0)
        {
            int idx = Mathf.Clamp(setup.premadeMap, 0, Mathf.Max(0, premadeMaps != null ? premadeMaps.Length - 1 : 0));
            if (premadeMaps != null && idx < premadeMaps.Length && premadeMaps[idx] != null)
                return premadeMaps[idx];
            return MakeBuiltinClassic(); // no asset assigned yet -> built-in classic shape
        }
        return null;
    }

    MapDefinition MakeBuiltinClassic()
    {
        var m = ScriptableObject.CreateInstance<MapDefinition>();
        m.rows = new string[]
        {
            "....R...",
            "R.R.R.R.",
            ".R.R.R..",
            "R.R.R.R.",
            "...R....",
        };
        return m;
    }

    // ================= UI =================

    void OnGUI()
    {
        var title = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        GUILayout.Label("REOURCE - MAP SETUP", title, GUILayout.Height(46));
        var sub = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        GUILayout.Label("Pick a map, tune the settings, watch the live 3D preview, then start.", sub);
        GUILayout.Space(8);

        GUILayout.BeginHorizontal();
        LeftColumn();
        RightColumn();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        msg = GUILayout.TextField(msg, GUILayout.Width(680), GUILayout.Height(22));
        if (msg.Length > 0) { } // (kept for one frame so it's readable; cleared on next input change)
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    void LeftColumn()
    {
        GUILayout.BeginVertical("box", GUILayout.Width(410));

        GUILayout.Label("MAP  (premade = node types fixed by the shape)", Bold());
        if (GUILayout.Toggle(setup.mapMode == 0, "PREMADE MAP (shape decides where + which types)", "toggle") && setup.mapMode != 0)
        {
            setup.mapMode = 0;
            QueueRebuild();
        }
        if (GUILayout.Toggle(setup.mapMode == 1, "FULLY RANDOM MAP (types rolled from the mix below)", "toggle") && setup.mapMode != 1)
        {
            setup.mapMode = 1;
            QueueRebuild();
        }
        if (setup.mapMode == 0)
        {
            if (premadeMaps != null)
            {
                for (int i = 0; i < premadeMaps.Length; i++)
                {
                    string name = (premadeMaps[i] != null)
                        ? premadeMaps[i].name + "  (" + premadeMaps[i].NodeSlotCount() + " nodes)"
                        : "(slot " + i + " - drag a MapDefinition in)";
                    if (GUILayout.Toggle(setup.premadeMap == i, name, "toggle") && setup.premadeMap != i)
                    {
                        setup.premadeMap = i;
                        QueueRebuild();
                    }
                }
            }
            else
            {
                GUILayout.Label("No premade maps assigned yet - the preview uses the built-in Classic 5x4.", Small());
            }
        }
        else
        {
            GUILayout.Space(4);
            GUILayout.Label("The random layout is decided by the SEED + node count when you start.\nShare SEED + settings to share the map.", Small());
        }

        GUILayout.Space(8);
        GUILayout.Label("SETTINGS  (all editable - map-setup-plan Q5)", Bold());

        setup.seed = IntField("Seed (share this to share the map!)", setup.seed, 1, 999999);
        setup.nodeCountCap = IntField("Node count cap", setup.nodeCountCap, 1, 200);
        setup.dualResourceHubPct = IntField("Dual-resource hub %", setup.dualResourceHubPct, 0, 100);
        setup.companyCount = IntField("Company count (you + AI)", setup.companyCount, 2, 8);
        setup.startingPP = IntField("Starting PP", Mathf.RoundToInt(setup.startingPP), 0, 100000);
        setup.taxEveryTicks = IntField("Tax every (ticks)", Mathf.RoundToInt(setup.taxEveryTicks), 1, 99);
        setup.taxRatePct = IntField("Tax rate %", Mathf.RoundToInt(setup.taxRatePct), 1, 100);
        setup.tickSeconds = IntField("Tick length (seconds)", Mathf.RoundToInt(setup.tickSeconds), 1, 3600);

        GUILayout.Space(4);
        GUILayout.Label("Node type mix (weights - rolled for R/? nodes and random maps):", Small());
        string[] names = { "Wood", "Metal", "Energy", "Water", "Factory" };
        int[] vals = { setup.rawWoodPct, setup.rawMetalPct, setup.rawEnergyPct, setup.rawWaterPct, setup.factoryPct };
        for (int i = 0; i < 5; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(names[i], GUILayout.Width(64));
            vals[i] = Mathf.RoundToInt(Mathf.Clamp01(GUILayout.HorizontalSlider(vals[i] / 100f, 0f, 1f, GUILayout.Width(230))) * 100f);
            GUILayout.Label(vals[i] + "%", GUILayout.Width(34));
            GUILayout.EndHorizontal();
        }
        if (vals[0] != setup.rawWoodPct || vals[1] != setup.rawMetalPct || vals[2] != setup.rawEnergyPct ||
            vals[3] != setup.rawWaterPct || vals[4] != setup.factoryPct)
        {
            setup.rawWoodPct = vals[0]; setup.rawMetalPct = vals[1]; setup.rawEnergyPct = vals[2];
            setup.rawWaterPct = vals[3]; setup.factoryPct = vals[4];
            QueueRebuild();
        }

        GUILayout.Space(10);
        GUILayout.Label("PRESETS  (save / load - shareable via EXPORT)", Bold());
        presetName = GUILayout.TextField(presetName, GUILayout.Width(340));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SAVE PRESET", GUILayout.Width(110), GUILayout.Height(28))) { SavePreset(); }
        if (GUILayout.Button("LOAD", GUILayout.Width(70), GUILayout.Height(28))) { LoadPreset(); }
        if (GUILayout.Button("DELETE", GUILayout.Width(70), GUILayout.Height(28)))
        {
            PlayerPrefs.DeleteKey(PresetKey(presetName));
            PlayerPrefs.Save();
            SetMsg("Deleted preset '" + presetName + "'.");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        if (GUILayout.Button("EXPORT SETUP  (copy text to share)", GUILayout.Height(30)))
            SetMsg(ExportToText());

        GUILayout.Space(4);
        importJson = GUILayout.TextArea(importJson, GUILayout.Height(56));
        if (GUILayout.Button("IMPORT SETUP  (paste the shared text)", GUILayout.Height(28)))
        {
            try
            {
                setup = GameSetup.FromJson(importJson);
                importJson = "";
                SetMsg("Imported setup - check map/seed, then start.");
                QueueRebuild();
            }
            catch (System.Exception e)
            {
                SetMsg("Import failed: " + e.Message);
            }
        }

        GUILayout.Space(12);
        if (GUILayout.Button("START GAME", BigButton(), GUILayout.Height(46)))
        {
            GameFlow.LastSetup = setup.Clone();
            GameFlow.LastMap = (setup.mapMode == 0 && premadeMaps != null && setup.premadeMap >= 0 && setup.premadeMap < premadeMaps.Length)
                ? premadeMaps[setup.premadeMap] : null;
            GameFlow.OpenGame();
        }
        GUILayout.EndVertical();
    }

    void RightColumn()
    {
        GUILayout.BeginVertical("box");

        GUILayout.Label("MAP PREVIEW  (text grid)", Bold());
        MapDefinition map = PreviewMap();
        if (map != null)
        {
            rightScroll = GUILayout.BeginScrollView(rightScroll, false, false);
            GUILayout.Label(map.PreviewText(), Mono(), GUILayout.Height(150));
            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Label("Random map: the exact layout is rolled from the SEED + node count when you start.\nChoose a premade map to see a fixed shape here.", Small());
        }

        if (previewError != "") GUILayout.Label(previewError, Error());

        GUILayout.Space(6);
        show3DPreview = GUILayout.Toggle(show3DPreview, "Live 3D preview (in the scene, right of this panel)", "toggle");
        if (show3DPreview != rebuildShown)
        {
            rebuildShown = show3DPreview;
            QueueRebuild();
        }
        GUILayout.Label("Look around: mouse drag / WASD to pan, wheel to zoom,\nhold middle mouse + move up/down for the camera angle.", Small());

        GUILayout.Space(6);
        if (previewNodes.Count > 0)
        {
            int comps = Mathf.Clamp(setup.companyCount, 1, 8);
            GUILayout.BeginHorizontal();
            GUILayout.Label(previewNodes.Count + " nodes", Bold());
            GUILayout.Label(comps + " company(ies) start owning the first nodes", Small());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ColorDot(playerColor); GUILayout.Label("you", Small());
            GUILayout.Space(6);
            ColorDot(aiColor); GUILayout.Label("AI", Small());
            GUILayout.Space(6);
            ColorDot(govColor); GUILayout.Label("government", Small());
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    bool rebuildShown = true;

    // ================= PRESETS (PlayerPrefs) / IMPORT / EXPORT (Q5) =================

    static string PresetKey(string name) { return "preset:" + name.ToLowerInvariant(); }

    void SavePreset()
    {
        var s = setup.Clone();
        PlayerPrefs.SetString(PresetKey(presetName), s.ToJson());
        PlayerPrefs.Save();
        SetMsg("Saved preset '" + presetName + "'.");
    }

    void LoadPreset()
    {
        string key = PresetKey(presetName);
        if (!PlayerPrefs.HasKey(key)) { SetMsg("No preset named '" + presetName + "'."); return; }
        try
        {
            setup = GameSetup.FromJson(PlayerPrefs.GetString(key));
            SetMsg("Loaded preset '" + presetName + "'.");
            QueueRebuild();
        }
        catch (System.Exception e) { SetMsg("Load failed: " + e.Message); }
    }

    string ExportToText()
    {
        return "Copy this to share your setup (paste it into IMPORT on another machine):\n\n"
             + setup.ToJson();
    }

    void SetMsg(string s) { msg = s; }

    // ================= GUI helpers =================

    int IntField(string label, int value, int min, int max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(230));
        string txt = GUILayout.TextField(value.ToString(), GUILayout.Width(90));
        int v;
        if (int.TryParse(txt, out v)) value = v;
        GUILayout.EndHorizontal();
        return Mathf.Clamp(value, min, max);
    }

    void ColorDot(Color c)
    {
        Rect r = GUILayoutUtility.GetRect(14, 14);
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    GUIStyle BigButton()
    {
        var s = new GUIStyle(GUI.skin.button);
        s.fontSize = 20;
        s.fontStyle = FontStyle.Bold;
        return s;
    }

    static GUIStyle _bold;
    GUIStyle Bold()
    {
        if (_bold == null) _bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        return _bold;
    }

    static GUIStyle _small;
    GUIStyle Small()
    {
        if (_small == null) _small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        return _small;
    }

    static GUIStyle _mono;
    GUIStyle Mono()
    {
        if (_mono == null)
        {
            _mono = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _mono.normal.textColor = new Color(0.85f, 0.9f, 0.9f);
        }
        return _mono;
    }

    static GUIStyle _error;
    GUIStyle Error()
    {
        if (_error == null)
        {
            _error = new GUIStyle(GUI.skin.label);
            _error.normal.textColor = new Color(1f, 0.4f, 0.35f);
        }
        return _error;
    }
}
