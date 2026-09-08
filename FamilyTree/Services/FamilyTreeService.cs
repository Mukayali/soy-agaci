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

    public async Task<FamilyTreeGraphDto> GetAncestorsAsync(int personId, int maxDepth)
    {
        var graph = new FamilyTreeGraphDto();
        maxDepth = Math.Clamp(maxDepth, 1, 20);

        var personExists = await _context.Persons.AsNoTracking().AnyAsync(p => p.Id == personId);
        if (!personExists)
        {
            return graph;
        }

        var addedNodeIds = new HashSet<int>();
        var frontier = new List<int> { personId };

        for (var depth = 1; depth <= maxDepth && frontier.Count > 0; depth++)
        {
            var frontierPeople = await _context.Persons.AsNoTracking()
                .Where(p => frontier.Contains(p.Id))
                .Select(p => new { p.Id, p.AnneId, p.BabaId })
                .ToListAsync();

            var parentIds = frontierPeople
                .SelectMany(p => new[] { p.AnneId, p.BabaId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (parentIds.Count == 0)
            {
                break;
            }

            var parents = await _context.Persons.AsNoTracking()
                .Include(p => p.Photos)
                .Where(p => parentIds.Contains(p.Id))
                .ToListAsync();
            var parentById = parents.ToDictionary(p => p.Id);

            var nextFrontier = new List<int>();

            foreach (var child in frontierPeople)
            {
                if (child.AnneId is int anneId && parentById.TryGetValue(anneId, out var anne))
                {
                    if (addedNodeIds.Add(anne.Id))
                    {
                        graph.Nodes.Add(ToNode(anne, -depth, AncestorRole(depth, isAnne: true)));
                        nextFrontier.Add(anne.Id);
                    }

                    graph.Links.Add(new FamilyTreeLinkDto { Source = anne.Id, Target = child.Id, Relationship = "parent" });
                }

                if (child.BabaId is int babaId && parentById.TryGetValue(babaId, out var baba))
                {
                    if (addedNodeIds.Add(baba.Id))
                    {
                        graph.Nodes.Add(ToNode(baba, -depth, AncestorRole(depth, isAnne: false)));
                        nextFrontier.Add(baba.Id);
                    }

                    graph.Links.Add(new FamilyTreeLinkDto { Source = baba.Id, Target = child.Id, Relationship = "parent" });
                }
            }

            frontier = nextFrontier;
        }

        var spouseRelationships = await _context.SpouseRelationships.AsNoTracking()
            .Where(sr => addedNodeIds.Contains(sr.Person1Id) && addedNodeIds.Contains(sr.Person2Id))
            .ToListAsync();

        foreach (var sr in spouseRelationships)
        {
            graph.Links.Add(new FamilyTreeLinkDto { Source = sr.Person1Id, Target = sr.Person2Id, Relationship = "spouse" });
        }

        DeduplicateNodes(graph);
        return graph;
    }

    private static string AncestorRole(int depth, bool isAnne) => depth switch
    {
        1 => isAnne ? "Anne" : "Baba",
        2 => isAnne ? "Nine" : "Dede",
        3 => isAnne ? "Büyük Nine" : "Büyük Dede",
        _ => $"{depth}. kuşak ata",
    };

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
            .Where(p => p.PersonSulaleler.Any(ps => ps.SulaleId == sulaleId))
            .ToListAsync();

        return await BuildConnectedGraphAsync(members);
    }

    /// <summary>
    /// Verilen kişi kümesini, yalnızca küme içindeki anne/baba ve eş bağlarıyla birlikte tek bir
    /// grafiğe dönüştürür. Nesil numaraları kan bağı derinliğine göre (Kahn'ın topolojik sıralaması)
    /// hesaplanır ve evli çiftler aynı satıra hizalanır. İsteğe bağlı <paramref name="decorate"/> ile
    /// her düğüme rol etiketi / merkez bayrağı verilebilir.
    /// </summary>
    private async Task<FamilyTreeGraphDto> BuildConnectedGraphAsync(
        List<Person> members,
        Func<Person, (string Role, bool IsCenter)>? decorate = null)
    {
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
            var (role, isCenter) = decorate?.Invoke(member) ?? (string.Empty, false);
            graph.Nodes.Add(ToNode(member, generation[member.Id], role, isCenter));
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

    public async Task<RelationshipResultDto> FindRelationshipAsync(int person1Id, int person2Id)
    {
        var p1 = await _context.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == person1Id);
        var p2 = await _context.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == person2Id);

        var result = new RelationshipResultDto
        {
            Person1Id = person1Id,
            Person2Id = person2Id,
            Person1Name = p1 != null ? $"{p1.Ad} {p1.Soyad}" : string.Empty,
            Person2Name = p2 != null ? $"{p2.Ad} {p2.Soyad}" : string.Empty,
        };

        if (p1 == null || p2 == null)
        {
            result.Summary = "Seçilen kişilerden en az biri bulunamadı.";
            return result;
        }

        if (person1Id == person2Id)
        {
            result.Related = true;
            result.Summary = "Aynı kişi seçildi.";
            result.Paths.Add(new RelationshipPathDto
            {
                Summary = "Aynı kişi seçildi.",
                Kind = "Kan bağı",
                Length = 0,
                PersonIds = new List<int> { person1Id },
            });
            result.Graph = await BuildConnectedGraphAsync(
                await LoadPersonsWithPhotosAsync(new[] { person1Id }),
                decorate: _ => ("Seçilen kişi", true));
            return result;
        }

        // Tüm kişi grafiğini (yalnızca Id + ebeveyn Id'leri) ve eş çiftlerini belleğe al.
        var all = await _context.Persons.AsNoTracking()
            .Select(p => new { p.Id, p.AnneId, p.BabaId })
            .ToListAsync();
        var spousePairs = await _context.SpouseRelationships.AsNoTracking()
            .Select(sr => new { sr.Person1Id, sr.Person2Id })
            .ToListAsync();

        var adj = new Dictionary<int, List<(int To, string Kind)>>();
        void AddEdge(int from, int to, string kind)
        {
            if (!adj.TryGetValue(from, out var list))
            {
                list = new List<(int, string)>();
                adj[from] = list;
            }

            list.Add((to, kind));
        }

        foreach (var p in all)
        {
            if (p.AnneId is int anneId)
            {
                AddEdge(p.Id, anneId, "up");
                AddEdge(anneId, p.Id, "down");
            }

            if (p.BabaId is int babaId)
            {
                AddEdge(p.Id, babaId, "up");
                AddEdge(babaId, p.Id, "down");
            }
        }

        foreach (var sp in spousePairs)
        {
            AddEdge(sp.Person1Id, sp.Person2Id, "spouse");
            AddEdge(sp.Person2Id, sp.Person1Id, "spouse");
        }

        // 1. kişiden 2. kişiye genişlik öncelikli arama. bloodOnly=true eş kenarlarını
        // atlar; blocked belirli (yönsüz) kenarları devre dışı bırakır (alternatif yol için).
        (List<int> Ids, List<string> Kinds)? Bfs(bool bloodOnly, HashSet<(int, int)>? blocked)
        {
            var prev = new Dictionary<int, (int From, string Kind)>();
            var visited = new HashSet<int> { person1Id };
            var queue = new Queue<int>();
            queue.Enqueue(person1Id);
            var found = false;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == person2Id)
                {
                    found = true;
                    break;
                }

                if (!adj.TryGetValue(current, out var neighbors))
                {
                    continue;
                }

                foreach (var (to, kind) in neighbors)
                {
                    if (bloodOnly && kind == "spouse")
                    {
                        continue;
                    }

                    if (blocked != null && blocked.Contains((current, to)))
                    {
                        continue;
                    }

                    if (visited.Add(to))
                    {
                        prev[to] = (current, kind);
                        queue.Enqueue(to);
                    }
                }
            }

            if (!found)
            {
                return null;
            }

            var ids = new List<int>();
            var kinds = new List<string>();
            var node = person2Id;
            while (node != person1Id)
            {
                ids.Add(node);
                var (from, kind) = prev[node];
                kinds.Add(kind);
                node = from;
            }

            ids.Add(person1Id);
            ids.Reverse();
            kinds.Reverse();
            return (ids, kinds);
        }

        var primary = Bfs(false, null);
        if (primary == null)
        {
            result.Summary = "Akrabalık bağı bulunamadı. Kayıtlı anne/baba ve eş ilişkileri üzerinden bu iki kişi birbirine bağlanamıyor.";
            return result;
        }

        var candidates = new List<(List<int> Ids, List<string> Kinds)> { primary.Value };

        // Yalnızca kan bağıyla giden yol (varsa) her zaman bir aday.
        var bloodOnlyPath = Bfs(true, null);
        if (bloodOnlyPath != null)
        {
            candidates.Add(bloodOnlyPath.Value);
        }

        // Birincil (ve varsa kan bağı) yolun her kenarını sırayla engelleyerek alternatif ara.
        var seeds = new List<List<int>> { primary.Value.Ids };
        if (bloodOnlyPath != null && string.Join(",", bloodOnlyPath.Value.Ids) != string.Join(",", primary.Value.Ids))
        {
            seeds.Add(bloodOnlyPath.Value.Ids);
        }

        foreach (var seedIds in seeds)
        {
            for (var i = 0; i + 1 < seedIds.Count; i++)
            {
                var blocked = new HashSet<(int, int)>
                {
                    (seedIds[i], seedIds[i + 1]),
                    (seedIds[i + 1], seedIds[i]),
                };

                var alt = Bfs(false, blocked);
                if (alt != null)
                {
                    candidates.Add(alt.Value);
                }
            }
        }

        // Ortak çocuğu olan (anne+baba) çiftler — "eş dolambacı" filtrelemesi için.
        var coParentPairs = new HashSet<(int, int)>();
        foreach (var p in all)
        {
            if (p.AnneId is int ca && p.BabaId is int cb)
            {
                coParentPairs.Add((Math.Min(ca, cb), Math.Max(ca, cb)));
            }
        }

        // Bir yoldaki eş kenarı, iki ucu ortak çocuğa sahip (yani zaten yol üstünde bir
        // çocuk üzerinden bağlı) bir çiftse ve bu çift sorgulanan iki kişinin kendisi
        // DEĞİLSE, bu yol aynı ilişkiyi "eşinin üzerinden" dolanarak tekrar anlatan sahte
        // bir alternatiftir — ele.
        bool IsSpouseDetour(List<int> ids, List<string> kinds)
        {
            for (var i = 0; i < kinds.Count; i++)
            {
                if (kinds[i] != "spouse")
                {
                    continue;
                }

                var u = ids[i];
                var v = ids[i + 1];
                var isQueriedCouple =
                    (u == person1Id && v == person2Id) || (u == person2Id && v == person1Id);
                if (!isQueriedCouple && coParentPairs.Contains((Math.Min(u, v), Math.Max(u, v))))
                {
                    return true;
                }
            }

            return false;
        }

        // Tekilleştir (kişi dizisine göre), kısadan uzuna sırala, aşırı dolambaçlıları ve
        // eş dolambaçlarını ele.
        var primaryLength = primary.Value.Ids.Count - 1;
        var seenKeys = new HashSet<string>();
        var distinctPaths = new List<(List<int> Ids, List<string> Kinds)>();
        foreach (var candidate in candidates.OrderBy(c => c.Ids.Count))
        {
            if ((candidate.Ids.Count - 1) > primaryLength + 6)
            {
                continue;
            }

            // Birincil yol her koşulda kalır; alternatifler eş dolambacıysa elenir.
            if (distinctPaths.Count > 0 && IsSpouseDetour(candidate.Ids, candidate.Kinds))
            {
                continue;
            }

            if (seenKeys.Add(string.Join(",", candidate.Ids)))
            {
                distinctPaths.Add(candidate);
            }
        }

        distinctPaths = distinctPaths.Take(12).ToList();

        var allPathIds = distinctPaths.SelectMany(d => d.Ids).Distinct().ToList();
        var pathPersons = await LoadPersonsWithPhotosAsync(allPathIds);
        var byId = pathPersons.ToDictionary(p => p.Id);

        List<RelationshipStepDto> BuildSteps(List<int> ids, List<string> kinds)
        {
            var steps = new List<RelationshipStepDto>();
            for (var i = 0; i < kinds.Count; i++)
            {
                var from = byId[ids[i]];
                var to = byId[ids[i + 1]];
                var relation = kinds[i] switch
                {
                    "up" => to.Id == from.AnneId ? "annesi" : (to.Id == from.BabaId ? "babası" : "ebeveyni"),
                    "down" => "çocuğu",
                    "spouse" => "eşi",
                    _ => "akrabası",
                };

                steps.Add(new RelationshipStepDto
                {
                    FromId = from.Id,
                    FromName = $"{from.Ad} {from.Soyad}",
                    ToId = to.Id,
                    ToName = $"{to.Ad} {to.Soyad}",
                    Relation = relation,
                });
            }

            return steps;
        }

        static string PathKind(List<string> kinds)
        {
            var hasSpouse = kinds.Contains("spouse");
            var hasBlood = kinds.Any(k => k is "up" or "down");
            if (hasSpouse && hasBlood)
            {
                return "Kan + evlilik";
            }

            if (hasSpouse)
            {
                return "Evlilik bağı";
            }

            if (kinds.Count > 0 && kinds[0] == "down" && kinds.Contains("up"))
            {
                return "Ortak soy";
            }

            return "Kan bağı";
        }

        // Aynı ilişkiyi (aynı başlık) farklı düğümlerden dolaşan yolları tekilleştir —
        // ör. "anne üzerinden" ve "baba üzerinden" kardeşlik ikisi de "öz kardeşler" der.
        // En fazla 4 farklı yol göster.
        var seenSummaries = new HashSet<string>();
        foreach (var d in distinctPaths)
        {
            var summary = BuildRelationshipSummary(d.Ids, d.Kinds, byId);
            if (result.Paths.Count > 0 && !seenSummaries.Add(summary))
            {
                continue;
            }

            seenSummaries.Add(summary);
            result.Paths.Add(new RelationshipPathDto
            {
                PersonIds = d.Ids,
                Length = d.Ids.Count - 1,
                Kind = PathKind(d.Kinds),
                Summary = summary,
                Steps = BuildSteps(d.Ids, d.Kinds),
            });

            if (result.Paths.Count >= 4)
            {
                break;
            }
        }

        result.Related = true;
        result.Summary = result.Paths[0].Summary;

        result.Graph = await BuildConnectedGraphAsync(pathPersons, decorate: pp =>
        {
            if (pp.Id == person1Id)
            {
                return ("1. kişi", false);
            }

            return pp.Id == person2Id ? ("2. kişi", false) : (string.Empty, false);
        });

        return result;
    }

    private async Task<List<Person>> LoadPersonsWithPhotosAsync(IEnumerable<int> ids)
    {
        var idList = ids.Distinct().ToList();
        return await _context.Persons
            .AsNoTracking()
            .Include(p => p.Photos)
            .Where(p => idList.Contains(p.Id))
            .ToListAsync();
    }

    /// <summary>
    /// Yol adımlarından (yukarı = ebeveyne, aşağı = çocuğa, eş) okunabilir bir akrabalık başlığı üretir.
    /// En kısa kan bağı yolu her zaman "önce yukarı, sonra aşağı" biçimindedir (ortak atadan geçer).
    /// </summary>
    private static string BuildRelationshipSummary(List<int> pathIds, List<string> kinds, Dictionary<int, Person> byId)
    {
        var name1 = $"{byId[pathIds[0]].Ad} {byId[pathIds[0]].Soyad}";
        var name2 = $"{byId[pathIds[^1]].Ad} {byId[pathIds[^1]].Soyad}";

        if (kinds.Contains("spouse"))
        {
            if (kinds.Count == 1)
            {
                return $"{name1} ile {name2} eşler.";
            }

            return $"{name1} ile {name2} evlilik bağı üzerinden akraba ({kinds.Count} adımlık bağ).";
        }

        var up = kinds.Count(k => k == "up");
        var down = kinds.Count(k => k == "down");

        // Beklenen düzen: tüm "up" adımları, ardından tüm "down" adımları.
        var monotonic = true;
        var seenDown = false;
        foreach (var k in kinds)
        {
            if (k == "down")
            {
                seenDown = true;
            }
            else if (seenDown)
            {
                monotonic = false;
                break;
            }
        }

        if (!monotonic)
        {
            // "Aşağı sonra yukarı" (V) biçimi: iki kişinin ortak bir alt soyu (çocuk/torun) var.
            var valley = true;
            var seenUp = false;
            foreach (var k in kinds)
            {
                if (k == "up")
                {
                    seenUp = true;
                }
                else if (seenUp)
                {
                    valley = false;
                    break;
                }
            }

            if (valley)
            {
                var bottom = byId[pathIds[down]];
                var bottomName = $"{bottom.Ad} {bottom.Soyad}";
                if (down == 1 && up == 1)
                {
                    return $"{name1} ile {name2}, {bottomName} adlı çocuğun ortak ebeveyni.";
                }

                return $"{name1} ile {name2} ortak alt soy ({bottomName}) üzerinden bağlı.";
            }

            return $"{name1} ile {name2} akraba ({kinds.Count} adımlık bağ).";
        }

        if (up == 0)
        {
            return $"{name2}, {name1} kişisinin {DescendantLabel(down)}.";
        }

        if (down == 0)
        {
            var parentEdgeChildId = pathIds[up - 1];
            var parentEdgeParentId = pathIds[up];
            return $"{name2}, {name1} kişisinin {AncestorLabel(up, byId[parentEdgeChildId], parentEdgeParentId)}.";
        }

        if (up == 1 && down == 1)
        {
            var a = byId[pathIds[0]];
            var b = byId[pathIds[^1]];
            var sharedAnne = a.AnneId.HasValue && a.AnneId == b.AnneId;
            var sharedBaba = a.BabaId.HasValue && a.BabaId == b.BabaId;
            var tur = sharedAnne && sharedBaba ? "öz" : "üvey";
            return $"{name1} ile {name2} {tur} kardeşler.";
        }

        if (up == 1 && down == 2)
        {
            return $"{name2}, {name1} kişisinin yeğeni.";
        }

        if (up == 2 && down == 1)
        {
            var self = byId[pathIds[0]];
            var parentOnPath = pathIds[1];
            var relative = byId[pathIds[^1]];
            var anneSide = self.AnneId == parentOnPath;
            var label = relative.Cinsiyet switch
            {
                Gender.Erkek => anneSide ? "dayısı" : "amcası",
                Gender.Kadin => anneSide ? "teyzesi" : "halası",
                _ => anneSide ? "dayısı veya teyzesi" : "amcası veya halası",
            };

            return $"{name2}, {name1} kişisinin {label}.";
        }

        // up >= 2 && down >= 2: kuzenler
        var derece = Math.Min(up, down) - 1;
        var uzaklik = Math.Abs(up - down);
        if (uzaklik == 0)
        {
            return $"{name1} ile {name2} {derece}. dereceden kuzenler.";
        }

        return $"{name1} ile {name2} {derece}. dereceden kuzenler ({uzaklik} kuşak uzaktan).";
    }

    private static string DescendantLabel(int down) => down switch
    {
        1 => "çocuğu",
        2 => "torunu",
        3 => "torununun çocuğu",
        _ => $"{down}. kuşak alt soyundan",
    };

    private static string AncestorLabel(int up, Person childOnLastEdge, int parentId) => up switch
    {
        1 => parentId == childOnLastEdge.AnneId ? "annesi" : "babası",
        2 => parentId == childOnLastEdge.AnneId ? "büyükannesi" : "büyükbabası",
        3 => parentId == childOnLastEdge.AnneId ? "büyük büyükannesi" : "büyük büyükbabası",
        _ => $"{up}. kuşak üstündeki atası",
    };

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
