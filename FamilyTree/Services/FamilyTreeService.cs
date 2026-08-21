using FamilyTree.Data;
using FamilyTree.Models;
using FamilyTree.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Services;

public class FamilyTreeService : IFamilyTreeService
{
    private readonly ApplicationDbContext _context;

    public FamilyTreeService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FamilyTreeGraphDto?> GetBaseTreeAsync(int personId)
    {
        var person = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Photos)
            .Include(p => p.Anne).ThenInclude(a => a!.Photos)
            .Include(p => p.Baba).ThenInclude(b => b!.Photos)
            .Include(p => p.SpouseRelationshipsAsPerson1).ThenInclude(sr => sr.Person2).ThenInclude(p2 => p2.Photos)
            .Include(p => p.SpouseRelationshipsAsPerson2).ThenInclude(sr => sr.Person1).ThenInclude(p1 => p1.Photos)
            .FirstOrDefaultAsync(p => p.Id == personId);

        if (person == null)
        {
            return null;
        }

        var siblings = new List<Person>();
        if (person.AnneId.HasValue || person.BabaId.HasValue)
        {
            siblings = await _context.Persons
                .AsNoTracking()
                .Include(p => p.Photos)
                .Where(p => p.Id != personId &&
                    ((person.AnneId.HasValue && p.AnneId == person.AnneId) ||
                     (person.BabaId.HasValue && p.BabaId == person.BabaId)))
                .ToListAsync();
        }

