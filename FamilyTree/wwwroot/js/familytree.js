(function () {
    'use strict';

    var app = document.getElementById('familyTreeApp');
    if (!app) {
        return;
    }

    var CARD_W = 170;
    var CARD_H = 86;
    var SPACING_X = 200;
    var SPACING_Y = 160;

    var svg = d3.select('#treeSvg');
    var wrapper = document.getElementById('treeWrapper');
    var loadingEl = document.getElementById('treeLoading');
    var g = svg.append('g').attr('class', 'viewport');
    var linksLayer = g.append('g').attr('class', 'links-layer');
    var nodesLayer = g.append('g').attr('class', 'nodes-layer');

    var zoomBehavior = d3.zoom()
        .scaleExtent([0.2, 3])
        .on('zoom', function (event) {
            g.attr('transform', event.transform);
        });
    svg.call(zoomBehavior);

    var state = {
        currentPersonId: null,
        nodesById: new Map(),
        links: [],
    };

    var EXPAND_BUTTON_IDS = [
        'showGrandparentsBtn',
        'showGrandchildrenBtn',
        'showNephewsBtn',
        'showAuntsUnclesBtn',
        'showCousinsBtn',
    ];

    function setLoading(isLoading) {
        loadingEl.classList.toggle('d-none', !isLoading);
    }

    function linkKey(l) {
        return l.source + '-' + l.target + '-' + l.relationship;
    }

    function mergeGraph(dto) {
        (dto.nodes || []).forEach(function (n) {
            if (!state.nodesById.has(n.id)) {
                state.nodesById.set(n.id, n);
            }
        });

        var existingKeys = new Set(state.links.map(linkKey));
        (dto.links || []).forEach(function (l) {
            var key = linkKey(l);
            if (!existingKeys.has(key)) {
                existingKeys.add(key);
                state.links.push(l);
            }
        });
    }

    function resetExpandButtons() {
        EXPAND_BUTTON_IDS.forEach(function (id) {
            document.getElementById(id).disabled = false;
        });
    }

    function computeLayout() {
        var nodes = Array.from(state.nodesById.values());
        if (nodes.length === 0) {
            return { nodes: [], links: [] };
        }

        var byGen = new Map();
        nodes.forEach(function (n) {
            if (!byGen.has(n.generation)) {
                byGen.set(n.generation, []);
            }
            byGen.get(n.generation).push(n);
        });

        var spouseOf = new Map();
        state.links.filter(function (l) { return l.relationship === 'spouse'; }).forEach(function (l) {
            spouseOf.set(l.source, l.target);
            spouseOf.set(l.target, l.source);
        });

        var parentsOf = new Map();
        state.links.filter(function (l) { return l.relationship === 'parent'; }).forEach(function (l) {
            if (!parentsOf.has(l.target)) {
                parentsOf.set(l.target, []);
            }
            parentsOf.get(l.target).push(l.source);
        });

        var generations = Array.from(byGen.keys()).sort(function (a, b) { return a - b; });
        var positioned = new Map();

        generations.forEach(function (gen) {
            var genNodes = byGen.get(gen);

            genNodes.forEach(function (n) {
                var parents = (parentsOf.get(n.id) || []).filter(function (pid) { return positioned.has(pid); });
                if (parents.length > 0) {
                    var sum = parents.reduce(function (acc, pid) { return acc + positioned.get(pid).x; }, 0);
                    n._anchor = sum / parents.length;
                } else {
                    n._anchor = null;
                }
            });

            genNodes.sort(function (a, b) {
                if (a._anchor === null && b._anchor === null) return a.id - b.id;
                if (a._anchor === null) return 1;
                if (b._anchor === null) return -1;
                return a._anchor - b._anchor;
            });

            var ordered = [];
            var visited = new Set();
            genNodes.forEach(function (n) {
                if (visited.has(n.id)) return;
                ordered.push(n);
                visited.add(n.id);
                var spouseId = spouseOf.get(n.id);
                if (spouseId !== undefined && byGen.get(gen).some(function (x) { return x.id === spouseId; }) && !visited.has(spouseId)) {
                    var spouseNode = byGen.get(gen).find(function (x) { return x.id === spouseId; });
                    ordered.push(spouseNode);
                    visited.add(spouseId);
                }
            });

            ordered.forEach(function (n, i) {
                var x = i * SPACING_X;
                var y = gen * SPACING_Y;
                n.x = x;
                n.y = y;
                positioned.set(n.id, { x: x, y: y });
            });
        });

        var resolvedLinks = state.links
            .filter(function (l) { return state.nodesById.has(l.source) && state.nodesById.has(l.target); })
            .map(function (l) {
                return {
                    source: state.nodesById.get(l.source),
                    target: state.nodesById.get(l.target),
                    relationship: l.relationship,
                };
            });

        return { nodes: nodes, links: resolvedLinks };
    }

    function truncate(text, max) {
        if (!text) return '';
        return text.length > max ? text.substring(0, max - 1) + '…' : text;
    }

    function personYears(d) {
        var birth = d.birthYear || '?';
        if (d.alive) {
            return birth + ' -';
        }
        return birth + ' - ' + (d.deathYear || '?');
    }

    function linkPath(d) {
        if (d.relationship === 'spouse') {
            var y = d.source.y + CARD_H / 2;
            return 'M' + (d.source.x + CARD_W / 2) + ',' + y + ' L' + (d.target.x + CARD_W / 2) + ',' + y;
        }

        var sx = d.source.x + CARD_W / 2;
        var sy = d.source.y + CARD_H;
        var tx = d.target.x + CARD_W / 2;
        var ty = d.target.y;
        var midY = sy + (ty - sy) / 2;
        return 'M' + sx + ',' + sy + ' C' + sx + ',' + midY + ' ' + tx + ',' + midY + ' ' + tx + ',' + ty;
    }

    function render() {
        var layout = computeLayout();

        var linkSel = linksLayer.selectAll('path.link')
            .data(layout.links, function (d) { return d.source.id + '-' + d.target.id + '-' + d.relationship; });

        linkSel.exit().remove();

        linkSel.enter()
            .append('path')
            .attr('class', function (d) { return 'link ' + d.relationship; })
            .attr('fill', 'none')
            .attr('stroke', function (d) { return d.relationship === 'spouse' ? '#c2185b' : '#555'; })
            .attr('stroke-width', 2)
            .attr('stroke-dasharray', function (d) { return d.relationship === 'spouse' ? '5,4' : null; })
            .merge(linkSel)
            .attr('d', linkPath);

        var nodeSel = nodesLayer.selectAll('g.person-node')
            .data(layout.nodes, function (d) { return d.id; });

        nodeSel.exit().remove();

        var nodeEnter = nodeSel.enter()
            .append('g')
            .attr('class', 'person-node')
            .style('cursor', 'pointer')
            .on('click', function (event, d) {
                if (d.id !== state.currentPersonId) {
                    loadBaseTree(d.id, true);
                }
            });

        nodeEnter.append('rect')
            .attr('class', 'card-bg')
            .attr('width', CARD_W)
            .attr('height', CARD_H)
            .attr('rx', 10)
            .attr('fill', '#fff');

        nodeEnter.append('circle')
            .attr('class', 'avatar-fallback')
            .attr('r', 16)
            .attr('cx', 24)
            .attr('cy', CARD_H / 2)
            .attr('fill', '#cfd8dc');

        nodeEnter.append('text')
            .attr('class', 'avatar-initial')
            .attr('x', 24)
            .attr('y', CARD_H / 2 + 5)
            .attr('text-anchor', 'middle')
            .attr('font-size', 13)
            .attr('fill', '#546e7a');

        nodeEnter.append('image')
            .attr('class', 'avatar-photo')
            .attr('width', 32)
            .attr('height', 32)
            .attr('x', 8)
            .attr('y', CARD_H / 2 - 16)
            .attr('clip-path', function (d) { return 'circle(16px at 16px 16px)'; })
            .attr('preserveAspectRatio', 'xMidYMid slice');

        nodeEnter.append('text')
            .attr('class', 'role-badge')
            .attr('x', CARD_W - 8)
            .attr('y', 16)
            .attr('text-anchor', 'end')
            .attr('font-size', 10)
            .attr('fill', '#90a4ae');

        nodeEnter.append('text')
            .attr('class', 'name-text')
            .attr('x', 50)
            .attr('y', CARD_H / 2 - 4)
            .attr('font-size', 13)
            .attr('font-weight', '600')
            .attr('fill', '#212121');

        nodeEnter.append('text')
            .attr('class', 'years-text')
            .attr('x', 50)
            .attr('y', CARD_H / 2 + 14)
            .attr('font-size', 11)
            .attr('fill', '#607d8b');

        var detailIcon = nodeEnter.append('g')
            .attr('class', 'detail-icon')
            .attr('transform', 'translate(' + (CARD_W - 22) + ', ' + (CARD_H - 22) + ')')
            .style('cursor', 'pointer')
            .on('click', function (event, d) {
                event.stopPropagation();
                window.location.href = '/Person/Details/' + d.id;
            });

        detailIcon.append('circle').attr('r', 10).attr('fill', '#e3f2fd');
        detailIcon.append('text')
            .attr('text-anchor', 'middle')
            .attr('y', 4)
            .attr('font-size', 11)
            .attr('fill', '#1976d2')
            .text('↗');

        var merged = nodeEnter.merge(nodeSel);

        merged.attr('transform', function (d) { return 'translate(' + d.x + ',' + d.y + ')'; });

        merged.select('rect.card-bg')
            .attr('stroke', function (d) { return d.isCenter ? '#f57f17' : (d.alive ? '#1976d2' : '#9e9e9e'); })
            .attr('stroke-width', function (d) { return d.isCenter ? 3 : 1.5; })
            .attr('stroke-dasharray', function (d) { return d.alive ? null : '4,3'; });

        merged.select('text.name-text').text(function (d) { return truncate(d.name, 18); });
        merged.select('text.years-text').text(personYears);
        merged.select('text.role-badge').text(function (d) { return d.isCenter ? '' : d.role; });

        merged.select('text.avatar-initial')
            .style('display', function (d) { return d.photoPath ? 'none' : null; })
            .text(function (d) { return d.name ? d.name.charAt(0).toUpperCase() : '?'; });

        merged.select('circle.avatar-fallback')
            .style('display', function (d) { return d.photoPath ? 'none' : null; });

        merged.select('image.avatar-photo')
            .attr('href', function (d) { return d.photoPath || null; })
            .style('display', function (d) { return d.photoPath ? null : 'none'; });
    }

    function fitToView(animate) {
        var nodes = Array.from(state.nodesById.values());
        if (nodes.length === 0) return;

        var minX = d3.min(nodes, function (d) { return d.x; });
        var maxX = d3.max(nodes, function (d) { return d.x; }) + CARD_W;
        var minY = d3.min(nodes, function (d) { return d.y; });
        var maxY = d3.max(nodes, function (d) { return d.y; }) + CARD_H;

        var contentW = maxX - minX;
        var contentH = maxY - minY;
        var boundsW = wrapper.clientWidth;
        var boundsH = wrapper.clientHeight;

        var scale = Math.min(boundsW / (contentW + 80), boundsH / (contentH + 80), 1.1);
        scale = Math.max(scale, 0.2);

        var tx = boundsW / 2 - scale * (minX + contentW / 2);
        var ty = boundsH / 2 - scale * (minY + contentH / 2);

        var transform = d3.zoomIdentity.translate(tx, ty).scale(scale);

        if (animate) {
            svg.transition().duration(500).call(zoomBehavior.transform, transform);
        } else {
            svg.call(zoomBehavior.transform, transform);
        }
    }

    function resizeSvg() {
        svg.attr('width', wrapper.clientWidth).attr('height', wrapper.clientHeight);
    }

    async function loadBaseTree(id, pushState) {
        setLoading(true);
        try {
            var res = await fetch('/api/familytree/' + id);
            if (!res.ok) {
                alert('Kişi bulunamadı.');
                return;
            }
            var dto = await res.json();

            state.nodesById = new Map();
            state.links = [];
            state.currentPersonId = id;
            mergeGraph(dto);
            resetExpandButtons();

            var centerNode = state.nodesById.get(id);
            document.querySelector('h2').textContent = 'Soy Ağacı' + (centerNode ? ' - ' + centerNode.name : '');

            if (pushState) {
                history.pushState(null, '', '/FamilyTree/' + id);
            }

            resizeSvg();
            render();
            fitToView(true);
        } finally {
            setLoading(false);
        }
    }

    async function expand(kind, endpoint, buttonId) {
        if (!state.currentPersonId) return;
        setLoading(true);
        try {
            var res = await fetch('/api/familytree/' + state.currentPersonId + '/' + endpoint);
            var dto = await res.json();
            mergeGraph(dto);
            document.getElementById(buttonId).disabled = true;
            resizeSvg();
            render();
            fitToView(true);
        } finally {
            setLoading(false);
        }
    }

    document.getElementById('zoomInBtn').addEventListener('click', function () {
        svg.transition().duration(200).call(zoomBehavior.scaleBy, 1.3);
    });
    document.getElementById('zoomOutBtn').addEventListener('click', function () {
        svg.transition().duration(200).call(zoomBehavior.scaleBy, 1 / 1.3);
    });
    document.getElementById('centerBtn').addEventListener('click', function () {
        fitToView(true);
    });
    document.getElementById('fullscreenBtn').addEventListener('click', function () {
        if (!document.fullscreenElement) {
            wrapper.requestFullscreen().then(function () {
                setTimeout(function () { resizeSvg(); fitToView(false); }, 100);
            });
        } else {
            document.exitFullscreen();
        }
    });
    document.addEventListener('fullscreenchange', function () {
        setTimeout(function () { resizeSvg(); fitToView(false); }, 100);
    });

    document.getElementById('showGrandparentsBtn').addEventListener('click', function () {
        expand('grandparents', 'grandparents', 'showGrandparentsBtn');
    });
    document.getElementById('showGrandchildrenBtn').addEventListener('click', function () {
        expand('grandchildren', 'grandchildren', 'showGrandchildrenBtn');
    });
    document.getElementById('showNephewsBtn').addEventListener('click', function () {
        expand('nephews', 'nephews', 'showNephewsBtn');
    });
    document.getElementById('showAuntsUnclesBtn').addEventListener('click', function () {
        expand('aunts-uncles', 'aunts-uncles', 'showAuntsUnclesBtn');
    });
    document.getElementById('showCousinsBtn').addEventListener('click', function () {
        expand('cousins', 'cousins', 'showCousinsBtn');
    });

    var searchInput = document.getElementById('treeSearch');
    var searchResults = document.getElementById('treeSearchResults');
    var searchTimeout;
    searchInput.addEventListener('input', function () {
        clearTimeout(searchTimeout);
        var q = searchInput.value.trim();
        if (q.length < 2) {
            searchResults.innerHTML = '';
            return;
        }
        searchTimeout = setTimeout(function () {
            fetch('/api/person/search?q=' + encodeURIComponent(q))
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    searchResults.innerHTML = '';
                    data.forEach(function (item) {
                        var el = document.createElement('button');
                        el.type = 'button';
                        el.className = 'list-group-item list-group-item-action';
                        el.textContent = item.adSoyad + (item.dogumYili ? ' (' + item.dogumYili + ')' : '');
                        el.addEventListener('click', function () {
                            searchInput.value = '';
                            searchResults.innerHTML = '';
                            loadBaseTree(item.id, true);
                        });
                        searchResults.appendChild(el);
                    });
                });
        }, 250);
    });
    document.addEventListener('click', function (e) {
        if (e.target !== searchInput) {
            searchResults.innerHTML = '';
        }
    });

    window.addEventListener('popstate', function () {
        var parts = location.pathname.split('/').filter(Boolean);
        var idPart = parts[parts.length - 1];
        var id = parseInt(idPart, 10);
        if (!isNaN(id)) {
            loadBaseTree(id, false);
        }
    });

    window.addEventListener('resize', function () {
        resizeSvg();
    });

    var initialId = parseInt(app.dataset.personId, 10);
    resizeSvg();
    if (!isNaN(initialId)) {
        loadBaseTree(initialId, false);
    }
})();
