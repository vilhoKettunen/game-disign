using UnityEngine;

/// <summary>
/// Shared node visuals, used by BOTH the in-game map (GameView) and the map
/// editor's live 3D preview (MenuView), so the map creator shows the same
/// node models as the game:
///
/// - PrefabFor:     prefab slot rule = Produced[0] + IsFactory
///                  (node-prefabs-camera-plan section 3), same mapping as GameView
/// - BuildNodeBody: the node body - the prefab if its slot is filled, else the
///                  placeholder shapes (NodeShapes). The model is NOT scaled here;
///                  it inherits its parent's scale, so both views (which parent
///                  the node at NodeScale) render it at the SAME size.
/// - AddLabel:      the floating name label above the node, aimed at the
///                  top-down camera and flipped 180° in-plane so the text reads
///                  correctly instead of upside down (a "W" reads as "W", not "M").
/// - ShortName:     the label text ("3 W", "7 ChF", ...)
///
/// The prefab slots themselves stay in the Inspector: GameView and MenuView
/// each expose prefabWood/Metal/Energy/Water/Chips/MechParts/Building/Food and
/// drag in the SAME prefab references (SampleScene "gameconfig" object), so
/// the preview is always a faithful copy of what the game will show.
/// </summary>
public static class NodeVisuals
{
    /// <summary>Label height above the node center (matches the old GameView label).</summary>
    public const float LabelHeight = 3.6f;
    public const int LabelFontSize = 40;

    /// <summary>
    /// The scale node models (and their labels) are rendered at, in BOTH the
    /// in-game map and the map creator's preview.
    ///
    /// WHY: in the game each node is parented under its ownership-area plane,
    /// which GameView sets to 0.45 - so in-game node models are effectively
    /// 0.45x their authored size (which is what makes them fit their 5-unit
    /// cells without clipping into neighbours). The map creator, by contrast,
    /// parents its nodes under an unscaled (1.0) root, so without this the same
    /// model appears 1.0/0.45 ~ 2.2x TOO big in the creator and clips into
    /// neighbouring nodes. Applying the SAME 0.45 to the creator's node root
    /// makes the creator a faithful, same-size copy of the game.
    ///
    /// HOW IT'S APPLIED:
    ///   - Game:  node model is built under the area plane, so it inherits this
    ///            scale from the area (GameView sets the area to NodeScale).
    ///   - Creator: MenuView sets its node root (bodyRoot) to NodeScale, which
    ///            scales the model AND its label together (mirrors the game).
    /// This shared builder (BuildNodeBody) therefore does NOT scale the model
    /// itself - it applies to both views' already-scaled parents identically.
    ///
    /// NOTE: this must equal the ownership-area scale GameView applies (see
    /// GameView.BuildMap). Change both together if you rescale.
    /// </summary>
    public const float NodeScale = 0.45f;

    /// <summary>
    /// Which prefab a node uses: slot = Produced[0] + IsFactory
    /// (node-prefabs-camera-plan section 3). Any null slot falls back to the
    /// placeholder shapes.
    /// </summary>
    public static GameObject PrefabFor(Node n,
        GameObject wood, GameObject metal, GameObject energy, GameObject water,
        GameObject chips, GameObject mechParts, GameObject building, GameObject food)
    {
        if (n.Produced.Count == 0) return null;
        var r = n.Produced[0];
        if (n.IsFactory)
        {
            switch (r)
            {
                case ResourceType.Chips: return chips;
                case ResourceType.MechanicalParts: return mechParts;
                case ResourceType.BuildingMaterials: return building;
                case ResourceType.Food: return food;
            }
        }
        else
        {
            switch (r)
            {
                case ResourceType.Wood: return wood;
                case ResourceType.Metal: return metal;
                case ResourceType.Energy: return energy;
                case ResourceType.Water: return water;
            }
        }
        return null;
    }

