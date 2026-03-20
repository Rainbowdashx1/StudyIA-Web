using StudyIA_Web.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Outline;

namespace StudyIA_Web.Services;

/// <summary>
/// Extrae texto y estructura de archivos PDF usando PdfPig (compatible con Blazor WASM).
/// Todos los métodos reciben los bytes del PDF en lugar de una ruta de archivo.
/// </summary>
public static class PdfTextService
{
    // ── Extracción de texto por páginas ────────────────────────────────────

    /// <summary>
    /// Devuelve pares (número de página, texto), omitiendo páginas en blanco o ilegibles.
    /// </summary>
    public static List<(int Page, string Text)> ExtractPages(byte[] pdfBytes)
    {
        var pages = new List<(int, string)>();
        try
        {
            using var doc = PdfDocument.Open(pdfBytes);
            foreach (var page in doc.GetPages())
            {
                try
                {
                    var text = string.Join(" ", page.GetWords().Select(w => w.Text));
                    if (!string.IsNullOrWhiteSpace(text))
                        pages.Add((page.Number, text));
                }
                catch { /* página ilegible */ }
            }
        }
        catch { /* PDF cifrado o inaccesible */ }

        return pages;
    }

    /// <summary>
    /// Recorta la lista de páginas hasta un máximo de <paramref name="maxChars"/> caracteres
    /// preservando los números de página originales.
    /// </summary>
    public static List<(int Page, string Text)> Trim(
        List<(int Page, string Text)> pages, int maxChars)
    {
        var result = new List<(int, string)>();
        var total  = 0;

        foreach (var (page, text) in pages)
        {
            if (total >= maxChars) break;
            var take = Math.Min(text.Length, maxChars - total);
            result.Add((page, text[..take]));
            total += take;
        }

        return result;
    }

    /// <summary>
    /// Construye un resumen ligero para extracción de temas.
    /// Prioriza líneas cortas (3-80 chars) que probablemente sean encabezados;
    /// si no encuentra ninguna, usa un fragmento del centro de la página.
    /// </summary>
    public static string BuildLightSummary(
        List<(int Page, string Text)> pages, int budgetPerPage = 280)
    {
        return string.Join("\n", pages.Select(p =>
        {
            var lines   = p.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var heading = lines.FirstOrDefault(l => l.Trim().Length is >= 3 and <= 80)
                          ?? string.Empty;
            var midStart = Math.Max(0, p.Text.Length / 2 - budgetPerPage / 2);
            var snippet  = heading.Length > 0
                ? heading.Trim()
                : p.Text.Substring(midStart,
                      Math.Min(budgetPerPage, p.Text.Length - midStart)).Trim();
            return $"Pag.{p.Page}: {snippet}";
        }));
    }

    /// <summary>
    /// Divide las páginas en bloques de como máximo <paramref name="chunkSize"/> caracteres.
    /// Las páginas nunca se parten; una página única demasiado grande obtiene su propio bloque truncado.
    /// </summary>
    public static List<List<(int Page, string Text)>> Chunk(
        List<(int Page, string Text)> pages, int chunkSize = 12_000)
    {
        var chunks  = new List<List<(int Page, string Text)>>();
        var current = new List<(int Page, string Text)>();
        var total   = 0;

        foreach (var (page, text) in pages)
        {
            var safe = text.Length > chunkSize ? text[..chunkSize] : text;

            if (total + safe.Length > chunkSize && current.Count > 0)
            {
                chunks.Add(current);
                current = [];
                total   = 0;
            }

            current.Add((page, safe));
            total += safe.Length;
        }

        if (current.Count > 0)
            chunks.Add(current);

        return chunks;
    }

    // ── Outline / Tabla de contenidos ─────────────────────────────────────

    /// <summary>
    /// Construye un índice estructural del PDF como lista ordenada de secciones,
    /// cada una con título, página de inicio y página de fin.
    ///
    /// Estrategia (aplicada en orden):
    ///   1. Lee los bookmarks embebidos del PDF cuando están presentes.
    ///   2. Si no hay bookmarks, detecta encabezados heurísticamente:
    ///      en cada página las letras con el mayor PointSize (que superen un 10 %
    ///      la mediana) se reconstruyen como fragmento de texto dominante y se
    ///      toman como título de sección. Páginas consecutivas con el mismo
    ///      título se fusionan en una sola sección.
    /// </summary>
    public static List<PdfSection> ExtractOutline(byte[] pdfBytes)
    {
        try
        {
            using var doc = PdfDocument.Open(pdfBytes);
            var totalPages = doc.NumberOfPages;

            // 1. Intentar bookmarks embebidos
            if (doc.TryGetBookmarks(out var bookmarks))
            {
                var fromBookmarks = FlattenBookmarks(bookmarks.Roots, totalPages);
                if (fromBookmarks.Count > 0)
                    return fromBookmarks;
            }

            // 2. Detección heurística por tamaño de fuente
            return ExtractOutlineHeuristic(doc);
        }
        catch
        {
            return [];
        }
    }

