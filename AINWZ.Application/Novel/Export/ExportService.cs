using System.Text;
using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Application.Novel.Export;

public class ExportService(SpeakEaseDbContext db)
{
    public async Task<(byte[] Content, string FileName, string ContentType)> ExportTxtAsync(
        string workId, CancellationToken ct = default)
    {
        var work = await db.Works.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workId, ct)
            ?? throw new InvalidOperationException($"作品(id={workId})不存在");

        var chapters = await db.Chapters.AsNoTracking()
            .Where(c => c.WorkId == workId && c.Status == "published")
            .OrderBy(c => c.Sequence)
            .ToListAsync(ct);

        if (chapters.Count == 0)
        {
            chapters = await db.Chapters.AsNoTracking()
                .Where(c => c.WorkId == workId)
                .OrderBy(c => c.Sequence)
                .ToListAsync(ct);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"《{work.Title}》");
        sb.AppendLine();
        sb.AppendLine($"作者：佚名");
        if (!string.IsNullOrWhiteSpace(work.Genre))
            sb.AppendLine($"题材：{work.Genre}");
        sb.AppendLine($"总字数：{work.TotalWordCount}");
        sb.AppendLine();
        sb.AppendLine(new string('=', 40));
        sb.AppendLine();

        foreach (var chapter in chapters)
        {
            sb.AppendLine($"第{chapter.Sequence}章 {chapter.Title}");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(chapter.Content))
            {
                var text = HtmlToPlainText(chapter.Content);
                sb.AppendLine(text);
            }

            sb.AppendLine();
            sb.AppendLine(new string('-', 30));
            sb.AppendLine();
        }

        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"{SanitizeFileName(work.Title)}.txt";
        return (content, fileName, "text/plain; charset=utf-8");
    }

    public async Task<(byte[] Content, string FileName, string ContentType)> ExportEpubAsync(
        string workId, CancellationToken ct = default)
    {
        var work = await db.Works.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workId, ct)
            ?? throw new InvalidOperationException($"作品(id={workId})不存在");

        var chapters = await db.Chapters.AsNoTracking()
            .Where(c => c.WorkId == workId && c.Status == "published")
            .OrderBy(c => c.Sequence)
            .ToListAsync(ct);

        if (chapters.Count == 0)
        {
            chapters = await db.Chapters.AsNoTracking()
                .Where(c => c.WorkId == workId)
                .OrderBy(c => c.Sequence)
                .ToListAsync(ct);
        }

        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var mimetypeEntry = archive.CreateEntry("mimetype", System.IO.Compression.CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mimetypeEntry.Open()))
                writer.Write("application/epub+zip");

            var metaInf = archive.CreateEntry("META-INF/container.xml");
            using (var writer = new StreamWriter(metaInf.Open()))
                writer.Write("""
<?xml version="1.0" encoding="UTF-8"?>
<container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
  <rootfiles>
    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
  </rootfiles>
</container>
""");

            var tocNcx = archive.CreateEntry("OEBPS/toc.ncx");
            using (var writer = new StreamWriter(tocNcx.Open()))
            {
                writer.Write($"""
<?xml version="1.0" encoding="UTF-8"?>
<ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
  <head><meta name="dtb:uid" content="urn:uuid:{workId}"/></head>
  <docTitle><text>{EscapeXml(work.Title)}</text></docTitle>
  <navMap>
""");
                for (var i = 0; i < chapters.Count; i++)
                {
                    writer.Write($"""
    <navPoint id="ch{i + 1}" playOrder="{i + 1}">
      <navLabel><text>{EscapeXml(chapters[i].Title ?? $"第{i + 1}章")}</text></navLabel>
      <content src="chapter{i + 1}.xhtml"/>
    </navPoint>
""");
                }
                writer.Write("  </navMap>\n</ncx>");
            }

            var contentOpf = archive.CreateEntry("OEBPS/content.opf");
            using (var writer = new StreamWriter(contentOpf.Open()))
            {
                writer.Write($"""
<?xml version="1.0" encoding="UTF-8"?>
<package xmlns="http://www.idpf.org/2007/opf" unique-identifier="BookId" version="3.0">
  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
    <dc:identifier id="BookId">urn:uuid:{workId}</dc:identifier>
    <dc:title>{EscapeXml(work.Title)}</dc:title>
    <dc:creator>佚名</dc:creator>
    <dc:language>zh-CN</dc:language>
    <meta property="dcterms:modified">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</meta>
  </metadata>
  <manifest>
    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
""");
                for (var i = 0; i < chapters.Count; i++)
                    writer.Write($"    <item id=\"ch{i + 1}\" href=\"chapter{i + 1}.xhtml\" media-type=\"application/xhtml+xml\"/>\n");
                writer.Write("  </manifest>\n  <spine toc=\"ncx\">\n");
                for (var i = 0; i < chapters.Count; i++)
                    writer.Write($"    <itemref idref=\"ch{i + 1}\"/>\n");
                writer.Write("  </spine>\n</package>");
            }

            for (var i = 0; i < chapters.Count; i++)
            {
                var chapter = chapters[i];
                var chapterEntry = archive.CreateEntry($"OEBPS/chapter{i + 1}.xhtml");
                using var writer = new StreamWriter(chapterEntry.Open(), Encoding.UTF8);
                var bodyHtml = PlainTextToXhtml(chapter.Content ?? "");
                writer.Write($"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml" xml:lang="zh-CN">
<head><title>{EscapeXml(chapter.Title ?? $"第{i + 1}章")}</title></head>
<body>
<h1>{EscapeXml(chapter.Title ?? $"第{i + 1}章")}</h1>
{bodyHtml}
</body>
</html>
""");
            }
        }

        var content = ms.ToArray();
        var fileName = $"{SanitizeFileName(work.Title)}.epub";
        return (content, fileName, "application/epub+zip");
    }

    private static string HtmlToPlainText(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<br\\s*/?>", "\n");
        text = System.Text.RegularExpressions.Regex.Replace(text, "</p>", "\n\n");
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        return text.Trim();
    }

    private static string PlainTextToXhtml(string text)
    {
        var sb = new StringBuilder();
        var paragraphs = text.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paragraphs)
        {
            var trimmed = p.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            sb.AppendLine($"  <p>{EscapeXml(trimmed)}</p>");
        }
        return sb.ToString();
    }

    private static string EscapeXml(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray());
    }
}