    /// <summary>
    /// Builds the node body under <paramref name="parent"/> (the parent should
    /// already be at the node's map position). Prefab if the slot is filled,
    /// else the placeholder shape. Returns every part's renderer so the caller
    /// can tint it with the owner color.
    /// </summary>
    public static Renderer[] BuildNodeBody(Transform parent, Node n, bool usePrefabs,
        GameObject wood, GameObject metal, GameObject energy, GameObject water,
        GameObject chips, GameObject mechParts, GameObject building, GameObject food)
    {
        GameObject body = null;
        if (usePrefabs)
        {
            var p = PrefabFor(n, wood, metal, energy, water, chips, mechParts, building, food);
            if (p != null)
            {
                body = Object.Instantiate(p, parent);
                body.name = "NodeBody";
                body.transform.localPosition = Vector3.zero;
                // authoring notes (plan section 4): no colliders/scripts on the prefab;
                // strip defensively so the node stays click-transparent
                foreach (var c in body.GetComponents<Collider>()) Object.Destroy(c);
                // NOTE on SIZE: we deliberately do NOT scale the model here.
                // The model inherits whatever scale its parent has, so both
                // callers render it at the SAME size as their node root:
                //   - GameView parents the node under the 0.45 ownership-area
                //     plane, so the model is 0.45 (see GameView.BuildMap).
                //   - MenuView sets its node root (bodyRoot) to NodeScale (0.45),
                //     so the creator's model is also 0.45 - a faithful copy of
                //     the game (see MenuView.BuildPreviewNode).
                // Keeping the scale on the parent (not here) means this builder
                // is size-agnostic and both views stay perfectly in sync.
            }
        }
        if (body == null)
        {
            body = NodeShapes.Build(parent, n);
            // NodeShapes sets the WORLD position to n.Position; make it local to
            // the (already positioned) parent so the body doesn't drift.
            body.transform.localPosition = Vector3.zero;
        }

        var parts = body.GetComponentsInChildren<Renderer>();
        var list = new Renderer[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].sharedMaterial != null)
                list[i] = parts[i];
        return list;
    }

    /// <summary>
    /// The floating name label above the node (Q8: type + owner color), shared
    /// by the game and the map editor. Aimed so its face points UP (toward the
    /// top-down camera) and it reads correctly - not upside down or mirrored
    /// (a "W" must read as "W", not "M").
    /// </summary>
    public static TextMesh AddLabel(Transform parent, string text, Color color)
    {
        var labelGo = new GameObject("NodeLabel");
        labelGo.transform.SetParent(parent, false);
        labelGo.transform.localPosition = new Vector3(0f, LabelHeight, 0f);
        // Aim the label so it reads correctly from the top-down camera.
        // TextMesh local axes: front = +Z (where the letters are), top = +Y,
        // right (end of line) = +X. The camera (CameraController) sits on the
        // -Z side looking toward +Z, so on screen: up = +Z, right = -X.
        // LookRotation(forward=+Y, up=+Z) gives:
        //   front -> +Y  (up toward the camera: we see the letters, not a mirror)
        //   top   -> +Z  (screen-up: tops of the letters point up)
        //   right -> -X  (screen-right: letters read left-to-right)
        // The previous -90°-around-X rotation left the text rotated 180° in-plane
        // (top down, right on the left), i.e. upside down - which is why a "W"
        // read like an "M". This is the 180° flip that fixes it.
        labelGo.transform.localRotation =
            Quaternion.LookRotation(Vector3.up, new Vector3(0f, 0f, 1f));
        var tm = labelGo.AddComponent<TextMesh>();
        // Unity 6: "Arial.ttf" is no longer a valid built-in font -> use "LegacyRuntime.ttf"
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) tm.font = f;
        tm.text = text;
        tm.fontSize = LabelFontSize;
        tm.color = color;
        tm.anchor = TextAnchor.UpperCenter;
        tm.alignment = TextAlignment.Center;
        return tm;
    }

    /// <summary>Label text: node number + resource code(s) + F for factories.</summary>
    public static string ShortName(Node n)
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

    public static Color Brighten(Color c, float f)
    {
        return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), 1f);
    }
}
