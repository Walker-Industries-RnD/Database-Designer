using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DatabaseDesigner;
using static DatabaseDesigner.SessionStorage;

namespace Database_Designer
{
    // Draws the ER diagram as a standalone SVG, using the same card layout as
    // the ER Diagram window so the export looks like what's on screen.
    public static class ErDiagramExporter
    {
        public const double CardWidth = 240, HeaderHeight = 34, RowHeight = 22, Margin = 40;

        private const string Paper = "#F7F4EC", Ink = "#2C2B28", Header = "#454138", Cream = "#F0E9D2",
            Muted = "#8A8476", Pk = "#C88A1E", Fk = "#3E7CB1", Edge = "#6F9BC9", Background = "#24221E";

        public static string Qualified(TableObject t) =>
            string.IsNullOrEmpty(t.SchemaName) ? t.TableName : $"{t.SchemaName}.{t.TableName}";

        public static double CardHeight(TableObject t) => HeaderHeight + Math.Max(1, t.Rows?.Count ?? 0) * RowHeight + 8;

        // positions: top-left of each card keyed by schema.table. Tables with
        // no position are laid out in a column to the right.
        public static string ToSvg(IEnumerable<TableObject> tablesIn, IDictionary<string, (double X, double Y)> positions,
                                   string title = null, bool darkBackground = false)
        {
            var tables = (tablesIn ?? Enumerable.Empty<TableObject>()).Where(t => !string.IsNullOrEmpty(t.TableName)).ToList();
            var pos = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase);
            double maxX = 0, nextY = Margin;
            foreach (var t in tables)
                if (positions != null && positions.TryGetValue(Qualified(t), out var p)) { pos[Qualified(t)] = p; maxX = Math.Max(maxX, p.X + CardWidth); }
            foreach (var t in tables.Where(t => !pos.ContainsKey(Qualified(t))))
            {
                pos[Qualified(t)] = (maxX + (maxX > 0 ? 110 : Margin), nextY);
                nextY += CardHeight(t) + 36;
            }

            double titleSpace = string.IsNullOrWhiteSpace(title) ? 0 : 44;
            double minX = pos.Count == 0 ? 0 : pos.Values.Min(p => p.X);
            double minY = pos.Count == 0 ? 0 : pos.Values.Min(p => p.Y);
            double dx = Margin - minX, dy = Margin + titleSpace - minY;
            double width = pos.Count == 0 ? 400 : tables.Max(t => pos[Qualified(t)].X + CardWidth) + dx + Margin;
            double height = pos.Count == 0 ? 200 : tables.Max(t => pos[Qualified(t)].Y + CardHeight(t)) + dy + Margin;

