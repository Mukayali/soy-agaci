using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using FamilyTree.Data;
using FamilyTree.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Services;

public class GenderReferenceService : IGenderReferenceService
{
    private const int MaxRows = 100_000;

    private static readonly CultureInfo Turkish = new("tr-TR");

    private static readonly XNamespace SpreadsheetNs =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<GenderReferenceService> _logger;

    public GenderReferenceService(ApplicationDbContext context, ILogger<GenderReferenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<int> GetReferenceCountAsync() => _context.AdCinsiyetler.CountAsync();

    public Task<List<AdCinsiyet>> GetReferenceSampleAsync(int limit = 25) =>
        _context.AdCinsiyetler.AsNoTracking().OrderBy(a => a.Ad).Take(limit).ToListAsync();

    public async Task<int> ClearReferenceAsync()
    {
        var all = await _context.AdCinsiyetler.ToListAsync();
        _context.AdCinsiyetler.RemoveRange(all);
        await _context.SaveChangesAsync();
        return all.Count;
    }

    public async Task<GenderImportResult> ImportReferenceAsync(Stream fileStream, string extension)
    {
        var result = new GenderImportResult();

        List<string[]> rows;
        try
        {
            rows = extension == ".csv"
                ? await ReadCsvRowsAsync(fileStream)
                : ReadXlsxRows(fileStream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cinsiyet referans dosyası ayrıştırılamadı.");
            result.ErrorMessage = "Dosya ayrıştırılamadı. Geçerli bir .xlsx veya .csv dosyası yükleyin.";
            return result;
        }

        if (rows.Count == 0)
        {
            result.ErrorMessage = "Dosya boş görünüyor.";
            return result;
        }

        // Sütun konumlarını başlık satırından bul; başlık yoksa 0 = Ad, 1 = Cinsiyet varsay.
        var (adCol, cinsCol, dataStart) = ResolveColumns(rows[0]);

        var parsed = new Dictionary<string, Gender>();
        var seenRaw = new HashSet<string>(StringComparer.Ordinal);

        for (var r = dataStart; r < rows.Count && r - dataStart < MaxRows; r++)
        {
            var row = rows[r];
            var adRaw = adCol < row.Length ? row[adCol]?.Trim() ?? string.Empty : string.Empty;
            var cinsRaw = cinsCol < row.Length ? row[cinsCol] : null;

            if (string.IsNullOrWhiteSpace(adRaw))
            {
                continue;
            }

            var gender = ParseGender(cinsRaw);
            if (gender == null)
            {
                result.Skipped++;
                if (result.Warnings.Count < 30)
                {
                    result.Warnings.Add($"Satır {r + 1}: '{adRaw}' için cinsiyet okunamadı ('{cinsRaw}'), atlandı.");
                }

                continue;
            }

            var key = NormalizeName(adRaw);
            if (key.Length == 0)
            {
                continue;
            }

            if (!seenRaw.Add(key) && parsed.TryGetValue(key, out var prev) && prev != gender)
            {
                result.Warnings.Add($"'{adRaw}' dosyada birden fazla farklı cinsiyetle geçiyor; sonuncusu kullanıldı.");
            }

            parsed[key] = gender.Value;
        }

        if (parsed.Count == 0)
        {
            result.ErrorMessage = "Dosyadan geçerli hiçbir 'ad + cinsiyet' satırı okunamadı. " +
                "İlk sütun ad, ikinci sütun cinsiyet (Erkek/Kadın) olmalı.";
            return result;
        }

        var existing = await _context.AdCinsiyetler.ToDictionaryAsync(a => a.Ad);

        foreach (var (name, gender) in parsed)
        {
            if (existing.TryGetValue(name, out var row))
            {
                if (row.Cinsiyet != gender)
                {
                    row.Cinsiyet = gender;
                    row.UpdatedAt = DateTime.UtcNow;
                    result.Updated++;
                }
            }
            else
            {
                _context.AdCinsiyetler.Add(new AdCinsiyet { Ad = name, Cinsiyet = gender });
                result.Added++;
            }
        }

        await _context.SaveChangesAsync();
        result.Success = true;
        return result;
    }

    public async Task<GenderMatchResult> MatchByNamesAsync(bool overwriteExisting)
    {
        var result = new GenderMatchResult();

        var reference = await _context.AdCinsiyetler.AsNoTracking()
            .Select(a => new { a.Ad, a.Cinsiyet })
            .ToListAsync();

        if (reference.Count == 0)
        {
            return result;
        }

        var map = new Dictionary<string, Gender>(StringComparer.Ordinal);
        foreach (var r in reference)
        {
            map[r.Ad] = r.Cinsiyet;
        }

        var query = _context.Persons.AsQueryable();
        if (!overwriteExisting)
        {
            query = query.Where(p => p.Cinsiyet == null);
        }

        var persons = await query.ToListAsync();

        foreach (var person in persons)
        {
            var match = LookupGender(map, person.Ad);
            if (match == null)
            {
                result.NoMatch++;
                continue;
            }

            if (person.Cinsiyet == match)
            {
                result.AlreadyCorrect++;
                continue;
            }

            person.Cinsiyet = match;
            person.UpdatedAt = DateTime.UtcNow;
            result.Updated++;
        }

        if (result.Updated > 0)
        {
            await _context.SaveChangesAsync();
        }

        return result;
    }

    public async Task<(List<Person> Persons, int TotalCount)> GetMissingGenderAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var baseQuery = _context.Persons.AsNoTracking().Where(p => p.Cinsiyet == null);

        var total = await baseQuery.CountAsync();

        var persons = await baseQuery
            .Include(p => p.Anne)
            .Include(p => p.Baba)
            .OrderBy(p => p.Ad).ThenBy(p => p.Soyad).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (persons, total);
    }

    public async Task<int> BulkSetGendersAsync(IDictionary<int, Gender> assignments)
    {
        if (assignments.Count == 0)
        {
            return 0;
        }

        var ids = assignments.Keys.ToList();
        var persons = await _context.Persons.Where(p => ids.Contains(p.Id)).ToListAsync();

        var count = 0;
        foreach (var person in persons)
        {
            if (assignments.TryGetValue(person.Id, out var gender) && person.Cinsiyet != gender)
            {
                person.Cinsiyet = gender;
                person.UpdatedAt = DateTime.UtcNow;
                count++;
            }
        }

        if (count > 0)
        {
            await _context.SaveChangesAsync();
        }

        return count;
    }

    // ---- Yardımcılar ------------------------------------------------------

    /// <summary>Türkçe kültürüyle BÜYÜK harfe çevirir, iç boşlukları tekleştirir.</summary>
    private static string NormalizeName(string value)
    {
        var collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.ToUpper(Turkish);
    }

    /// <summary>Önce tam adı, bulunamazsa ilk kelimeyi referansta arar.</summary>
    private static Gender? LookupGender(Dictionary<string, Gender> map, string personAd)
    {
        var full = NormalizeName(personAd ?? string.Empty);
        if (full.Length == 0)
        {
            return null;
        }

        if (map.TryGetValue(full, out var g))
        {
            return g;
        }

        var space = full.IndexOf(' ');
        if (space > 0 && map.TryGetValue(full[..space], out var firstTokenGender))
        {
            return firstTokenGender;
        }

        return null;
    }

    private static Gender? ParseGender(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = raw.Trim().ToLowerInvariant().Replace("ı", "i").Replace("ğ", "g");

        return s switch
        {
            "erkek" or "e" or "m" or "male" or "bay" or "b" or "1" => Gender.Erkek,
            "kadin" or "kadın" or "k" or "f" or "female" or "bayan" or "2" => Gender.Kadin,
            _ => null,
        };
    }

    private static (int AdCol, int CinsCol, int DataStart) ResolveColumns(string[] header)
    {
        int adCol = -1, cinsCol = -1;
        for (var i = 0; i < header.Length; i++)
        {
            var h = (header[i] ?? string.Empty).Trim().ToLowerInvariant()
                .Replace("ı", "i").Replace("ğ", "g").Replace("ş", "s");

            if (adCol < 0 && (h is "ad" or "isim" or "adi" or "ad soyad" or "name" or "first name" or "firstname"))
            {
                adCol = i;
            }
            else if (cinsCol < 0 && (h is "cinsiyet" or "cins" or "gender" or "sex"))
            {
                cinsCol = i;
            }
        }

        if (adCol >= 0 && cinsCol >= 0)
        {
            return (adCol, cinsCol, 1);
        }

        // Başlık tanınmadı: ilk satır da veri kabul edilir, ilk iki sütun ad/cinsiyet.
        return (0, 1, 0);
    }

    private static async Task<List<string[]>> ReadCsvRowsAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync();
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        var lines = text.Split('\n');
        var rows = new List<string[]>();
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var delimiter = line.Count(c => c == ';') > line.Count(c => c == ',') ? ';' : ',';
            rows.Add(SplitCsvLine(line, delimiter));
        }

