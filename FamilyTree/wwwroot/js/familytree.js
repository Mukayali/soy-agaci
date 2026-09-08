(function () {
    'use strict';

    var app = document.getElementById('familyTreeApp');
    if (!app) {
        return;
    }

    var CARD_W = 270;
    var CARD_H = 136;
    var SPACING_X = 310;
    var SPACING_Y = 228;

    var PHOTO_X = 12;
    var PHOTO_Y = 12;
    var PHOTO_W = 94;
    var PHOTO_H = CARD_H - 24;
    var PHOTO_RADIUS = 14;
    var TEXT_X = PHOTO_X + PHOTO_W + 16;

    var svg = d3.select('#treeSvg');
    var wrapper = document.getElementById('treeWrapper');
    var loadingEl = document.getElementById('treeLoading');
    var g = svg.append('g').attr('class', 'viewport');
    var linksLayer = g.append('g').attr('class', 'links-layer');
    var nodesLayer = g.append('g').attr('class', 'nodes-layer');

    // clipPath, nodesLayer'ın bir çocuğu olarak tanımlanır — böylece PDF/PNG/SVG dışa
    // aktarma sırasında (bkz. buildExportSvg) nodesLayer klonlandığında bu tanım da
    // birlikte kopyalanır ve fotoğraf kırpma referansı (url(#personPhotoClip)) kopyada da
    // çözümlenir. svg kökündeki ayrı bir <defs> kullanılsaydı klonlanan SVG'de kaybolurdu.
    nodesLayer.append('defs')
        .append('clipPath')
        .attr('id', 'personPhotoClip')
        .append('rect')
        .attr('x', PHOTO_X)
        .attr('y', PHOTO_Y)
        .attr('width', PHOTO_W)
        .attr('height', PHOTO_H)
        .attr('rx', PHOTO_RADIUS);

    var zoomBehavior = d3.zoom()
        .scaleExtent([0.2, 3])
        .on('zoom', function (event) {
            g.attr('transform', event.transform);
        });
    svg.call(zoomBehavior);

    var state = {
        currentPersonId: null,
        currentSulaleId: null,
        nodesById: new Map(),
        links: [],
        pathNodeIds: new Set(),
        pathEndpointIds: new Set(),
        pathEdgeKeys: new Set(),
        relPaths: [],
        relSelectedPath: 0,
        relLinkParams: null,
    };

    function edgeKey(a, b) {
        return Math.min(a, b) + '-' + Math.max(a, b);
    }

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text == null ? '' : String(text);
        return div.innerHTML;
    }

    function clearRelationshipHighlight() {
        state.pathNodeIds = new Set();
        state.pathEndpointIds = new Set();
        state.pathEdgeKeys = new Set();
        state.relPaths = [];
        state.relSelectedPath = 0;
        state.relLinkParams = null;
        var resultEl = document.getElementById('relResult');
        if (resultEl) {
            resultEl.classList.add('d-none');
            resultEl.innerHTML = '';
        }
        var clearBtn = document.getElementById('relClearBtn');
        if (clearBtn) {
            clearBtn.classList.add('d-none');
        }
    }

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

    function disableExpandButtons() {
        EXPAND_BUTTON_IDS.forEach(function (id) {
            document.getElementById(id).disabled = true;
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
            .merge(linkSel)
            .attr('stroke', function (d) {
                if (state.pathEdgeKeys.has(edgeKey(d.source.id, d.target.id))) {
                    return '#e65100';
                }
                return d.relationship === 'spouse' ? '#c2185b' : '#555';
            })
            .attr('stroke-width', function (d) {
                return state.pathEdgeKeys.has(edgeKey(d.source.id, d.target.id)) ? 5 : 2;
            })
            .attr('stroke-dasharray', function (d) { return d.relationship === 'spouse' ? '5,4' : null; })
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
            .attr('rx', 20)
            .attr('fill', '#fff');

        nodeEnter.append('rect')
            .attr('class', 'avatar-fallback')
            .attr('x', PHOTO_X)
            .attr('y', PHOTO_Y)
            .attr('width', PHOTO_W)
            .attr('height', PHOTO_H)
            .attr('rx', PHOTO_RADIUS)
            .attr('fill', '#cfd8dc');

        nodeEnter.append('text')
            .attr('class', 'avatar-initial')
            .attr('x', PHOTO_X + PHOTO_W / 2)
            .attr('y', PHOTO_Y + PHOTO_H / 2 + 11)
            .attr('text-anchor', 'middle')
            .attr('font-size', 32)
            .attr('font-weight', '600')
            .attr('fill', '#607d8b');

        nodeEnter.append('image')
            .attr('class', 'avatar-photo')
            .attr('x', PHOTO_X)
            .attr('y', PHOTO_Y)
            .attr('width', PHOTO_W)
            .attr('height', PHOTO_H)
            .attr('clip-path', 'url(#personPhotoClip)')
            .attr('preserveAspectRatio', 'xMidYMid slice');

        nodeEnter.append('text')
            .attr('class', 'role-badge')
            .attr('x', CARD_W - 12)
            .attr('y', 20)
            .attr('text-anchor', 'end')
            .attr('font-size', 11)
            .attr('fill', '#90a4ae');

        nodeEnter.append('text')
            .attr('class', 'name-line1')
            .attr('x', TEXT_X)
            .attr('y', CARD_H / 2 - 15)
            .attr('font-size', 15)
            .attr('font-weight', '700')
            .attr('fill', '#212121');

        nodeEnter.append('text')
            .attr('class', 'name-line2')
            .attr('x', TEXT_X)
            .attr('y', CARD_H / 2 + 4)
            .attr('font-size', 15)
            .attr('font-weight', '700')
            .attr('fill', '#212121');

        nodeEnter.append('text')
            .attr('class', 'years-text')
            .attr('x', TEXT_X)
            .attr('y', CARD_H / 2 + 28)
            .attr('font-size', 15)
            .attr('fill', '#64748b');

        var detailIcon = nodeEnter.append('g')
            .attr('class', 'detail-icon')
            .attr('transform', 'translate(' + (CARD_W - 6) + ', ' + (CARD_H - 6) + ')')
            .style('cursor', 'pointer')
            .on('click', function (event, d) {
                event.stopPropagation();
                window.location.href = '/Person/Details/' + d.id;
            });

        detailIcon.append('circle').attr('r', 15).attr('fill', '#e3f2fd');
        detailIcon.append('text')
            .attr('text-anchor', 'middle')
            .attr('y', 5)
            .attr('font-size', 15)
            .attr('fill', '#1976d2')
            .text('↗');

        var merged = nodeEnter.merge(nodeSel);

        merged.attr('transform', function (d) { return 'translate(' + d.x + ',' + d.y + ')'; });

        merged.select('rect.card-bg')
            .attr('stroke', function (d) {
                if (d.isCenter) return '#f57f17';
                if (state.pathEndpointIds.has(d.id)) return '#e65100';
                if (state.pathNodeIds.has(d.id)) return '#00897b';
                return d.alive ? '#1976d2' : '#9e9e9e';
            })
            .attr('stroke-width', function (d) { return (d.isCenter || state.pathNodeIds.has(d.id)) ? 4 : 2.5; })
            .attr('stroke-dasharray', function (d) { return d.alive ? null : '9,6'; });

        merged.select('text.name-line1').text(function (d) { return truncate(d.ad || d.name, 16); });
        merged.select('text.name-line2').text(function (d) { return truncate(d.soyad || '', 16); });
        merged.select('text.years-text').text(personYears);
        merged.select('text.role-badge').text(function (d) { return d.isCenter ? '' : d.role; });

        merged.select('text.avatar-initial')
            .style('display', function (d) { return d.photoPath ? 'none' : null; })
            .text(function (d) { return d.name ? d.name.charAt(0).toUpperCase() : '?'; });

        merged.select('rect.avatar-fallback')
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

    async function inlinePhotoImages(svgRoot) {
        var images = Array.from(svgRoot.querySelectorAll('image')).filter(function (img) {
            var href = img.getAttribute('href');
            return href && !href.startsWith('data:');
        });

        await Promise.all(images.map(async function (img) {
            try {
                var href = img.getAttribute('href');
                var res = await fetch(href);
                var blob = await res.blob();
                var dataUrl = await new Promise(function (resolve, reject) {
                    var reader = new FileReader();
                    reader.onload = function () { resolve(reader.result); };
                    reader.onerror = reject;
                    reader.readAsDataURL(blob);
                });
                img.setAttribute('href', dataUrl);
            } catch (err) {
                img.setAttribute('href', null);
                img.removeAttribute('href');
            }
        }));
    }

    function downloadBlob(blob, fileName) {
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }

    function logExport(format) {
        var personId = state.currentPersonId
            || (state.relLinkParams ? state.relLinkParams.a : null);
        fetch('/api/familytree/log-export', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ personId: personId, format: format }),
        }).catch(function () { /* günlükleme başarısız olursa dışa aktarmayı engelleme */ });
    }

    function safeFileName(centerNode, extension) {
        var header = getExportHeader();
        var label = header.fileLabel
            || (centerNode ? centerNode.name : document.getElementById('treeTitle').textContent.replace('Soy Ağacı', '').trim());
        var safeLabel = (label || 'agaci').replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-+|-+$/g, '');
        var prefix = header.fileLabel ? 'akrabalik-bagi-' : 'soy-agaci-';
        return prefix + (safeLabel || 'agaci') + '.' + extension;
    }

    /**
     * Dışa aktarılan dosyanın başlığını üretir. Akrabalık bağı modundaysa (state.relPaths dolu)
     * başlık iki kişiyi, seçili yolun türünü/uzunluğunu, özetini ve adımlarını içerir — böylece
     * PDF/PNG/SVG hangi yolun vurgulandığını kendi başına anlatır.
     */
    function getExportHeader() {
        var paths = state.relPaths || [];
        if (paths.length > 0 && state.relLinkParams) {
            var n1 = state.nodesById.get(state.relLinkParams.a);
            var n2 = state.nodesById.get(state.relLinkParams.b);
            var name1 = n1 ? n1.name : '1. kişi';
            var name2 = n2 ? n2.name : '2. kişi';
            var idx = state.relSelectedPath || 0;
            var total = paths.length;
            var path = paths[idx] || {};

            var subLines = [];
            subLines.push((total > 1 ? (idx + 1) + '/' + total + '. yol · ' : 'Yol: ') +
                (path.kind || '') + ' (' + (path.length || 0) + ' adım)');
            if (path.summary) {
                subLines.push(path.summary);
            }
            (path.steps || []).forEach(function (s, i) {
                subLines.push((i + 1) + '. ' + s.fromName + ' → ' + s.toName + ': ' + s.relation);
            });

            return {
                title: 'Akrabalık Bağı: ' + name1 + ' — ' + name2,
                subLines: subLines,
                stepStart: path.summary ? 2 : 1,
                fileLabel: name1 + '-' + name2 + (total > 1 ? '-yol-' + (idx + 1) : ''),
            };
        }

        return {
            title: document.getElementById('treeTitle').textContent.trim(),
            subLines: [],
            stepStart: 0,
            fileLabel: null,
        };
    }

    /**
     * O an ekranda yüklü tüm soy ağacını (mevcut zoom/pan durumundan bağımsız, tam içerik)
     * başlık ve tarih bilgisiyle bağımsız bir <svg> içine kopyalar. Fotoğraf <image>
     * öğeleri base64 data URI'ye gömülür — aksi halde SVG bir blob/data URL üzerinden
     * rasterize edilirken (PNG/PDF dışa aktarma) iç içe ağ isteklerinin zamanlaması
     * tarayıcıda güvenilir şekilde beklenmeyebilir ve fotoğraflar boş kalabilir.
     */
    async function buildExportSvg() {
        var nodes = Array.from(state.nodesById.values());
        if (nodes.length === 0) {
            return null;
        }

        var header = getExportHeader();
        var headerLineHeight = 16;

        var padding = 40;
        var headerHeight = 52 + header.subLines.length * headerLineHeight + (header.subLines.length ? 10 : 0);
        var footerHeight = 30;

        var minX = d3.min(nodes, function (d) { return d.x; });
        var maxX = d3.max(nodes, function (d) { return d.x; }) + CARD_W;
        var minY = d3.min(nodes, function (d) { return d.y; });
        var maxY = d3.max(nodes, function (d) { return d.y; }) + CARD_H;

        var contentW = maxX - minX;
        var contentH = maxY - minY;
        var pageW = contentW + padding * 2;
        var pageH = contentH + padding * 2 + headerHeight + footerHeight;

        var exportSvg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        exportSvg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        exportSvg.setAttribute('width', pageW);
        exportSvg.setAttribute('height', pageH);
        exportSvg.setAttribute('viewBox', '0 0 ' + pageW + ' ' + pageH);
        exportSvg.setAttribute('font-family', 'Arial, Helvetica, sans-serif');

        var bg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        bg.setAttribute('x', 0);
        bg.setAttribute('y', 0);
        bg.setAttribute('width', pageW);
        bg.setAttribute('height', pageH);
        bg.setAttribute('fill', '#ffffff');
        exportSvg.appendChild(bg);

        var contentGroup = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        contentGroup.setAttribute('transform', 'translate(' + (padding - minX) + ',' + (headerHeight + padding - minY) + ')');
        contentGroup.appendChild(linksLayer.node().cloneNode(true));
        contentGroup.appendChild(nodesLayer.node().cloneNode(true));
        exportSvg.appendChild(contentGroup);

        await inlinePhotoImages(exportSvg);

        var centerNode = state.currentPersonId ? state.nodesById.get(state.currentPersonId) : null;

        var titleEl = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        titleEl.setAttribute('x', padding);
        titleEl.setAttribute('y', 30);
        titleEl.setAttribute('font-size', 20);
        titleEl.setAttribute('font-weight', 'bold');
        titleEl.setAttribute('fill', '#212121');
        titleEl.textContent = header.title;
        exportSvg.appendChild(titleEl);

        header.subLines.forEach(function (line, i) {
            var lineEl = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            lineEl.setAttribute('x', padding);
            lineEl.setAttribute('y', 30 + 22 + i * headerLineHeight);
            var isStep = i >= header.stepStart;
            lineEl.setAttribute('font-size', isStep ? 11 : 12.5);
            lineEl.setAttribute('font-weight', i === 0 ? 'bold' : 'normal');
            lineEl.setAttribute('fill', i === 0 ? '#e65100' : (isStep ? '#555555' : '#212121'));
            lineEl.textContent = line;
            exportSvg.appendChild(lineEl);
        });

        var dateEl = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        dateEl.setAttribute('x', padding);
        dateEl.setAttribute('y', pageH - 12);
        dateEl.setAttribute('font-size', 10);
        dateEl.setAttribute('fill', '#757575');
        dateEl.textContent = 'Oluşturma tarihi: ' + new Date().toLocaleDateString('tr-TR');
        exportSvg.appendChild(dateEl);

        return { exportSvg: exportSvg, pageW: pageW, pageH: pageH, centerNode: centerNode };
    }

    async function svgToCanvas(exportSvg, pageW, pageH, scale) {
        var svgString = new XMLSerializer().serializeToString(exportSvg);
        var svgBlob = new Blob([svgString], { type: 'image/svg+xml;charset=utf-8' });
        var svgUrl = URL.createObjectURL(svgBlob);

        var img = new Image();
        var imageLoaded = new Promise(function (resolve, reject) {
            img.onload = resolve;
            img.onerror = reject;
        });
        img.src = svgUrl;
        await imageLoaded;

        var canvas = document.createElement('canvas');
        canvas.width = pageW * scale;
        canvas.height = pageH * scale;
        var ctx = canvas.getContext('2d');
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.scale(scale, scale);
        ctx.drawImage(img, 0, 0, pageW, pageH);
        URL.revokeObjectURL(svgUrl);

        return canvas;
    }

    async function runExport(buttonId, busyLabel, errorLabel, fn) {
        var btn = document.getElementById(buttonId);
        var originalLabel = btn.textContent;
        btn.disabled = true;
        btn.textContent = busyLabel;

        try {
            var built = await buildExportSvg();
            if (!built) {
                return;
            }
            await fn(built);
        } catch (err) {
            console.error(err);
            alert(errorLabel);
        } finally {
            btn.disabled = false;
            btn.textContent = originalLabel;
        }
    }

    function exportPdf() {
        return runExport('exportPdfBtn', 'Hazırlanıyor...', 'PDF oluşturulurken bir hata oluştu.', async function (built) {
            if (typeof jspdf === 'undefined' || typeof jspdf.jsPDF === 'undefined') {
                alert('PDF oluşturma kütüphanesi yüklenemedi.');
                return;
            }

            // Metni jsPDF'in özel font gömme mekanizmasına (Türkçe karakterlerde hatalı
            // glif üretebiliyor) bırakmak yerine, SVG'yi tarayıcının kendi doğru font
            // render motoruyla bir canvas'a çizip JPEG olarak PDF'e gömüyoruz
            // (jsPDF'in PNG kodlayıcısı aynı görüntüyü onlarca kat daha büyük üretiyor).
            var canvas = await svgToCanvas(built.exportSvg, built.pageW, built.pageH, 2);
            var jpgDataUrl = canvas.toDataURL('image/jpeg', 0.95);

            var doc = new jspdf.jsPDF({
                orientation: built.pageW > built.pageH ? 'landscape' : 'portrait',
                unit: 'px',
                format: [built.pageW, built.pageH],
                hotfixes: ['px_scaling'],
                compress: true,
            });

            doc.addImage(jpgDataUrl, 'JPEG', 0, 0, built.pageW, built.pageH, undefined, 'FAST');
            doc.save(safeFileName(built.centerNode, 'pdf'));
            logExport('pdf');
        });
    }

    function exportPng() {
        return runExport('exportPngBtn', 'Hazırlanıyor...', 'PNG oluşturulurken bir hata oluştu.', async function (built) {
            var canvas = await svgToCanvas(built.exportSvg, built.pageW, built.pageH, 2);
            canvas.toBlob(function (blob) {
                downloadBlob(blob, safeFileName(built.centerNode, 'png'));
                logExport('png');
            }, 'image/png');
        });
    }

    function exportSvgFile() {
        return runExport('exportSvgBtn', 'Hazırlanıyor...', 'SVG oluşturulurken bir hata oluştu.', function (built) {
            var svgString = new XMLSerializer().serializeToString(built.exportSvg);
            var blob = new Blob([svgString], { type: 'image/svg+xml;charset=utf-8' });
            downloadBlob(blob, safeFileName(built.centerNode, 'svg'));
            logExport('svg');
        });
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
            state.currentSulaleId = null;
            clearRelationshipHighlight();
            mergeGraph(dto);
            resetExpandButtons();

            var sulaleSelectEl = document.getElementById('sulaleSelect');
            if (sulaleSelectEl) {
                sulaleSelectEl.value = '';
            }

            var centerNode = state.nodesById.get(id);
            document.getElementById('treeTitle').textContent = 'Soy Ağacı' + (centerNode ? ' - ' + centerNode.name : '');

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

    async function loadSulaleTree(sulaleId, pushState) {
        setLoading(true);
        try {
            var res = await fetch('/api/familytree/sulale/' + sulaleId);
            if (!res.ok) {
                alert('Sülale bulunamadı.');
                return;
            }
            var dto = await res.json();

            state.nodesById = new Map();
            state.links = [];
            state.currentPersonId = null;
            state.currentSulaleId = sulaleId;
            clearRelationshipHighlight();
            mergeGraph(dto);
            disableExpandButtons();

            var sulaleSelectEl = document.getElementById('sulaleSelect');
            var sulaleAdi = '';
            if (sulaleSelectEl) {
                sulaleSelectEl.value = String(sulaleId);
                var selectedOption = sulaleSelectEl.options[sulaleSelectEl.selectedIndex];
                sulaleAdi = selectedOption ? selectedOption.textContent : '';
            }
            document.getElementById('treeTitle').textContent = 'Soy Ağacı - ' + sulaleAdi + ' Sülalesi';

            if (pushState) {
                history.pushState(null, '', '/FamilyTree/Sulale/' + sulaleId);
            }

            resizeSvg();
            render();
            fitToView(true);
        } finally {
            setLoading(false);
        }
    }

    function setRelationshipInputs(data) {
        var p1 = document.getElementById('relPerson1');
        var p1Id = document.getElementById('relPerson1Id');
        var p2 = document.getElementById('relPerson2');
        var p2Id = document.getElementById('relPerson2Id');
        if (p1 && data.person1Name) { p1.value = data.person1Name; p1Id.value = data.person1Id; }
        if (p2 && data.person2Name) { p2.value = data.person2Name; p2Id.value = data.person2Id; }
    }

    async function loadRelationship(aId, bId, pushUrl) {
        var resultEl = document.getElementById('relResult');
        setLoading(true);
        try {
            var res = await fetch('/api/familytree/relationship?a=' + aId + '&b=' + bId);
            if (!res.ok) {
                alert('Akrabalık bağı sorgulanamadı.');
                return;
            }
            var data = await res.json();

            resultEl.classList.remove('d-none');
            setRelationshipInputs(data);

            if (pushUrl) {
                history.pushState(null, '', '/FamilyTree?rel1=' + aId + '&rel2=' + bId);
            }

            if (!data.related) {
                var warningHtml = '<div class="alert alert-warning mb-0 py-2">' + escapeHtml(data.summary) + '</div>';
                if (state.nodesById.size === 0) {
                    // Harita boşsa 1. kişinin temel ağacını göster ki sayfa boş kalmasın.
                    await loadBaseTree(aId, false);
                } else {
                    render();
                }
                clearRelationshipHighlight();
                resultEl.classList.remove('d-none');
                resultEl.innerHTML = warningHtml;
                return;
            }

            state.nodesById = new Map();
            state.links = [];
            state.currentPersonId = null;
            state.currentSulaleId = null;
            mergeGraph(data.graph);
            disableExpandButtons();

            var sulaleSelectEl = document.getElementById('sulaleSelect');
            if (sulaleSelectEl) {
                sulaleSelectEl.value = '';
            }

            document.getElementById('treeTitle').textContent = 'Soy Ağacı - Akrabalık Bağı';

            state.relPaths = data.paths || [];
            state.relLinkParams = { a: aId, b: bId };
            selectRelationshipPath(0, true);

            document.getElementById('relClearBtn').classList.remove('d-none');
        } finally {
            setLoading(false);
        }
    }

    function applyPathHighlight(personIds) {
        state.pathNodeIds = new Set(personIds);
        state.pathEndpointIds = new Set();
        if (personIds.length > 0) {
            state.pathEndpointIds.add(personIds[0]);
            state.pathEndpointIds.add(personIds[personIds.length - 1]);
        }
        state.pathEdgeKeys = new Set();
        for (var i = 0; i < personIds.length - 1; i++) {
            state.pathEdgeKeys.add(edgeKey(personIds[i], personIds[i + 1]));
        }
    }

    function selectRelationshipPath(idx, doFit) {
        var paths = state.relPaths || [];
        if (paths.length === 0) {
            return;
        }

        idx = Math.max(0, Math.min(idx, paths.length - 1));
        state.relSelectedPath = idx;
        var path = paths[idx];

        applyPathHighlight(path.personIds || []);

        var resultEl = document.getElementById('relResult');
        var stepsHtml = (path.steps || []).map(function (s) {
            return '<li>' + escapeHtml(s.fromName) + ' → <strong>' + escapeHtml(s.toName) + '</strong>: ' + escapeHtml(s.relation) + '</li>';
        }).join('');

        var tabsHtml = '';
        if (paths.length > 1) {
            tabsHtml = '<div class="btn-group btn-group-sm mb-2 flex-wrap" role="group" aria-label="Akrabalık yolları">' +
                paths.map(function (p, i) {
                    return '<button type="button" class="btn ' + (i === idx ? 'btn-warning' : 'btn-outline-warning') +
                        '" data-rel-path="' + i + '">' + (i + 1) + '. yol · ' + escapeHtml(p.kind) + ' (' + p.length + ' adım)</button>';
                }).join('') +
                '</div>';
        }

        resultEl.classList.remove('d-none');
        resultEl.innerHTML =
            tabsHtml +
            '<div class="fw-bold mb-1">' + escapeHtml(path.summary) + '</div>' +
            (stepsHtml ? '<ol class="mb-1 ps-3">' + stepsHtml + '</ol>' : '') +
            '<div class="text-muted">' +
            (paths.length > 1 ? 'Bu iki kişi arasında <strong>' + paths.length + ' farklı yol</strong> bulundu. ' : '') +
            'Seçili yol haritada <span style="color:#e65100;font-weight:600;">turuncu</span> ile vurgulandı. Bir karta tıklayınca o kişinin ağacına geçebilirsiniz.</div>' +
            '<button type="button" id="relCopyLinkBtn" class="btn btn-outline-secondary btn-sm mt-1">Paylaşılabilir bağlantıyı kopyala</button>';

        resultEl.querySelectorAll('[data-rel-path]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                selectRelationshipPath(parseInt(btn.dataset.relPath, 10), false);
            });
        });

        var copyBtn = document.getElementById('relCopyLinkBtn');
        copyBtn.addEventListener('click', function () {
            var lp = state.relLinkParams || {};
            var url = location.origin + '/FamilyTree?rel1=' + lp.a + '&rel2=' + lp.b;
            navigator.clipboard.writeText(url).then(function () {
                copyBtn.textContent = 'Bağlantı kopyalandı ✓';
                setTimeout(function () { copyBtn.textContent = 'Paylaşılabilir bağlantıyı kopyala'; }, 2000);
            }).catch(function () {
                window.prompt('Bağlantıyı kopyalayın:', url);
            });
        });

        resizeSvg();
        render();
        if (doFit) {
            fitToView(true);
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
    document.getElementById('exportPdfBtn').addEventListener('click', exportPdf);
    document.getElementById('exportPngBtn').addEventListener('click', exportPng);
    document.getElementById('exportSvgBtn').addEventListener('click', exportSvgFile);

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

    document.getElementById('sulaleSelect').addEventListener('change', function () {
        var val = this.value;
        if (val) {
            loadSulaleTree(parseInt(val, 10), true);
        } else if (state.currentPersonId) {
            loadBaseTree(state.currentPersonId, true);
        } else {
            window.location.href = '/FamilyTree';
        }
    });

    function attachSearch(input, results, onPick) {
        var timer;
        input.addEventListener('input', function () {
            clearTimeout(timer);
            var q = input.value.trim();
            if (q.length < 2) {
                results.innerHTML = '';
                return;
            }
            timer = setTimeout(function () {
                fetch('/api/person/search?q=' + encodeURIComponent(q))
                    .then(function (r) { return r.json(); })
                    .then(function (data) {
                        results.innerHTML = '';
                        data.forEach(function (item) {
                            var el = document.createElement('button');
                            el.type = 'button';
                            el.className = 'list-group-item list-group-item-action';
                            el.textContent = item.adSoyad + (item.dogumYili ? ' (' + item.dogumYili + ')' : '') + (item.tcKimlikNoMasked ? ' — TC: ' + item.tcKimlikNoMasked : '');
                            el.addEventListener('click', function () {
                                results.innerHTML = '';
                                onPick(item);
                            });
                            results.appendChild(el);
                        });
                    });
            }, 250);
        });
        document.addEventListener('click', function (e) {
            if (e.target !== input) {
                results.innerHTML = '';
            }
        });
    }

    var searchInput = document.getElementById('treeSearch');
    attachSearch(searchInput, document.getElementById('treeSearchResults'), function (item) {
        searchInput.value = '';
        loadBaseTree(item.id, true);
    });

    var relP1 = document.getElementById('relPerson1');
    var relP1Id = document.getElementById('relPerson1Id');
    var relP2 = document.getElementById('relPerson2');
    var relP2Id = document.getElementById('relPerson2Id');

    attachSearch(relP1, document.getElementById('relPerson1Results'), function (item) {
        relP1.value = item.adSoyad;
        relP1Id.value = item.id;
    });
    attachSearch(relP2, document.getElementById('relPerson2Results'), function (item) {
        relP2.value = item.adSoyad;
        relP2Id.value = item.id;
    });

    relP1.addEventListener('input', function () { relP1Id.value = ''; });
    relP2.addEventListener('input', function () { relP2Id.value = ''; });

    document.getElementById('relFindBtn').addEventListener('click', function () {
        var a = parseInt(relP1Id.value, 10);
        var b = parseInt(relP2Id.value, 10);
        if (isNaN(a) || isNaN(b)) {
            alert('Lütfen listeden iki kişi de seçin.');
            return;
        }
        if (a === b) {
            alert('Lütfen farklı iki kişi seçin.');
            return;
        }
        loadRelationship(a, b, true);
    });

    document.getElementById('relClearBtn').addEventListener('click', function () {
        relP1.value = '';
        relP1Id.value = '';
        relP2.value = '';
        relP2Id.value = '';
        clearRelationshipHighlight();
        window.location.href = '/FamilyTree';
    });

    window.addEventListener('popstate', function () {
        var relParams = new URLSearchParams(location.search);
        var pr1 = parseInt(relParams.get('rel1'), 10);
        var pr2 = parseInt(relParams.get('rel2'), 10);
        if (!isNaN(pr1) && !isNaN(pr2)) {
            loadRelationship(pr1, pr2, false);
            return;
        }

        var sulaleMatch = /\/FamilyTree\/Sulale\/(\d+)/i.exec(location.pathname);
        if (sulaleMatch) {
            loadSulaleTree(parseInt(sulaleMatch[1], 10), false);
            return;
        }

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
    var initialSulaleId = parseInt(app.dataset.sulaleId, 10);
    var initialRel1 = parseInt(app.dataset.rel1, 10);
    var initialRel2 = parseInt(app.dataset.rel2, 10);
    resizeSvg();
    if (!isNaN(initialRel1) && !isNaN(initialRel2)) {
        loadRelationship(initialRel1, initialRel2, false);
    } else if (!isNaN(initialSulaleId)) {
        loadSulaleTree(initialSulaleId, false);
    } else if (!isNaN(initialId)) {
        loadBaseTree(initialId, false);
    }
})();