        var children = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Photos)
            .Where(p => p.AnneId == personId || p.BabaId == personId)
            .ToListAsync();

        var spouses = person.SpouseRelationshipsAsPerson1.Select(sr => sr.Person2)
            .Concat(person.SpouseRelationshipsAsPerson2.Select(sr => sr.Person1))
            .ToList();

        var graph = new FamilyTreeGraphDto();
        var nodeIds = new HashSet<int>();

        void AddNode(Person p, int generation, string role, bool isCenter = false)
        {
            if (!nodeIds.Add(p.Id))
            {
                return;
            }

            graph.Nodes.Add(ToNode(p, generation, role, isCenter));
        }

        AddNode(person, 0, "Merkez", isCenter: true);

        if (person.Anne != null)
        {
            AddNode(person.Anne, -1, "Anne");
        }

        if (person.Baba != null)
        {
            AddNode(person.Baba, -1, "Baba");
        }

        foreach (var spouse in spouses)
        {
            AddNode(spouse, 0, "Eş");
        }

        foreach (var sibling in siblings)
        {
            AddNode(sibling, 0, "Kardeş");
        }

        foreach (var child in children)
        {
            AddNode(child, 1, "Çocuk");
        }

        if (person.Anne != null)
        {
            graph.Links.Add(new FamilyTreeLinkDto { Source = person.Anne.Id, Target = person.Id, Relationship = "parent" });

            foreach (var sibling in siblings.Where(s => s.AnneId == person.AnneId))
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = person.Anne.Id, Target = sibling.Id, Relationship = "parent" });
            }
        }

        if (person.Baba != null)
        {
            graph.Links.Add(new FamilyTreeLinkDto { Source = person.Baba.Id, Target = person.Id, Relationship = "parent" });

            foreach (var sibling in siblings.Where(s => s.BabaId == person.BabaId))
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = person.Baba.Id, Target = sibling.Id, Relationship = "parent" });
            }
        }

        foreach (var spouse in spouses)
        {
            graph.Links.Add(new FamilyTreeLinkDto { Source = person.Id, Target = spouse.Id, Relationship = "spouse" });
        }

        foreach (var child in children)
        {
            if (child.AnneId.HasValue && nodeIds.Contains(child.AnneId.Value))
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = child.AnneId.Value, Target = child.Id, Relationship = "parent" });
            }

            if (child.BabaId.HasValue && nodeIds.Contains(child.BabaId.Value))
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = child.BabaId.Value, Target = child.Id, Relationship = "parent" });
            }
        }

        return graph;
    }

    public async Task<FamilyTreeGraphDto> GetGrandparentsAsync(int personId)
    {
        var graph = new FamilyTreeGraphDto();

        var person = await _context.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personId);
        if (person == null)
        {
            return graph;
        }

        async Task AddSideAsync(int? parentId)
        {
            if (!parentId.HasValue)
            {
                return;
            }

            var parent = await _context.Persons
                .AsNoTracking()
                .Include(p => p.Photos)
                .Include(p => p.Anne).ThenInclude(a => a!.Photos)
                .Include(p => p.Baba).ThenInclude(b => b!.Photos)
                .FirstOrDefaultAsync(p => p.Id == parentId.Value);

            if (parent == null)
            {
                return;
            }

            if (parent.Anne != null)
            {
                graph.Nodes.Add(ToNode(parent.Anne, -2, "Nine"));
                graph.Links.Add(new FamilyTreeLinkDto { Source = parent.Anne.Id, Target = parent.Id, Relationship = "parent" });
            }

            if (parent.Baba != null)
            {
                graph.Nodes.Add(ToNode(parent.Baba, -2, "Dede"));
                graph.Links.Add(new FamilyTreeLinkDto { Source = parent.Baba.Id, Target = parent.Id, Relationship = "parent" });
            }

            if (parent.Anne != null && parent.Baba != null)
            {
                var isSpouse = await _context.SpouseRelationships.AsNoTracking().AnyAsync(sr =>
                    (sr.Person1Id == parent.Anne.Id && sr.Person2Id == parent.Baba.Id) ||
                    (sr.Person1Id == parent.Baba.Id && sr.Person2Id == parent.Anne.Id));

                if (isSpouse)
                {
                    graph.Links.Add(new FamilyTreeLinkDto { Source = parent.Anne.Id, Target = parent.Baba.Id, Relationship = "spouse" });
                }
            }
        }

        await AddSideAsync(person.AnneId);
        await AddSideAsync(person.BabaId);

        DeduplicateNodes(graph);
        return graph;
    }

    public async Task<FamilyTreeGraphDto> GetGrandchildrenAsync(int personId)
    {
        var graph = new FamilyTreeGraphDto();

        var childIds = await _context.Persons
            .AsNoTracking()
            .Where(p => p.AnneId == personId || p.BabaId == personId)
            .Select(p => p.Id)
            .ToListAsync();

        if (childIds.Count == 0)
        {
            return graph;
        }

        var grandchildren = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Photos)
            .Where(p => (p.AnneId.HasValue && childIds.Contains(p.AnneId.Value)) ||
                        (p.BabaId.HasValue && childIds.Contains(p.BabaId.Value)))
            .ToListAsync();

        foreach (var grandchild in grandchildren)
        {
            graph.Nodes.Add(ToNode(grandchild, 2, "Torun"));

            if (grandchild.AnneId.HasValue && childIds.Contains(grandchild.AnneId.Value))
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = grandchild.AnneId.Value, Target = grandchild.Id, Relationship = "parent" });
            }

            if (grandchild.BabaId.HasValue && childIds.Contains(grandchild.BabaId.Value))
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = grandchild.BabaId.Value, Target = grandchild.Id, Relationship = "parent" });
            }
        }

        DeduplicateNodes(graph);
        return graph;
    }

    public async Task<FamilyTreeGraphDto> GetNephewsAsync(int personId)
    {
        var graph = new FamilyTreeGraphDto();

        var person = await _context.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personId);
        if (person == null || (!person.AnneId.HasValue && !person.BabaId.HasValue))
        {
            return graph;
        }

        var siblingIds = await _context.Persons
            .AsNoTracking()
            .Where(p => p.Id != personId &&
                ((person.AnneId.HasValue && p.AnneId == person.AnneId) ||
                 (person.BabaId.HasValue && p.BabaId == person.BabaId)))
            .Select(p => p.Id)
            .ToListAsync();

        if (siblingIds.Count == 0)
        {
            return graph;
        }

        var nephews = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Photos)
            .Where(p => (p.AnneId.HasValue && siblingIds.Contains(p.AnneId.Value)) ||
                        (p.BabaId.HasValue && siblingIds.Contains(p.BabaId.Value)))
            .ToListAsync();

        foreach (var nephew in nephews)
        {
            graph.Nodes.Add(ToNode(nephew, 1, "Yeğen"));

            if (nephew.AnneId.HasValue && siblingIds.Contains(nephew.AnneId.Value))
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = nephew.AnneId.Value, Target = nephew.Id, Relationship = "parent" });
            }

            if (nephew.BabaId.HasValue && siblingIds.Contains(nephew.BabaId.Value))
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = nephew.BabaId.Value, Target = nephew.Id, Relationship = "parent" });
            }
        }

        DeduplicateNodes(graph);
        return graph;
    }

    public async Task<FamilyTreeGraphDto> GetAuntsUnclesAsync(int personId)
    {
        var graph = new FamilyTreeGraphDto();

        var person = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Anne)
            .Include(p => p.Baba)
            .FirstOrDefaultAsync(p => p.Id == personId);

        if (person == null)
        {
            return graph;
        }

        async Task AddSideAsync(Person? parent, string erkekRol, string kadinRol, string belirsizRol)
        {
            if (parent == null || (!parent.AnneId.HasValue && !parent.BabaId.HasValue))
            {
                return;
            }

            var siblings = await _context.Persons
                .AsNoTracking()
                .Include(p => p.Photos)
                .Where(p => p.Id != parent.Id &&
                    ((parent.AnneId.HasValue && p.AnneId == parent.AnneId) ||
                     (parent.BabaId.HasValue && p.BabaId == parent.BabaId)))
                .ToListAsync();

            foreach (var sibling in siblings)
            {
                var role = sibling.Cinsiyet switch
                {
                    Gender.Erkek => erkekRol,
                    Gender.Kadin => kadinRol,
                    _ => belirsizRol,
                };

                graph.Nodes.Add(ToNode(sibling, -1, role));

                if (parent.AnneId.HasValue && sibling.AnneId == parent.AnneId)
                {
                    graph.Links.Add(new FamilyTreeLinkDto { Source = parent.AnneId.Value, Target = sibling.Id, Relationship = "parent" });
                }

                if (parent.BabaId.HasValue && sibling.BabaId == parent.BabaId)
                {
                    graph.Links.Add(new FamilyTreeLinkDto { Source = parent.BabaId.Value, Target = sibling.Id, Relationship = "parent" });
                }
            }
        }

        await AddSideAsync(person.Anne, "Dayı", "Teyze", "Anne Tarafından Kardeş");
        await AddSideAsync(person.Baba, "Amca", "Hala", "Baba Tarafından Kardeş");

        DeduplicateNodes(graph);
        return graph;
    }

    public async Task<FamilyTreeGraphDto> GetCousinsAsync(int personId)
    {
        var graph = new FamilyTreeGraphDto();

        var person = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Anne)
            .Include(p => p.Baba)
            .FirstOrDefaultAsync(p => p.Id == personId);

        if (person == null)
        {
            return graph;
        }

        async Task<List<Person>> GetParentSiblingsAsync(Person? parent)
        {
            if (parent == null || (!parent.AnneId.HasValue && !parent.BabaId.HasValue))
            {
                return new List<Person>();
            }

            return await _context.Persons
                .AsNoTracking()
                .Where(p => p.Id != parent.Id &&
                    ((parent.AnneId.HasValue && p.AnneId == parent.AnneId) ||
                     (parent.BabaId.HasValue && p.BabaId == parent.BabaId)))
                .ToListAsync();
        }

        var ebeveynKardesleri = (await GetParentSiblingsAsync(person.Anne))
            .Concat(await GetParentSiblingsAsync(person.Baba))
            .ToList();

        var ebeveynKardesIds = ebeveynKardesleri.Select(p => p.Id).Distinct().ToList();
        if (ebeveynKardesIds.Count == 0)
        {
            return graph;
        }

        var cousins = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Photos)
            .Where(p => (p.AnneId.HasValue && ebeveynKardesIds.Contains(p.AnneId.Value)) ||
                        (p.BabaId.HasValue && ebeveynKardesIds.Contains(p.BabaId.Value)))
            .ToListAsync();

        foreach (var cousin in cousins)
        {
            graph.Nodes.Add(ToNode(cousin, 0, "Kuzen"));

            if (cousin.AnneId.HasValue && ebeveynKardesIds.Contains(cousin.AnneId.Value))
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = cousin.AnneId.Value, Target = cousin.Id, Relationship = "parent" });
            }

            if (cousin.BabaId.HasValue && ebeveynKardesIds.Contains(cousin.BabaId.Value))
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = cousin.BabaId.Value, Target = cousin.Id, Relationship = "parent" });
            }
        }

        DeduplicateNodes(graph);
        return graph;
    }

    public async Task<FamilyTreeGraphDto?> GetBySulaleAsync(int sulaleId)
    {
        var sulaleExists = await _context.Sulaleler.AsNoTracking().AnyAsync(s => s.Id == sulaleId);
        if (!sulaleExists)
        {
            return null;
        }

        var members = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Photos)
            .Where(p => p.SulaleId == sulaleId)
            .ToListAsync();

        var graph = new FamilyTreeGraphDto();
        if (members.Count == 0)
        {
            return graph;
        }

        var memberIds = members.Select(m => m.Id).ToHashSet();

        var parentsOf = members.ToDictionary(
            m => m.Id,
            m =>
            {
                var parents = new List<int>();
                if (m.AnneId.HasValue && memberIds.Contains(m.AnneId.Value))
                {
                    parents.Add(m.AnneId.Value);
                }

                if (m.BabaId.HasValue && memberIds.Contains(m.BabaId.Value))
                {
                    parents.Add(m.BabaId.Value);
                }

                return parents;
            });

        var childrenOf = memberIds.ToDictionary(id => id, _ => new List<int>());
        foreach (var (childId, parents) in parentsOf)
        {
            foreach (var parentId in parents)
            {
                childrenOf[parentId].Add(childId);
            }
        }

        // Kan bağı derinliğine göre nesil hesabı (Kahn'ın topolojik sıralaması):
        // sette ebeveyni olmayan kişiler 0. nesil, her çocuk ebeveynlerinin en derinin bir fazlası.
        var generation = new Dictionary<int, int>();
        var inDegree = memberIds.ToDictionary(id => id, id => parentsOf[id].Count);
        var ready = new Queue<int>(memberIds.Where(id => inDegree[id] == 0));
        foreach (var id in ready)
        {
            generation[id] = 0;
        }

        var pending = new Queue<int>(ready);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var childId in childrenOf[current])
            {
                var candidate = generation[current] + 1;
                if (!generation.TryGetValue(childId, out var existing) || existing < candidate)
                {
                    generation[childId] = candidate;
                }

                inDegree[childId]--;
                if (inDegree[childId] == 0)
                {
                    pending.Enqueue(childId);
                }
            }
        }

        // Beklenmedik bir döngü olsa bile (normalde döngüler oluşturma anında engellenir) her kişiye bir nesil ata.
        foreach (var id in memberIds)
        {
            if (!generation.ContainsKey(id))
            {
                generation[id] = 0;
            }
        }

        var spouseRelationships = await _context.SpouseRelationships
            .AsNoTracking()
            .Where(sr => memberIds.Contains(sr.Person1Id) && memberIds.Contains(sr.Person2Id))
            .ToListAsync();

        // Evli çiftler aynı satırda görünsün diye eş jenerasyonlarını hizala (sabit noktaya ulaşana kadar).
        var aligning = true;
        var safety = memberIds.Count + spouseRelationships.Count + 5;
        while (aligning && safety-- > 0)
        {
            aligning = false;
            foreach (var sr in spouseRelationships)
            {
                var g1 = generation[sr.Person1Id];
                var g2 = generation[sr.Person2Id];
                if (g1 != g2)
                {
                    var max = Math.Max(g1, g2);
                    generation[sr.Person1Id] = max;
                    generation[sr.Person2Id] = max;
                    aligning = true;
                }
            }
        }

        foreach (var member in members)
        {
            graph.Nodes.Add(ToNode(member, generation[member.Id], role: string.Empty));
        }

        foreach (var (childId, parents) in parentsOf)
        {
            foreach (var parentId in parents)
            {
                graph.Links.Add(new FamilyTreeLinkDto { Source = parentId, Target = childId, Relationship = "parent" });
            }
        }

        foreach (var sr in spouseRelationships)
        {
            graph.Links.Add(new FamilyTreeLinkDto { Source = sr.Person1Id, Target = sr.Person2Id, Relationship = "spouse" });
        }

        return graph;
    }

    private static void DeduplicateNodes(FamilyTreeGraphDto graph)
    {
        var seen = new HashSet<int>();
        graph.Nodes = graph.Nodes.Where(n => seen.Add(n.Id)).ToList();
    }

    private static FamilyTreeNodeDto ToNode(Person p, int generation, string role, bool isCenter = false)
    {
        var primaryPhoto = p.Photos?.OrderByDescending(ph => ph.IsPrimary).ThenBy(ph => ph.CreatedAt).FirstOrDefault();

        return new FamilyTreeNodeDto
        {
            Id = p.Id,
            Name = $"{p.Ad} {p.Soyad}",
            Ad = p.Ad,
            Soyad = p.Soyad,
            BirthYear = p.DogumTarihi?.Year,
            DeathYear = p.OlumTarihi?.Year,
            Alive = p.OlumTarihi == null,
            PhotoPath = primaryPhoto?.FilePath,
            Generation = generation,
            Role = role,
            IsCenter = isCenter,
        };
    }
}
