using System.Globalization;
using System.Text;
using FamilyTree.Data;
using FamilyTree.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Services;

public class GedcomService : IGedcomService
{
    private static readonly string[] GedcomMonths =
    {
        "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC",
    };

    private const int MaxIndividuals = 5000;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<GedcomService> _logger;

    public GedcomService(ApplicationDbContext context, ILogger<GedcomService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // DIŞA AKTARMA
    // ------------------------------------------------------------------

    public async Task<byte[]> ExportAsync()
    {
        var persons = await _context.Persons.AsNoTracking().ToListAsync();
        var spouseRelationships = await _context.SpouseRelationships.AsNoTracking().ToListAsync();
        var spousesFams = new Dictionary<int, List<int>>();

        var sb = new StringBuilder();

        sb.AppendLine("0 HEAD");
        sb.AppendLine("1 SOUR SoyAgaciYonetimSistemi");
        sb.AppendLine("1 GEDC");
        sb.AppendLine("2 VERS 5.5.1");
        sb.AppendLine("2 FORM LINEAGE-LINKED");
        sb.AppendLine("1 CHAR UTF-8");
        sb.AppendLine($"1 DATE {ToGedcomDate(DateTime.UtcNow)}");
        sb.AppendLine("1 SUBM @SUBM1@");
        sb.AppendLine("0 @SUBM1@ SUBM");
        sb.AppendLine("1 NAME Soy Agaci Yonetim Sistemi");

        // Aile grupları: (AnneId, BabaId) çiftine göre çocukları grupla.
        var familiesByParents = persons
            .Where(p => p.AnneId.HasValue || p.BabaId.HasValue)
            .GroupBy(p => (Anne: p.AnneId, Baba: p.BabaId))
            .ToList();

        var familyIndex = new Dictionary<(int? Anne, int? Baba), int>();
        var familyCounter = 0;
        var famLines = new StringBuilder();

        foreach (var group in familiesByParents)
        {
            familyCounter++;
            var famId = $"@F{familyCounter}@";
            familyIndex[group.Key] = familyCounter;

            famLines.AppendLine($"0 {famId} FAM");
            if (group.Key.Baba.HasValue)
            {
                famLines.AppendLine($"1 HUSB @I{group.Key.Baba.Value}@");
                SetSpousesFamsIndex(spousesFams, group.Key.Baba.Value, familyCounter);
            }

            if (group.Key.Anne.HasValue)
            {
                famLines.AppendLine($"1 WIFE @I{group.Key.Anne.Value}@");
                SetSpousesFamsIndex(spousesFams, group.Key.Anne.Value, familyCounter);
            }

            foreach (var child in group.OrderBy(c => c.DogumTarihi))
            {
                famLines.AppendLine($"1 CHIL @I{child.Id}@");
            }

            var marriage = FindSpouseRelationship(spouseRelationships, group.Key.Anne, group.Key.Baba);
            if (marriage?.MarriageDate != null)
            {
                famLines.AppendLine("1 MARR");
                famLines.AppendLine($"2 DATE {ToGedcomDate(marriage.MarriageDate.Value)}");
            }
        }

        // Ortak çocuğu olmayan eş ilişkileri için de ayrı FAM kayıtları oluştur.
        foreach (var sr in spouseRelationships)
        {
            var key = MakeSpouseKey(persons, sr.Person1Id, sr.Person2Id);
            if (key.HasValue && familyIndex.ContainsKey(key.Value))
            {
                continue; // zaten bir çocuk grubu üzerinden işlendi
            }

            familyCounter++;
            var famId = $"@F{familyCounter}@";

            var p1 = persons.FirstOrDefault(p => p.Id == sr.Person1Id);
            var p2 = persons.FirstOrDefault(p => p.Id == sr.Person2Id);
            if (p1 == null || p2 == null)
            {
                familyCounter--;
                continue;
            }

            var (husb, wife) = OrderAsHusbandWife(p1, p2);

            famLines.AppendLine($"0 {famId} FAM");
            famLines.AppendLine($"1 HUSB @I{husb.Id}@");
            famLines.AppendLine($"1 WIFE @I{wife.Id}@");
            if (sr.MarriageDate != null)
            {
                famLines.AppendLine("1 MARR");
                famLines.AppendLine($"2 DATE {ToGedcomDate(sr.MarriageDate.Value)}");
            }

            SetSpousesFamsIndex(spousesFams, husb.Id, familyCounter);
            SetSpousesFamsIndex(spousesFams, wife.Id, familyCounter);
        }

        // INDI kayıtları
        foreach (var person in persons.OrderBy(p => p.Id))
        {
            sb.AppendLine($"0 @I{person.Id}@ INDI");
            sb.AppendLine($"1 NAME {EscapeGedcomText(person.Ad)} /{EscapeGedcomText(person.Soyad)}/");

            var sex = person.Cinsiyet switch
            {
                Gender.Erkek => "M",
                Gender.Kadin => "F",
                _ => null,
            };
            if (sex != null)
            {
                sb.AppendLine($"1 SEX {sex}");
            }

            if (person.DogumTarihi.HasValue)
            {
                sb.AppendLine("1 BIRT");
                sb.AppendLine($"2 DATE {ToGedcomDate(person.DogumTarihi.Value)}");
            }

            if (person.OlumTarihi.HasValue)
            {
                sb.AppendLine("1 DEAT");
                sb.AppendLine($"2 DATE {ToGedcomDate(person.OlumTarihi.Value)}");
            }

            if (!string.IsNullOrWhiteSpace(person.Aciklama))
            {
                AppendGedcomNote(sb, person.Aciklama);
            }

            if (person.AnneId.HasValue || person.BabaId.HasValue)
            {
                var key = (person.AnneId, person.BabaId);
                if (familyIndex.TryGetValue(key, out var famNum))
                {
                    sb.AppendLine($"1 FAMC @F{famNum}@");
                }
            }

            if (spousesFams.TryGetValue(person.Id, out var famsList))
            {
                foreach (var famNum in famsList.Distinct())
                {
                    sb.AppendLine($"1 FAMS @F{famNum}@");
                }
            }
        }

        var finalGedcom = new StringBuilder();
        finalGedcom.Append(sb);
        finalGedcom.Append(famLines);
        finalGedcom.AppendLine("0 TRLR");

        return Encoding.UTF8.GetBytes(finalGedcom.ToString());
    }

    private static void SetSpousesFamsIndex(Dictionary<int, List<int>> map, int personId, int famNum)
    {
        if (!map.TryGetValue(personId, out var list))
        {
            list = new List<int>();
            map[personId] = list;
        }

        list.Add(famNum);
    }

    private static (int? Anne, int? Baba)? MakeSpouseKey(List<Person> persons, int person1Id, int person2Id)
    {
        var p1 = persons.FirstOrDefault(p => p.Id == person1Id);
        var p2 = persons.FirstOrDefault(p => p.Id == person2Id);
        if (p1 == null || p2 == null)
        {
            return null;
        }

        var (husb, wife) = OrderAsHusbandWife(p1, p2);
        return (wife.Id, husb.Id);
    }

    private static (Person Husb, Person Wife) OrderAsHusbandWife(Person p1, Person p2)
    {
        if (p1.Cinsiyet == Gender.Kadin || p2.Cinsiyet == Gender.Erkek)
        {
            return (p2, p1);
        }

        return (p1, p2);
    }

    private static SpouseRelationship? FindSpouseRelationship(List<SpouseRelationship> relationships, int? anneId, int? babaId)
    {
        if (!anneId.HasValue || !babaId.HasValue)
        {
            return null;
        }

        return relationships.FirstOrDefault(sr =>
            (sr.Person1Id == anneId && sr.Person2Id == babaId) ||
            (sr.Person1Id == babaId && sr.Person2Id == anneId));
    }

    private static void AppendGedcomNote(StringBuilder sb, string text)
    {
        const int chunkSize = 200;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var first = true;

        foreach (var line in lines)
        {
            var remaining = line;
            var isFirstChunkOfLine = true;

            do
            {
                var chunk = remaining.Length > chunkSize ? remaining[..chunkSize] : remaining;
                remaining = remaining.Length > chunkSize ? remaining[chunkSize..] : string.Empty;

                if (first)
                {
                    sb.AppendLine($"1 NOTE {EscapeGedcomText(chunk)}");
                    first = false;
                }
                else if (isFirstChunkOfLine)
                {
                    sb.AppendLine($"1 CONT {EscapeGedcomText(chunk)}");
                }
                else
                {
                    sb.AppendLine($"1 CONC {EscapeGedcomText(chunk)}");
                }

                isFirstChunkOfLine = false;
            }
            while (remaining.Length > 0);
        }
    }

    private static string EscapeGedcomText(string text) => text.Replace("\r", string.Empty).Replace("\n", " ");

    private static string ToGedcomDate(DateTime date) => $"{date:dd} {GedcomMonths[date.Month - 1]} {date:yyyy}";

    // ------------------------------------------------------------------
    // İÇE AKTARMA
    // ------------------------------------------------------------------

    public async Task<GedcomImportResult> ImportAsync(Stream gedcomContent)
    {
        var result = new GedcomImportResult();

        string text;
        using (var reader = new StreamReader(gedcomContent, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync();
        }

        List<GedcomLine> lines;
        try
        {
            lines = ParseLines(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GEDCOM dosyası ayrıştırılamadı.");
            result.ErrorMessage = "GEDCOM dosyası ayrıştırılamadı. Dosyanın geçerli bir GEDCOM (.ged) dosyası olduğundan emin olun.";
            return result;
        }

        var records = GroupIntoRecords(lines);
        var indiRecords = records.Where(r => r.Lines.Count > 0 && r.Lines[0].Tag == "INDI").ToList();
        var famRecords = records.Where(r => r.Lines.Count > 0 && r.Lines[0].Tag == "FAM").ToList();

        if (indiRecords.Count == 0)
        {
            result.ErrorMessage = "Dosyada hiç kişi (INDI) kaydı bulunamadı.";
            return result;
        }

        if (indiRecords.Count > MaxIndividuals)
        {
            result.ErrorMessage = $"Dosyada {indiRecords.Count} kişi bulunuyor; tek seferde en fazla {MaxIndividuals} kişi içe aktarılabilir.";
            return result;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var xrefToPersonId = new Dictionary<string, int>();

            foreach (var (xref, recordLines) in indiRecords)
            {
                var (ad, soyad) = ParseName(FindValue(recordLines, 1, "NAME"));
                var sexValue = FindValue(recordLines, 1, "SEX");
                Gender? gender = sexValue switch
                {
                    "M" => Gender.Erkek,
                    "F" => Gender.Kadin,
                    _ => null,
                };

                var (birthDate, birthNote) = ParseGedcomDateWithNote(FindNestedValue(recordLines, "BIRT", "DATE"));
                var (deathDate, deathNote) = ParseGedcomDateWithNote(FindNestedValue(recordLines, "DEAT", "DATE"));

                var noteParts = new List<string>();
                if (birthNote != null)
                {
                    noteParts.Add($"Doğum (GEDCOM'dan, kesin tarih çözümlenemedi): {birthNote}");
                }

                if (deathNote != null)
                {
                    noteParts.Add($"Ölüm (GEDCOM'dan, kesin tarih çözümlenemedi): {deathNote}");
                }

                var gedcomNote = FindValue(recordLines, 1, "NOTE");
                if (!string.IsNullOrWhiteSpace(gedcomNote))
                {
                    noteParts.Add(gedcomNote);
                }

                var person = new Person
                {
                    Ad = string.IsNullOrWhiteSpace(ad) ? "Bilinmiyor" : ad,
                    Soyad = soyad ?? string.Empty,
                    Cinsiyet = gender,
                    DogumTarihi = birthDate,
                    OlumTarihi = deathDate,
                    Aciklama = noteParts.Count > 0 ? string.Join(" | ", noteParts) : null,
                    CreatedAt = DateTime.UtcNow,
                };

                _context.Persons.Add(person);
                await _context.SaveChangesAsync();

                xrefToPersonId[xref] = person.Id;
                result.PersonsCreated++;
            }

            foreach (var (famXref, recordLines) in famRecords)
            {
                result.FamiliesProcessed++;

                var husbXref = FindValue(recordLines, 1, "HUSB");
                var wifeXref = FindValue(recordLines, 1, "WIFE");
                var chilXrefs = recordLines.Where(l => l.Level == 1 && l.Tag == "CHIL" && l.Value != null).Select(l => l.Value!).ToList();

                int? babaId = husbXref != null && xrefToPersonId.TryGetValue(husbXref, out var hId) ? hId : null;
                int? anneId = wifeXref != null && xrefToPersonId.TryGetValue(wifeXref, out var wId) ? wId : null;

                if (husbXref != null && babaId == null)
                {
                    result.Warnings.Add($"{famXref}: HUSB ({husbXref}) sistemde bulunamadı, atlandı.");
                }

                if (wifeXref != null && anneId == null)
                {
                    result.Warnings.Add($"{famXref}: WIFE ({wifeXref}) sistemde bulunamadı, atlandı.");
                }

                foreach (var chilXref in chilXrefs)
                {
                    if (!xrefToPersonId.TryGetValue(chilXref, out var childId))
                    {
                        result.Warnings.Add($"{famXref}: CHIL ({chilXref}) sistemde bulunamadı, atlandı.");
                        continue;
                    }

                    if (childId == anneId || childId == babaId)
                    {
                        result.Warnings.Add($"{famXref}: bir kişi kendi ebeveyni olarak işaretlenemez, atlandı ({chilXref}).");
                        continue;
                    }

                    var child = await _context.Persons.FindAsync(childId);
                    if (child == null)
                    {
                        continue;
                    }

                    if (anneId.HasValue)
                    {
                        child.AnneId = anneId;
                    }

                    if (babaId.HasValue)
                    {
                        child.BabaId = babaId;
                    }
                }

                if (anneId.HasValue && babaId.HasValue)
                {
                    var (marriageDate, _) = ParseGedcomDateWithNote(FindNestedValue(recordLines, "MARR", "DATE"));

                    var exists = await _context.SpouseRelationships.AnyAsync(sr =>
                        (sr.Person1Id == anneId && sr.Person2Id == babaId) ||
                        (sr.Person1Id == babaId && sr.Person2Id == anneId));

                    if (!exists)
                    {
                        _context.SpouseRelationships.Add(new SpouseRelationship
                        {
                            Person1Id = babaId.Value,
                            Person2Id = anneId.Value,
                            MarriageDate = marriageDate,
                        });
                        result.SpouseRelationshipsCreated++;
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "GEDCOM içe aktarma sırasında hata oluştu, tüm değişiklikler geri alındı.");
            result.Success = false;
            result.ErrorMessage = "İçe aktarma sırasında bir hata oluştu; hiçbir kayıt eklenmedi. Sunucu loglarını kontrol edin.";
            return result;
        }
    }

    private static (string Ad, string? Soyad) ParseName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return ("Bilinmiyor", null);
        }

        var slashStart = rawName.IndexOf('/');
        var slashEnd = rawName.LastIndexOf('/');

        if (slashStart >= 0 && slashEnd > slashStart)
        {
            var ad = rawName[..slashStart].Trim();
            var soyad = rawName[(slashStart + 1)..slashEnd].Trim();
            return (string.IsNullOrWhiteSpace(ad) ? "Bilinmiyor" : ad, soyad);
        }

        return (rawName.Trim(), null);
    }

    private static (DateTime? Date, string? RawIfUnparsed) ParseGedcomDateWithNote(string? rawDate)
    {
        if (string.IsNullOrWhiteSpace(rawDate))
        {
            return (null, null);
        }

        var cleaned = rawDate.Trim();

        // "12 MAR 1950" gibi tam tarihleri dene.
        var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 3 &&
            int.TryParse(tokens[0], out var day) &&
            Array.IndexOf(GedcomMonths, tokens[1].ToUpperInvariant()) >= 0 &&
            int.TryParse(tokens[2], out var year))
        {
            var month = Array.IndexOf(GedcomMonths, tokens[1].ToUpperInvariant()) + 1;
            try
            {
                return (new DateTime(year, month, day), null);
            }
            catch (ArgumentOutOfRangeException)
            {
                return (null, cleaned);
            }
        }

        // Yalnızca yıl (ör. "1950") ya da tahmini tarihler (ör. "ABT 1950", "BEF 1900") -
        // yanlış kesinlik oluşturmamak için tarih alanına yazmıyoruz, ham metni not olarak döndürüyoruz.
        return (null, cleaned);
    }

    private record GedcomLine(int Level, string? Xref, string Tag, string? Value);

    private static List<GedcomLine> ParseLines(string text)
    {
        var result = new List<GedcomLine>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim('\r', '﻿').TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            var firstSpace = line.IndexOf(' ');
            if (firstSpace < 0)
            {
                continue;
            }

            if (!int.TryParse(line[..firstSpace], out var level))
            {
                continue;
            }

            var rest = line[(firstSpace + 1)..].TrimStart();
            string? xref = null;
            string tag;
            string? value;

            if (rest.StartsWith('@'))
            {
                var xrefEnd = rest.IndexOf('@', 1);
                if (xrefEnd < 0)
                {
                    continue;
                }

                xref = rest[..(xrefEnd + 1)];
                var afterXref = rest[(xrefEnd + 1)..].TrimStart();
                SplitTagValue(afterXref, out tag, out value);
            }
            else
            {
                SplitTagValue(rest, out tag, out value);
            }

            result.Add(new GedcomLine(level, xref, tag, value));
        }

        return result;
    }

    private static void SplitTagValue(string text, out string tag, out string? value)
    {
        var spaceIndex = text.IndexOf(' ');
        if (spaceIndex < 0)
        {
            tag = text;
            value = null;
        }
        else
        {
            tag = text[..spaceIndex];
            value = text[(spaceIndex + 1)..];
        }
    }

    private static List<(string Xref, List<GedcomLine> Lines)> GroupIntoRecords(List<GedcomLine> lines)
    {
        var records = new List<(string Xref, List<GedcomLine> Lines)>();
        List<GedcomLine>? current = null;
        string? currentXref = null;

        foreach (var line in lines)
        {
            if (line.Level == 0)
            {
                if (current != null && currentXref != null)
                {
                    records.Add((currentXref, current));
                }

                current = line.Xref != null ? new List<GedcomLine> { line } : null;
                currentXref = line.Xref;
            }
            else
            {
                current?.Add(line);
            }
        }

        if (current != null && currentXref != null)
        {
            records.Add((currentXref, current));
        }

        return records;
    }

    private static string? FindValue(List<GedcomLine> lines, int level, string tag) =>
        lines.FirstOrDefault(l => l.Level == level && l.Tag == tag)?.Value;

    /// <summary>Ör. BIRT altındaki DATE değerini bulur (1 BIRT / 2 DATE ...).</summary>
    private static string? FindNestedValue(List<GedcomLine> lines, string parentTag, string childTag)
    {
        var parentIndex = lines.FindIndex(l => l.Level == 1 && l.Tag == parentTag);
        if (parentIndex < 0)
        {
            return null;
        }

        for (var i = parentIndex + 1; i < lines.Count; i++)
        {
            if (lines[i].Level <= 1)
            {
                break;
            }

            if (lines[i].Level == 2 && lines[i].Tag == childTag)
            {
                return lines[i].Value;
            }
        }

        return null;
    }
}
