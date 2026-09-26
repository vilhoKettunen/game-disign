using UnityEngine;

/// <summary>
/// A premade map as a grid of characters (map-setup-plan.txt Q1: ScriptableObject
/// grids). One row per line; every row should be the same length (missing cells
/// read as ground). Create with: Assets > Create > Recource > Map Definition.
///
/// Legend (map-setup-plan Q8: "0" = water for lakes/sea, "." = ground):
///   W = Wood node        M = Metal node       E = Energy node       a = Water node
///   C = Chips factory    P = Mech Parts       B = Building Mats     F = Food factory
///   R = any node (type rolled from the settings mix)   ? = random raw hub
///   0 = water (lake / ocean / river)                   . = ground (empty slot)
///
/// Pinned types (W/M/E/a/C/P/B/F) always produce that resource; R/? are rolled.
/// Node types for R/? and dual-resource hubs are seeded (GameSetup.seed), so a
/// shared map + seed always produces the same map for everyone.
/// </summary>
[CreateAssetMenu(fileName = "MapDefinition", menuName = "Recource/Map Definition")]
public class MapDefinition : ScriptableObject
{
    /// <summary>World spacing between grid cells (matches the old 5-unit grid).</summary>
    public const float GridSpacing = 5f;

    [TextArea(6, 20)]
    [Tooltip("One line per row, top = north. W/M/E/a = raw nodes, C/P/B/F = factories, R = any node, ? = random raw, 0 = water, . = ground.")]
    public string[] rows = new string[0];

    public int Rows { get { return rows.Length; } }

    public int Cols
    {
        get
        {
            int m = 0;
            for (int i = 0; i < rows.Length; i++)
                m = Mathf.Max(m, rows[i].Length);
            return m;
        }
    }

    /// <summary>Cell at (row, col); out-of-range reads as ground '.'.</summary>
    public char CellAt(int row, int col)
    {
        if (row < 0 || row >= rows.Length) return '.';
        string s = rows[row];
        if (col < 0 || col >= s.Length) return '.';
        return s[col];
    }

    public bool IsNodeCell(char c)
    {
        return c == 'W' || c == 'M' || c == 'E' || c == 'a' ||
               c == 'C' || c == 'P' || c == 'B' || c == 'F' ||
               c == 'R' || c == '?';
    }

    public bool IsWater(char c) { return c == '0'; }

    public ResourceType? FixedRaw(char c)
    {
        switch (c)
        {
            case 'W': return ResourceType.Wood;
            case 'M': return ResourceType.Metal;
            case 'E': return ResourceType.Energy;
            case 'a': return ResourceType.Water;
            default: return null;
        }
    }

    public ResourceType? FixedRefined(char c)
    {
        switch (c)
        {
            case 'C': return ResourceType.Chips;
            case 'P': return ResourceType.MechanicalParts;
            case 'B': return ResourceType.BuildingMaterials;
            case 'F': return ResourceType.Food;
            default: return null;
        }
    }

    /// <summary>How many node slots the shape contains (before the node-count cap).</summary>
    public int NodeSlotCount()
    {
        int n = 0;
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
                if (IsNodeCell(rows[r][c])) n++;
        return n;
    }

    /// <summary>World position of a cell center; the grid is centered on (0,0).</summary>
    public Vector2 CellCenter(int row, int col)
    {
        return new Vector2((col - (Cols - 1) * 0.5f) * GridSpacing,
                           (row - (Rows - 1) * 0.5f) * GridSpacing);
    }

    public Vector2 ExtentMin() { return new Vector2(0f, 0f); }
    public Vector2 ExtentMax() { return new Vector2((Cols - 1) * GridSpacing, (Rows - 1) * GridSpacing); }

    /// <summary>Text preview for the setup screen (map-setup-plan Q9).</summary>
    public string PreviewText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("    " + ColumnNumbers(Cols));
        for (int r = 0; r < Rows; r++)
            sb.AppendLine(r + "   " + rows[r]);
        sb.AppendLine();
        sb.AppendLine("node slots: " + NodeSlotCount());
        sb.AppendLine("legend: W=Wood M=Metal E=Energy a=Water  C=Chips P=MechParts B=Building F=Food");
        sb.AppendLine("        R=any node  ?=random raw  0=water  .=ground");
        return sb.ToString();
    }

    static string ColumnNumbers(int cols)
    {
        var sb = new System.Text.StringBuilder();
        for (int c = 0; c < cols; c++) sb.Append(c % 10);
        return sb.ToString();
    }

    /// <summary>Validation warnings (shown if you open the asset in the editor console).</summary>
    public string Validate()
    {
        var sb = new System.Text.StringBuilder();
        if (rows.Length == 0)
            sb.AppendLine("map is empty");
        int longest = 0;
        for (int i = 0; i < rows.Length; i++)
            longest = Mathf.Max(longest, rows[i].Length);
        for (int i = 0; i < rows.Length; i++)
            if (rows[i].Length != longest)
                sb.AppendLine("row " + i + " is " + rows[i].Length + " chars (longest is " + longest + ") - the extra cells read as ground");
        foreach (string r in rows)
            foreach (char ch in r)
                if (!IsNodeCell(ch) && !IsWater(ch) && ch != '.' && !char.IsWhiteSpace(ch))
                    sb.AppendLine("'" + ch + "' is not in the legend - it will be treated as ground");
        return sb.ToString();
    }
}