        return rows;
    }

    private static string[] SplitCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    /// <summary>
    /// .xlsx (OpenXML) dosyasından ilk çalışma sayfasının satırlarını okur. Ek kütüphane
    /// kullanmamak için ZipArchive + XDocument ile elle ayrıştırılır (proje CSV/GEDCOM'u da
    /// elle ayrıştırıyor). Paylaşımlı dize tablosu ve inline string hücreleri desteklenir.
    /// </summary>
    private static List<string[]> ReadXlsxRows(Stream stream)
    {
        // ZipArchive akışın Seek edilebilmesini gerektirir; IFormFile akışı genelde edilebilir,
        // yine de garanti için belleğe alıyoruz (dosya küçük — sadece ad + cinsiyet).
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);

        var sharedStrings = new List<string>();
        var sstEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sstEntry != null)
        {
            using var sstStream = sstEntry.Open();
            var sstDoc = XDocument.Load(sstStream);
            foreach (var si in sstDoc.Root!.Elements(SpreadsheetNs + "si"))
            {
                sharedStrings.Add(string.Concat(si.Descendants(SpreadsheetNs + "t").Select(t => t.Value)));
            }
        }

        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? archive.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
                e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

        if (sheetEntry == null)
        {
            return new List<string[]>();
        }

        using var sheetStream = sheetEntry.Open();
        var sheetDoc = XDocument.Load(sheetStream);
        var sheetData = sheetDoc.Root?.Element(SpreadsheetNs + "sheetData");
        if (sheetData == null)
        {
            return new List<string[]>();
        }

        var rows = new List<string[]>();
        foreach (var row in sheetData.Elements(SpreadsheetNs + "row"))
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements(SpreadsheetNs + "c"))
            {
                var reference = cell.Attribute("r")?.Value;
                var colIndex = reference != null ? ColumnIndex(reference) : cells.Count;
                while (cells.Count < colIndex)
                {
                    cells.Add(string.Empty);
                }

                var type = cell.Attribute("t")?.Value;
                string value;
                if (type == "s")
                {
                    value = int.TryParse(cell.Element(SpreadsheetNs + "v")?.Value, out var idx)
                        && idx >= 0 && idx < sharedStrings.Count
                        ? sharedStrings[idx]
                        : string.Empty;
                }
                else if (type == "inlineStr")
                {
                    value = string.Concat(
                        cell.Element(SpreadsheetNs + "is")?.Descendants(SpreadsheetNs + "t").Select(t => t.Value)
                        ?? Enumerable.Empty<string>());
                }
                else
                {
                    value = cell.Element(SpreadsheetNs + "v")?.Value ?? string.Empty;
                }

                cells.Add(value);
            }

            if (cells.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                rows.Add(cells.ToArray());
            }
        }

        return rows;
    }

    /// <summary>"B3" gibi bir hücre referansındaki sütunu 0 tabanlı indekse çevirir.</summary>
    private static int ColumnIndex(string cellReference)
    {
        var col = 0;
        foreach (var c in cellReference)
        {
            if (!char.IsLetter(c))
            {
                break;
            }

            col = col * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return Math.Max(0, col - 1);
    }
}
