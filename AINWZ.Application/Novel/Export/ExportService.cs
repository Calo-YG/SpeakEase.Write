using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Application.Novel.Export;

public class ExportService(SpeakEaseDbContext db)
{
    public async Task<(byte[] Content, string FileName, string ContentType)> ExportTxtAsync(
        string workId, int? startSequence = null, int? endSequence = null, CancellationToken ct = default)
    {
        var work = await db.Works.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workId, ct)
            ?? throw new InvalidOperationException($"作品(id={workId})不存在");

        var chapters = await QueryChaptersAsync(workId, startSequence, endSequence, ct);

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
        var fileName = BuildFileName(work.Title, "txt", startSequence, endSequence);
        return (content, fileName, "text/plain; charset=utf-8");
    }

    public async Task<(byte[] Content, string FileName, string ContentType)> ExportEpubAsync(
        string workId, int? startSequence = null, int? endSequence = null, CancellationToken ct = default)
    {
        var work = await db.Works.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workId, ct)
            ?? throw new InvalidOperationException($"作品(id={workId})不存在");

        var chapters = await QueryChaptersAsync(workId, startSequence, endSequence, ct);

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
      <navLabel><text>{EscapeXml(chapters[i].Title ?? $"第{chapters[i].Sequence}章")}</text></navLabel>
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
    <meta property="dcterms:modified">{DateTime.Now:yyyy-MM-ddTHH:mm:ssZ}</meta>
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
<head><title>{EscapeXml(chapter.Title ?? $"第{chapter.Sequence}章")}</title></head>
<body>
<h1>{EscapeXml(chapter.Title ?? $"第{chapter.Sequence}章")}</h1>
{bodyHtml}
</body>
</html>
""");
            }
        }

        var content = ms.ToArray();
        var fileName = BuildFileName(work.Title, "epub", startSequence, endSequence);
        return (content, fileName, "application/epub+zip");
    }

    private async Task<List<Domain.Entities.Works.ChapterEntity>> QueryChaptersAsync(
        string workId, int? startSequence, int? endSequence, CancellationToken ct)
    {
        var query = db.Chapters.AsNoTracking().Where(c => c.WorkId == workId);

        if (startSequence.HasValue)
            query = query.Where(c => c.Sequence >= startSequence.Value);
        if (endSequence.HasValue)
            query = query.Where(c => c.Sequence <= endSequence.Value);

        var chapters = await query
            .Where(c => c.Status == "published")
            .OrderBy(c => c.Sequence)
            .ToListAsync(ct);

        if (chapters.Count == 0)
        {
            query = db.Chapters.AsNoTracking().Where(c => c.WorkId == workId);
            if (startSequence.HasValue)
                query = query.Where(c => c.Sequence >= startSequence.Value);
            if (endSequence.HasValue)
                query = query.Where(c => c.Sequence <= endSequence.Value);

            chapters = await query.OrderBy(c => c.Sequence).ToListAsync(ct);
        }

        return chapters;
    }

    private static string BuildFileName(string title, string ext, int? startSequence, int? endSequence)
    {
        var baseName = SanitizeFileName(title);
        if (startSequence.HasValue && endSequence.HasValue)
            return $"{baseName}_第{startSequence}-{endSequence}章.{ext}";
        if (startSequence.HasValue)
            return $"{baseName}_第{startSequence}章起.{ext}";
        if (endSequence.HasValue)
            return $"{baseName}_至第{endSequence}章.{ext}";
        return $"{baseName}.{ext}";
    }

    private static string HtmlToPlainText(string html)
    {
        var text = Regex.Replace(html, "<br\\s*/?>", "\n");
        text = Regex.Replace(text, "</p>", "\n\n");
        text = Regex.Replace(text, "<[^>]+>", "");
        text = WebUtility.HtmlDecode(text);
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
        return new string([.. name.Where(c => !invalid.Contains(c))]);
    }
}