            var sb = new StringBuilder();
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(width)}\" height=\"{N(height)}\" viewBox=\"0 0 {N(width)} {N(height)}\" font-family=\"Inter, 'Segoe UI', Helvetica, Arial, sans-serif\">\n");
            sb.Append($"  <rect width=\"100%\" height=\"100%\" fill=\"{(darkBackground ? Background : "#FFFFFF")}\"/>\n");
            if (titleSpace > 0)
                sb.Append($"  <text x=\"{N(Margin)}\" y=\"{N(Margin + 14)}\" font-size=\"20\" fill=\"{(darkBackground ? Cream : Ink)}\">{X(title)}</text>\n");

            (double X, double Y) At(string key) { var p = pos[key]; return (p.X + dx, p.Y + dy); }
            var byName = tables.GroupBy(Qualified, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            double RowY(TableObject t, string column)
            {
                int idx = Math.Max(0, (t.Rows ?? new()).FindIndex(r => string.Equals(r.Name, column, StringComparison.OrdinalIgnoreCase)));
                return At(Qualified(t)).Y + HeaderHeight + idx * RowHeight + RowHeight / 2;
            }

            sb.Append("  <g fill=\"none\" stroke=\"" + Edge + "\" stroke-width=\"1.6\">\n");
            var heads = new StringBuilder();
            foreach (var t in tables)
            {
                foreach (var r in t.References ?? new())
                {
                    if (r.RefTable == null || !byName.TryGetValue(r.RefTable, out var parent)) continue;
                    var c = At(Qualified(t)); var pp = At(Qualified(parent));
                    double ay = RowY(t, r.ForeignKey), by = RowY(parent, r.RefTableKey);
                    bool self = string.Equals(Qualified(t), Qualified(parent), StringComparison.OrdinalIgnoreCase);
                    double ax, bx, dirA, dirB;
                    if (self) { ax = c.X + CardWidth; bx = pp.X + CardWidth; dirA = 1; dirB = 1; }
                    else if (pp.X + CardWidth <= c.X) { ax = c.X; bx = pp.X + CardWidth; dirA = -1; dirB = 1; }
                    else if (pp.X >= c.X + CardWidth) { ax = c.X + CardWidth; bx = pp.X; dirA = 1; dirB = -1; }
                    else { ax = c.X; bx = pp.X; dirA = -1; dirB = -1; }
                    double bend = Math.Max(50, Math.Abs(bx - ax) / 2);
                    sb.Append($"    <path d=\"M {N(ax)} {N(ay)} C {N(ax + dirA * bend)} {N(ay)}, {N(bx + dirB * bend)} {N(by)}, {N(bx)} {N(by)}\"/>\n");
                    heads.Append($"  <polygon fill=\"{Edge}\" points=\"{N(bx)},{N(by)} {N(bx + dirB * 9)},{N(by - 5)} {N(bx + dirB * 9)},{N(by + 5)}\"/>\n");
                    heads.Append($"  <circle fill=\"{Fk}\" cx=\"{N(ax)}\" cy=\"{N(ay)}\" r=\"3.5\"/>\n");
                }
            }
            sb.Append("  </g>\n");
            sb.Append(heads);

            foreach (var t in tables)
            {
                var (x, y) = At(Qualified(t));
                double h = CardHeight(t);
                var fkCols = new HashSet<string>((t.References ?? new()).Select(r => r.ForeignKey ?? ""), StringComparer.OrdinalIgnoreCase);
                sb.Append($"  <g>\n");
                sb.Append($"    <rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(CardWidth)}\" height=\"{N(h)}\" rx=\"8\" fill=\"{Paper}\" stroke=\"{Header}\"/>\n");
                sb.Append($"    <path d=\"M {N(x)} {N(y + HeaderHeight)} V {N(y + 8)} Q {N(x)} {N(y)} {N(x + 8)} {N(y)} H {N(x + CardWidth - 8)} Q {N(x + CardWidth)} {N(y)} {N(x + CardWidth)} {N(y + 8)} V {N(y + HeaderHeight)} Z\" fill=\"{Header}\"/>\n");
                sb.Append($"    <text x=\"{N(x + 10)}\" y=\"{N(y + 22)}\" font-size=\"13\" fill=\"{Cream}\">{X(Clip(Qualified(t), 30))}</text>\n");
                int i = 0;
                foreach (var r in t.Rows ?? new List<RowCreation>())
                {
                    double ry = y + HeaderHeight + i * RowHeight + 15;
                    bool isFk = fkCols.Contains(r.Name ?? "");
                    string badge = r.IsPrimary == true ? "PK" : isFk ? "FK" : r.IsUnique == true ? "UQ" : "";
                    string badgeColor = r.IsPrimary == true ? Pk : isFk ? Fk : Muted;
                    if (badge.Length > 0)
                        sb.Append($"    <text x=\"{N(x + 10)}\" y=\"{N(ry)}\" font-size=\"9\" font-weight=\"bold\" fill=\"{badgeColor}\">{badge}</text>\n");
                    var name = (r.Name ?? "") + (r.IsNotNull == true || r.IsPrimary == true ? "" : "?");
                    sb.Append($"    <text x=\"{N(x + 40)}\" y=\"{N(ry)}\" font-size=\"12\" fill=\"{Ink}\">{X(Clip(name, 20))}</text>\n");
                    var type = Row.TypeWithLimit(r.RowType?.ToString() ?? (r.Media == true ? "SecureMedia" : "Text"), r.Limit).ToLowerInvariant() + (r.IsArray == true ? "[]" : "");
                    sb.Append($"    <text x=\"{N(x + CardWidth - 10)}\" y=\"{N(ry)}\" font-size=\"11\" text-anchor=\"end\" fill=\"{Muted}\">{X(Clip(type, 16))}</text>\n");
                    i++;
                }
                if (i == 0)
                    sb.Append($"    <text x=\"{N(x + 10)}\" y=\"{N(y + HeaderHeight + 15)}\" font-size=\"11\" fill=\"{Muted}\">no columns yet</text>\n");
                sb.Append("  </g>\n");
            }
            sb.Append("</svg>\n");
            return sb.ToString();
        }

        private static string Clip(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        private static string N(double v) => Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);

        private static string X(string s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