    // ── Extracción por bookmarks ───────────────────────────────────────────

    private static List<PdfSection> FlattenBookmarks(
        IReadOnlyList<BookmarkNode> nodes, int totalPages)
    {
        var flat = new List<(string Title, int Page)>();
        Flatten(nodes, flat);
        if (flat.Count == 0) return [];
        return FinalizeOutline(flat, totalPages);
    }

    private static void Flatten(IReadOnlyList<BookmarkNode> nodes, List<(string, int)> result)
    {
        foreach (var node in nodes)
        {
            if (node is DocumentBookmarkNode docNode && docNode.PageNumber > 0)
                result.Add((docNode.Title, docNode.PageNumber));

            if (node.Children?.Count > 0)
                Flatten(node.Children, result);
        }
    }

    // ── Extracción heurística ──────────────────────────────────────────────

    private static List<PdfSection> ExtractOutlineHeuristic(PdfDocument doc)
    {
        var candidates = new List<(string Title, int Page)>();

        foreach (var page in doc.GetPages())
        {
            try
            {
                var heading = GetDominantHeading(page.Letters);
                if (!string.IsNullOrWhiteSpace(heading))
                    candidates.Add((heading, page.Number));
            }
            catch { /* página ilegible */ }
        }

        // Fusionar páginas consecutivas con el mismo encabezado
        var merged = new List<(string Title, int Page)>();
        foreach (var (title, page) in candidates)
        {
            if (merged.Count > 0 &&
                string.Equals(merged[^1].Title, title, StringComparison.OrdinalIgnoreCase))
                continue;
            merged.Add((title, page));
        }

        return FinalizeOutline(merged, doc.NumberOfPages);
    }

    /// <summary>
    /// Equivalente al FontSizeListener de iText7 pero usando las <see cref="Letter"/>s de PdfPig.
    /// Agrupa las letras con el mayor PointSize (≥ 97 % del máximo y > 110 % de la mediana),
    /// las ordena por posición X y las une insertando un espacio cuando el gap horizontal
    /// supera el 25 % del tamaño de fuente.
    /// </summary>
    private static string GetDominantHeading(IReadOnlyList<Letter> letters)
    {
        if (letters.Count == 0) return string.Empty;

        var maxSize = letters.Max(l => l.PointSize);
        var sorted  = letters.Select(l => l.PointSize).Order().ToList();
        var median  = sorted[sorted.Count / 2];

        var candidates = letters
            .Where(l => l.PointSize >= maxSize * 0.97f &&
                        l.PointSize >  median  * 1.10f)
            .OrderBy(l => l.StartBaseLine.X)
            .ToList();

        if (candidates.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < candidates.Count; i++)
        {
            var letter = candidates[i];
            if (i > 0)
            {
                var prev = candidates[i - 1];
                var gap  = letter.StartBaseLine.X - prev.EndBaseLine.X;
                if (gap > letter.PointSize * 0.25f)
                    sb.Append(' ');
            }
            sb.Append(letter.Value);
        }

        var joined = sb.ToString().Trim();
        return joined.Length is >= 3 and <= 100 ? joined : string.Empty;
    }

    // ── Helper compartido ──────────────────────────────────────────────────

    private static List<PdfSection> FinalizeOutline(
        List<(string Title, int Page)> raw, int totalPages)
    {
        if (raw.Count == 0)
            return BuildAutoSections(totalPages);

        var sections = new List<PdfSection>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            sections.Add(new PdfSection
            {
                Title     = raw[i].Title,
                StartPage = raw[i].Page,
                EndPage   = i + 1 < raw.Count
                    ? raw[i + 1].Page - 1
                    : totalPages
            });
        }
        return sections;
    }

    private static List<PdfSection> BuildAutoSections(int totalPages)
    {
        const int pageSize = 10;
        var sections = new List<PdfSection>();
        for (var start = 1; start <= totalPages; start += pageSize)
        {
            var end = Math.Min(start + pageSize - 1, totalPages);
            sections.Add(new PdfSection
            {
                Title     = $"Páginas {start}–{end}",
                StartPage = start,
                EndPage   = end
            });
        }
        return sections;
    }
}
