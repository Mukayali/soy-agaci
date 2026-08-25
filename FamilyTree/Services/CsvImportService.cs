using System.Globalization;
using System.Text;
using FamilyTree.Data;
using FamilyTree.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Services;

public class CsvImportService : ICsvImportService
{
    private const int MaxRows = 5000;

    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy", "d.M.yyyy", "d/M/yyyy",
    };

    private readonly ApplicationDbContext _context;
    private readonly ILogger<CsvImportService> _logger;

    public CsvImportService(ApplicationDbContext context, ILogger<CsvImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private class ParsedRow
    {
        public int LineNumber { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string Soyad { get; set; } = string.Empty;
        public string? Tc { get; set; }
        public Gender? Cinsiyet { get; set; }
        public DateTime? DogumTarihi { get; set; }
        public DateTime? OlumTarihi { get; set; }
        public string? AnneTc { get; set; }
        public string? BabaTc { get; set; }
        public string? DogumYeri { get; set; }
        public List<int> SulaleIds { get; set; } = new();
        public int? NewPersonId { get; set; }
    }

    public async Task<CsvImportResult> ImportAsync(Stream csvContent)
    {
        var result = new CsvImportResult();

        string text;
        using (var reader = new StreamReader(csvContent, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync();
        }

        List<List<string>> rows;
        try
        {
            rows = ParseCsv(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CSV dosyası ayrıştırılamadı.");
            result.ErrorMessage = "CSV dosyası ayrıştırılamadı. Dosyanın geçerli bir CSV olduğundan emin olun.";
            return result;
        }

        if (rows.Count == 0)
        {
            result.ErrorMessage = "CSV dosyası boş.";
            return result;
        }

        var columnIndex = MapColumns(rows[0]);
        if (!columnIndex.ContainsKey("ad") || !columnIndex.ContainsKey("soyad"))
        {
            result.ErrorMessage = "CSV başlık satırında 'Ad' ve 'Soyad' sütunları bulunmalıdır.";
            return result;
        }

        var dataRows = rows.Skip(1)
            .Select((cells, idx) => (Cells: cells, LineNumber: idx + 2))
            .Where(r => r.Cells.Any(c => !string.IsNullOrWhiteSpace(c)))
            .ToList();

        if (dataRows.Count == 0)
        {
            result.ErrorMessage = "CSV dosyasında veri satırı bulunamadı.";
            return result;
        }

        if (dataRows.Count > MaxRows)
        {
            result.ErrorMessage = $"CSV dosyasında {dataRows.Count} satır var; tek seferde en fazla {MaxRows} satır içe aktarılabilir.";
            return result;
        }

        string? Field(List<string> cells, string column) =>
            columnIndex.TryGetValue(column, out var i) && i < cells.Count ? cells[i].Trim() : null;

        var parsedRows = new List<ParsedRow>();
        var seenTcInFile = new HashSet<string>();

        foreach (var (cells, lineNumber) in dataRows)
        {
            var ad = Field(cells, "ad");
            var soyad = Field(cells, "soyad");

            if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(soyad))
            {
                result.Warnings.Add($"Satır {lineNumber}: Ad veya Soyad boş, atlandı.");
                continue;
            }

            var tc = NormalizeTc(Field(cells, "tc"));
            if (tc == null && !string.IsNullOrWhiteSpace(Field(cells, "tc")))
            {
                result.Warnings.Add($"Satır {lineNumber}: TC Kimlik No formatı geçersiz (11 haneli olmalı), TC olmadan içe aktarılacak.");
            }

            if (tc != null && !seenTcInFile.Add(tc))
            {
                result.Warnings.Add($"Satır {lineNumber}: TC {MaskTc(tc)} dosya içinde birden fazla kez geçiyor, bu satır atlandı.");
                continue;
            }

            var cinsiyetRaw = Field(cells, "cinsiyet");
            var cinsiyet = ParseGender(cinsiyetRaw);
            if (cinsiyet == null && !string.IsNullOrWhiteSpace(cinsiyetRaw))
            {
                result.Warnings.Add($"Satır {lineNumber}: Cinsiyet değeri ('{cinsiyetRaw}') tanınamadı, boş bırakıldı.");
            }

            var dogum = ParseDate(Field(cells, "dogumtarihi"));
            if (dogum == null && !string.IsNullOrWhiteSpace(Field(cells, "dogumtarihi")))
            {
                result.Warnings.Add($"Satır {lineNumber}: Doğum tarihi çözümlenemedi, boş bırakıldı.");
            }

            var olum = ParseDate(Field(cells, "olumtarihi"));
            if (olum == null && !string.IsNullOrWhiteSpace(Field(cells, "olumtarihi")))
            {
                result.Warnings.Add($"Satır {lineNumber}: Ölüm tarihi çözümlenemedi, boş bırakıldı.");
            }

            var today = DateTime.UtcNow.Date;
            if (dogum.HasValue && dogum.Value.Date > today)
            {
                result.Warnings.Add($"Satır {lineNumber}: Doğum tarihi gelecekte olamaz, boş bırakıldı.");
                dogum = null;
            }

            if (olum.HasValue && olum.Value.Date > today)
            {
                result.Warnings.Add($"Satır {lineNumber}: Ölüm tarihi gelecekte olamaz, boş bırakıldı.");
                olum = null;
            }

            if (dogum.HasValue && olum.HasValue && dogum.Value > olum.Value)
            {
                result.Warnings.Add($"Satır {lineNumber}: Ölüm tarihi doğum tarihinden önce olamaz, ölüm tarihi boş bırakıldı.");
                olum = null;
            }

            var anneTc = NormalizeTc(Field(cells, "annetc"));
            var babaTc = NormalizeTc(Field(cells, "babatc"));

            if (anneTc != null && tc != null && anneTc == tc)
            {
                result.Warnings.Add($"Satır {lineNumber}: Kişi kendi annesi olamaz, Anne TC yok sayıldı.");
                anneTc = null;
            }

            if (babaTc != null && tc != null && babaTc == tc)
            {
                result.Warnings.Add($"Satır {lineNumber}: Kişi kendi babası olamaz, Baba TC yok sayıldı.");
                babaTc = null;
            }

            var dogumYeriRaw = Field(cells, "dogumyeri");
            var dogumYeri = string.IsNullOrWhiteSpace(dogumYeriRaw) ? null : dogumYeriRaw.Trim();

            // Bir kişi birden fazla sülalenin üyesi olabildiğinden (many-to-many, bkz. PersonSulale)
            // bu sütun ";" ile ayrılmış birden fazla SulaleId kabul eder (ör. "2;5").
            var sulaleIdRaw = Field(cells, "sulaleid");
            var sulaleIds = new List<int>();
            if (!string.IsNullOrWhiteSpace(sulaleIdRaw))
            {
                foreach (var part in sulaleIdRaw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSulaleId))
                    {
                        sulaleIds.Add(parsedSulaleId);
                    }
                    else
                    {
                        result.Warnings.Add($"Satır {lineNumber}: SulaleId değeri ('{part.Trim()}') sayısal değil, atlandı.");
                    }
                }
            }

            parsedRows.Add(new ParsedRow
            {
                LineNumber = lineNumber,
                Ad = ad.Trim(),
                Soyad = soyad.Trim(),
                Tc = tc,
                Cinsiyet = cinsiyet,
                DogumTarihi = dogum,
                OlumTarihi = olum,
                AnneTc = anneTc,
                BabaTc = babaTc,
                DogumYeri = dogumYeri,
                SulaleIds = sulaleIds.Distinct().ToList(),
            });
        }

        var referencedTcs = parsedRows
            .SelectMany(r => new[] { r.Tc, r.AnneTc, r.BabaTc })
            .Where(tc => tc != null)
            .Select(tc => tc!)
            .Distinct()
            .ToList();

        // IgnoreQueryFilters: UNIQUE index soft-delete durumundan habersizdir, bu yüzden yumuşak
        // silinmiş bir kişinin TC'si de burada dikkate alınmalı — aksi halde bu sorgu böyle bir
        // TC'yi "kayıtlı değil" sanıp yeni bir Person eklemeye çalışır ve SaveChangesAsync
        // sırasında MySqlException fırlatıp tüm CSV içe aktarma transaction'ını bozar (bkz.
        // PersonService.CreateAsync/UpdateAsync'teki aynı isimli not, gerçek kullanıcı verisiyle
        // bulunmuş bir hata). Tracking açık (AsNoTracking değil): eşleşen TC'ler aşağıda
        // doğrudan güncellenip SaveChangesAsync ile kalıcı hale getirilecek.
        var existingPersonsByTc = referencedTcs.Count == 0
            ? new Dictionary<string, Person>()
            : await _context.Persons.IgnoreQueryFilters()
                .Where(p => p.TcKimlikNo != null && referencedTcs.Contains(p.TcKimlikNo))
                .ToDictionaryAsync(p => p.TcKimlikNo!, p => p);

        var existingByTc = existingPersonsByTc.ToDictionary(kv => kv.Key, kv => kv.Value.Id);

        var referencedSulaleIds = parsedRows
            .SelectMany(r => r.SulaleIds)
            .Distinct()
            .ToList();

        var existingSulaleIds = referencedSulaleIds.Count == 0
            ? new HashSet<int>()
            : (await _context.Sulaleler
                .Where(s => referencedSulaleIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync())
                .ToHashSet();

        foreach (var row in parsedRows)
        {
            var invalidIds = row.SulaleIds.Where(id => !existingSulaleIds.Contains(id)).ToList();
            foreach (var invalidId in invalidIds)
            {
                result.Warnings.Add($"Satır {row.LineNumber}: SulaleId {invalidId} bulunamadı, bu sülale ataması yapılmadı.");
            }

            if (invalidIds.Count > 0)
            {
                row.SulaleIds = row.SulaleIds.Except(invalidIds).ToList();
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var tcToNewPersonId = new Dictionary<string, int>();

            foreach (var row in parsedRows)
            {
                if (row.Tc != null && existingPersonsByTc.TryGetValue(row.Tc, out var existingPerson))
                {
                    if (existingPerson.IsDeleted)
                    {
                        result.Warnings.Add($"Satır {row.LineNumber}: TC {MaskTc(row.Tc)} silinmiş bir kişiye ait, güncellenmedi (önce Silinmiş Kişiler sayfasından geri getirin).");
                        continue;
                    }

                    // Upsert: satırda dolu olan alanlar mevcut kişinin üzerine yazılır, boş
                    // bırakılan alanlar (ör. bir yenileme CSV'sinde yalnızca ölüm tarihi
                    // güncelleniyorsa) mevcut değeri korur — CSV'nin diğer alanlarındaki
                    // "yanlış kesinlik oluşturma yerine mevcut/boş bırak" prensibiyle tutarlı.
                    existingPerson.Ad = row.Ad;
                    existingPerson.Soyad = row.Soyad;
                    if (row.Cinsiyet.HasValue)
                    {
                        existingPerson.Cinsiyet = row.Cinsiyet;
                    }

                    if (row.DogumTarihi.HasValue)
                    {
                        existingPerson.DogumTarihi = row.DogumTarihi;
                    }

                    if (row.OlumTarihi.HasValue)
                    {
                        existingPerson.OlumTarihi = row.OlumTarihi;
                    }

                    if (row.DogumYeri != null)
                    {
                        existingPerson.DogumYeri = row.DogumYeri;
                    }

                    existingPerson.UpdatedAt = DateTime.UtcNow;

                    if (row.SulaleIds.Count > 0)
                    {
                        var alreadyMember = await _context.PersonSulaleler.IgnoreQueryFilters()
                            .Where(ps => ps.PersonId == existingPerson.Id && row.SulaleIds.Contains(ps.SulaleId))
                            .Select(ps => ps.SulaleId)
                            .ToListAsync();

                        foreach (var sId in row.SulaleIds.Except(alreadyMember))
                        {
                            _context.PersonSulaleler.Add(new PersonSulale { PersonId = existingPerson.Id, SulaleId = sId });
                        }
                    }

                    await _context.SaveChangesAsync();

                    result.PersonsUpdated++;
                    row.NewPersonId = existingPerson.Id;
                    continue;
                }

                var person = new Person
                {
                    Ad = row.Ad,
                    Soyad = row.Soyad,
                    TcKimlikNo = row.Tc,
                    Cinsiyet = row.Cinsiyet,
                    DogumTarihi = row.DogumTarihi,
                    OlumTarihi = row.OlumTarihi,
                    DogumYeri = row.DogumYeri,
                    CreatedAt = DateTime.UtcNow,
                };

                _context.Persons.Add(person);
                await _context.SaveChangesAsync();

                foreach (var sId in row.SulaleIds)
                {
                    _context.PersonSulaleler.Add(new PersonSulale { PersonId = person.Id, SulaleId = sId });
                }

                if (row.SulaleIds.Count > 0)
                {
                    await _context.SaveChangesAsync();
                }

                result.PersonsCreated++;
                row.NewPersonId = person.Id;

                if (row.Tc != null)
                {
                    tcToNewPersonId[row.Tc] = person.Id;
                }
            }

            int? ResolveTc(string? tc) =>
                tc == null ? null
                : tcToNewPersonId.TryGetValue(tc, out var newId) ? newId
                : existingByTc.TryGetValue(tc, out var existingId) ? existingId
                : (int?)null;

            foreach (var row in parsedRows)
            {
                if (row.NewPersonId == null)
                {
                    continue;
                }

                var anneId = ResolveTc(row.AnneTc);
                var babaId = ResolveTc(row.BabaTc);

                if (row.AnneTc != null && anneId == null)
                {
                    result.Warnings.Add($"Satır {row.LineNumber}: Anne TC {MaskTc(row.AnneTc)} bulunamadı, anne ilişkisi kurulmadı.");
                }

                if (row.BabaTc != null && babaId == null)
                {
                    result.Warnings.Add($"Satır {row.LineNumber}: Baba TC {MaskTc(row.BabaTc)} bulunamadı, baba ilişkisi kurulmadı.");
                }

                if (anneId == null && babaId == null)
                {
                    continue;
                }

                var person = await _context.Persons.FindAsync(row.NewPersonId.Value);
                if (person == null)
                {
                    continue;
                }

                if (anneId.HasValue)
                {
                    person.AnneId = anneId;
                }

                if (babaId.HasValue)
                {
                    person.BabaId = babaId;
                }

                result.RelationshipsLinked++;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "CSV içe aktarma sırasında hata oluştu, tüm değişiklikler geri alındı.");
            result.Success = false;
            result.ErrorMessage = "İçe aktarma sırasında bir hata oluştu; hiçbir kayıt eklenmedi. Sunucu loglarını kontrol edin.";
            return result;
        }
    }

    private static Dictionary<string, int> MapColumns(List<string> header)
    {
        var map = new Dictionary<string, int>();

        for (var i = 0; i < header.Count; i++)
        {
            var key = NormalizeHeader(header[i]);
            var canonical = key switch
            {
                "tc" or "tckimlikno" or "tcno" or "kimlikno" => "tc",
                "ad" or "isim" => "ad",
                "soyad" => "soyad",
                "cinsiyet" => "cinsiyet",
                "dogumtarihi" or "dogumtarih" => "dogumtarihi",
                "olumtarihi" or "olumtarih" => "olumtarihi",
                "annetc" or "annetckimlikno" or "anneno" => "annetc",
                "babatc" or "babatckimlikno" or "babano" => "babatc",
                "dogumyeri" => "dogumyeri",
                "sulaleid" or "sulale" => "sulaleid",
                _ => null,
            };

            if (canonical != null && !map.ContainsKey(canonical))
            {
                map[canonical] = i;
            }
        }

        return map;
    }

    private static string NormalizeHeader(string raw)
    {
        var s = raw.Trim().ToLowerInvariant();
        s = s.Replace("ı", "i").Replace("ş", "s").Replace("ğ", "g")
             .Replace("ç", "c").Replace("ö", "o").Replace("ü", "u");
        return new string(s.Where(char.IsLetterOrDigit).ToArray());
    }

    private static string? NormalizeTc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digitsOnly = new string(raw.Where(char.IsDigit).ToArray());
        return digitsOnly.Length == 11 ? digitsOnly : null;
    }

    private static Gender? ParseGender(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = raw.Trim().ToLowerInvariant();
        s = s.Replace("ı", "i").Replace("ğ", "g");

        return s switch
        {
            "erkek" or "e" or "m" or "male" => Gender.Erkek,
            "kadin" or "kadın" or "k" or "f" or "female" => Gender.Kadin,
            _ => null,
        };
    }

    private static DateTime? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();

        if (DateTime.TryParseExact(trimmed, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact.Date;
        }

        if (DateTime.TryParse(trimmed, new CultureInfo("tr-TR"), DateTimeStyles.None, out var trParsed))
        {
            return trParsed.Date;
        }

        return null;
    }

    private static string MaskTc(string tc)
    {
        if (tc.Length != 11)
        {
            return "*****";
        }

        return $"{tc[..3]}*****{tc[^3..]}";
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        // BOM'u ve olası satır sonu farklılıklarını (\r\n / \n) sorunsuz işlemek için karakter bazlı ilerliyoruz.
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        while (i < text.Length)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    i++;
                    break;
                case ',':
                    currentRow.Add(field.ToString());
                    field.Clear();
                    i++;
                    break;
                case '\r':
                    i++;
                    break;
                case '\n':
                    currentRow.Add(field.ToString());
                    field.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                    i++;
                    break;
                default:
                    field.Append(c);
                    i++;
                    break;
            }
        }

        if (field.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(field.ToString());
            rows.Add(currentRow);
        }

        return rows.Where(r => r.Count > 0).ToList();
    }
}
