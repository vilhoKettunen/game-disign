using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Placeholder node shapes (node-prefabs-camera-plan.txt Q10, verbatim):
///   wood = cylinder          metal = sphere            energy = cube
///   water = triangle (cone)  dual hub = two types arranged in a checkerboard
///   chips = two balls on top of each other
///   mechanical parts = cylinder with a cube on top
///   building materials = cube with a sphere on top
///   food = triangle (cone) with a ball on top
///
/// Used by GameView (until real prefabs are assigned) and by the live 3D map
/// preview in the menu. Every part gets its own material so ownership colors
/// never leak between nodes. Unity has no Cone primitive, so "triangle" shapes
/// use a small generated cone mesh.
/// </summary>
public static class NodeShapes
{
    /// <summary>A full-size node occupies ~4.4 units - it fits one 5-unit grid cell.</summary>
    public const float FitRadius = 2.2f;

    static float W = 1.6f;   // default node width
    static float H = 1.8f;   // default node height

    public static GameObject Build(Transform parent, Node n)
    {
        var root = new GameObject("NodeShape");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(n.Position.x, 0f, n.Position.y);

        if (n.IsFactory)
        {
            BuildFactory(root.transform, n.Produced[0]);
        }
        else if (n.Produced.Count == 1)
        {
            AddRaw(root.transform, n.Produced[0], Vector3.zero, 1f);
        }
        else
        {
            // dual hub (Q1): two shrunk shapes of the two types, checkerboard -
            // same type on opposite corners (top-left + bottom-right, top-right + bottom-left)
            float o = FitRadius * 0.5f;
            float s = 0.55f;
            AddRaw(root.transform, n.Produced[0], new Vector3(-o, 0f, -o), s);
            AddRaw(root.transform, n.Produced[1], new Vector3(o, 0f, o), s);
            AddRaw(root.transform, n.Produced[1], new Vector3(-o, 0f, o), s);
            AddRaw(root.transform, n.Produced[0], new Vector3(o, 0f, -o), s);
        }

        return root;
    }

    static void AddRaw(Transform parent, ResourceType r, Vector3 off, float scale)
    {
        if (r == ResourceType.Water)
        {
            AddCone(parent, off, W * scale, 2.0f * scale);
            return;
        }
        float w = W * scale, h = H * scale;
        if (r == ResourceType.Metal) PlaceShape(parent, PrimitiveType.Sphere, off, w, w);
        else if (r == ResourceType.Energy) PlaceShape(parent, PrimitiveType.Cube, off, w, h);
        else PlaceShape(parent, PrimitiveType.Cylinder, off, w, h);
    }

    static void BuildFactory(Transform root, ResourceType outR)
    {
        switch (outR)
        {
            case ResourceType.Chips: // two balls on top of each other
                PlaceShape(root, PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), 1.2f, 1.2f);
                PlaceShape(root, PrimitiveType.Sphere, new Vector3(0f, 1.2f, 0f), 1.2f, 1.2f);
                break;
            case ResourceType.MechanicalParts: // cylinder with a cube on top
                PlaceShape(root, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), 1.6f, 1.4f);
                PlaceShape(root, PrimitiveType.Cube, new Vector3(0f, 1.4f, 0f), 1.2f, 1.2f);
                break;
            case ResourceType.BuildingMaterials: // cube with a sphere on top
                PlaceShape(root, PrimitiveType.Cube, new Vector3(0f, 0f, 0f), 1.6f, 1.6f);
                PlaceShape(root, PrimitiveType.Sphere, new Vector3(0f, 1.6f, 0f), 1.2f, 1.2f);
                break;
            case ResourceType.Food: // triangle (cone) with a ball on top
                AddCone(root, new Vector3(0f, 0f, 0f), 1.6f, 2.0f);
                PlaceShape(root, PrimitiveType.Sphere, new Vector3(0f, 2.0f, 0f), 1.0f, 1.0f);
                break;
            default:
                PlaceShape(root, PrimitiveType.Cube, new Vector3(0f, 0f, 0f), 1.6f, 1.8f);
                break;
        }
    }

    /// <summary>Place a primitive so its BASE sits at localPosition y = off.y.</summary>
    static void PlaceShape(Transform parent, PrimitiveType type, Vector3 off, float w, float h)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = type.ToString();
        go.transform.SetParent(parent, false);

        if (type == PrimitiveType.Sphere)
        {
            go.transform.localScale = new Vector3(w, w, w);
            go.transform.localPosition = new Vector3(off.x, off.y + w * 0.5f, off.z);
        }
        else
        {
            go.transform.localScale = new Vector3(w, h, w);
            go.transform.localPosition = new Vector3(off.x, off.y + h * 0.5f, off.z);
        }

        Strip(go);
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        go.GetComponent<Renderer>().sharedMaterial = new Material(sh);
    }

    // ================= generated cone (Unity has no Cone primitive) =================

    static Mesh _coneMesh;

    static void AddCone(Transform parent, Vector3 off, float radius, float height)
    {
        if (_coneMesh == null) _coneMesh = MakeConeMesh(16);
        var go = new GameObject("Cone");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(off.x, off.y + height * 0.5f, off.z);
        go.transform.localScale = new Vector3(radius, height, radius); // mesh built with r=1, h=2
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = _coneMesh;
        var mr = go.AddComponent<MeshRenderer>();
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        mr.sharedMaterial = new Material(sh);
    }

    static Mesh MakeConeMesh(int n)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        Vector3 apex = new Vector3(0f, 1f, 0f);
        Vector3[] ring = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float t = (i / (float)n) * Mathf.PI * 2f;
            ring[i] = new Vector3(Mathf.Cos(t), -1f, Mathf.Sin(t));
        }

        // side faces
        for (int i = 0; i < n; i++)
        {
            Vector3 b0 = ring[i], b1 = ring[(i + 1) % n];
            Vector3 fn = Vector3.Cross(b1 - apex, b0 - apex);
            if (fn.sqrMagnitude < 1e-8f) fn = Vector3.up;
            fn.Normalize();
            Vector3 outDir = (b0 + b1) * 0.5f; outDir.y = 0f;
            if (outDir.sqrMagnitude > 1e-8f && Vector3.Dot(fn, outDir) < 0f) fn = -fn;
            // winding: apex -> b1 -> b0 yields outward normals (was inverted: apex -> b0 -> b1)
            Tri(verts, norms, tris, apex, b1, b0, fn);
        }

        // bottom cap
        Vector3 baseCenter = new Vector3(0f, -1f, 0f);
        Vector3 down = new Vector3(0f, -1f, 0f);
        for (int i = 0; i < n; i++)
            // winding consistent with the sides: front-facing from below (matches the down normal)
            Tri(verts, norms, tris, baseCenter, ring[i], ring[(i + 1) % n], down);

        var m = new Mesh();
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    static void Tri(List<Vector3> v, List<Vector3> nrm, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
    {
        int base0 = v.Count;
        v.Add(a); nrm.Add(normal);
        v.Add(b); nrm.Add(normal);
        v.Add(c); nrm.Add(normal);
        t.Add(base0); t.Add(base0 + 1); t.Add(base0 + 2);
    }

    static void Strip(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);
    }
}
