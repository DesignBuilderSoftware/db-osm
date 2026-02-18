var map;
var drawnItems;
var drawHandler = null;
var isDrawing = false;
var isEditing = false;
var editHandler = null;
var activeDrawMode = null; // 'rectangle' or 'polygon'
var currentBounds = null;
var currentPolygonVertices = null;
var currentArea = 0;
var searchMarker = null;

var MAX_AREA_KM2 = 5;
var DEG_TO_KM = 111.32;

function initMap() {
    map = L.map('map').setView([51.505, -0.09], 13); // London

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);

    drawnItems = new L.FeatureGroup();
    map.addLayer(drawnItems);

    map.on(L.Draw.Event.CREATED, function (event) {
        setDrawingState(false);
        drawnItems.clearLayers();
        drawnItems.addLayer(event.layer);
        updateSelectionFromLayer(event.layer);
        updateStatus();
    });

    map.on(L.Draw.Event.EDITED, function (event) {
        event.layers.eachLayer(function (layer) {
            updateSelectionFromLayer(layer);
            updateStatus();
        });
    });

    map.on('draw:drawstop', function () {
        setDrawingState(false);
    });

    updateStatus();
}

function startDrawing(mode) {
    if (isDrawing) {
        if (drawHandler) drawHandler.disable();
        setDrawingState(false);
        return;
    }

    activeDrawMode = mode;
    var shapeOptions = { color: '#3498db', weight: 2, fillOpacity: 0.2 };
    var DrawType = mode === 'polygon' ? L.Draw.Polygon : L.Draw.Rectangle;
    drawHandler = new DrawType(map, { shapeOptions: shapeOptions });
    drawHandler.enable();
    setDrawingState(true);
}

function setDrawingState(drawing) {
    isDrawing = drawing;
    var rectBtn = document.getElementById('drawBtn');
    var polyBtn = document.getElementById('drawPolyBtn');
    if (drawing) {
        var activeBtn = activeDrawMode === 'polygon' ? polyBtn : rectBtn;
        var otherBtn = activeDrawMode === 'polygon' ? rectBtn : polyBtn;
        activeBtn.classList.add('drawing');
        activeBtn.innerHTML = 'Cancel Drawing';
        otherBtn.disabled = true;
    } else {
        rectBtn.classList.remove('drawing');
        rectBtn.innerHTML = 'Draw Rectangle';
        rectBtn.disabled = false;
        polyBtn.classList.remove('drawing');
        polyBtn.innerHTML = 'Draw Polygon';
        polyBtn.disabled = false;
        drawHandler = null;
        activeDrawMode = null;
    }
}

function toggleEdit() {
    if (isEditing) {
        editHandler.save();
        editHandler.disable();
        setEditState(false);
        drawnItems.eachLayer(function (layer) {
            updateSelectionFromLayer(layer);
        });
        updateStatus();
    } else {
        editHandler = new L.EditToolbar.Edit(map, { featureGroup: drawnItems });
        editHandler.enable();
        setEditState(true);
    }
}

function setEditState(editing) {
    isEditing = editing;
    var editBtn = document.getElementById('editBtn');
    editBtn.classList.toggle('editing', editing);
    editBtn.innerHTML = editing ? 'Save Edit' : 'Edit';
    document.getElementById('drawBtn').disabled = editing;
    document.getElementById('drawPolyBtn').disabled = editing;
    document.getElementById('clearBtn').disabled = editing;
    document.getElementById('loadBtn').disabled = editing;
    if (!editing) updateStatus();
}

function updateSelectionFromLayer(layer) {
    currentBounds = layer.getBounds();
    if (layer.getLatLngs && !(layer instanceof L.Rectangle)) {
        var latlngs = layer.getLatLngs()[0];
        currentPolygonVertices = latlngs.map(function (ll) {
            return { lat: ll.lat, lng: ll.lng };
        });
        currentArea = calculatePolygonAreaKm2(currentPolygonVertices);
    } else {
        currentPolygonVertices = null;
        currentArea = calculateBoundsAreaKm2(currentBounds);
    }
}

function calculateBoundsAreaKm2(bounds) {
    var ne = bounds.getNorthEast();
    var sw = bounds.getSouthWest();
    var latKm = (ne.lat - sw.lat) * DEG_TO_KM;
    var lonKm = (ne.lng - sw.lng) * DEG_TO_KM * Math.cos((ne.lat + sw.lat) / 2 * Math.PI / 180);
    return Math.abs(latKm * lonKm);
}

function calculatePolygonAreaKm2(vertices) {
    if (vertices.length < 3) return 0;
    var area = 0;
    for (var i = 0; i < vertices.length; i++) {
        var j = (i + 1) % vertices.length;
        var xi = vertices[i].lng * DEG_TO_KM * Math.cos(vertices[i].lat * Math.PI / 180);
        var yi = vertices[i].lat * DEG_TO_KM;
        var xj = vertices[j].lng * DEG_TO_KM * Math.cos(vertices[j].lat * Math.PI / 180);
        var yj = vertices[j].lat * DEG_TO_KM;
        area += xi * yj - xj * yi;
    }
    return Math.abs(area / 2);
}

function formatArea() {
    return currentArea < 0.01
        ? (currentArea * 1000000).toFixed(0) + ' m\u00B2'
        : currentArea.toFixed(2) + ' km\u00B2';
}

function formatBounds() {
    var ne = currentBounds.getNorthEast();
    var sw = currentBounds.getSouthWest();
    return 'Bounds: [' + sw.lat.toFixed(5) + ', ' + sw.lng.toFixed(5) +
        '] to [' + ne.lat.toFixed(5) + ', ' + ne.lng.toFixed(5) + ']';
}

function updateStatus() {
    var statusDiv = document.getElementById('status');
    var loadBtn = document.getElementById('loadBtn');
    var editBtn = document.getElementById('editBtn');

    if (currentBounds === null) {
        statusDiv.className = '';
        statusDiv.innerHTML = 'Ready. Draw a rectangle or polygon to select an area.';
        loadBtn.disabled = true;
        editBtn.disabled = true;
        return;
    }

    editBtn.disabled = false;
    var areaText = formatArea();
    var shapeType = currentPolygonVertices ? 'Polygon (' + currentPolygonVertices.length + ' vertices)' : 'Rectangle';

    if (currentArea > MAX_AREA_KM2) {
        statusDiv.className = 'status-error';
        statusDiv.innerHTML = '\u26A0 Area too large: ' + areaText + ' (max: ' + MAX_AREA_KM2 + ' km\u00B2)<br>' +
            shapeType + ' \u2014 Please select a smaller area.<br>' + formatBounds();
        loadBtn.disabled = true;
        showWarningModal(areaText);
    } else {
        statusDiv.className = '';
        statusDiv.innerHTML = shapeType + ' \u2014 Area: ' + areaText + '<br>' + formatBounds();
        loadBtn.disabled = false;
    }
}

function clearSelection() {
    if (isEditing) {
        editHandler.disable();
        setEditState(false);
    }
    drawnItems.clearLayers();
    currentBounds = null;
    currentPolygonVertices = null;
    currentArea = 0;
    updateStatus();
}

function showWarningModal(areaText) {
    document.getElementById('modalAreaText').textContent = areaText;
    document.getElementById('warningModal').style.display = 'block';
}

function closeWarningModal() {
    document.getElementById('warningModal').style.display = 'none';
}

function loadToDesignBuilder() {
    if (currentBounds === null) {
        alert('Please select an area first.');
        return;
    }

    if (currentArea > MAX_AREA_KM2) {
        showWarningModal(formatArea());
        return;
    }

    var ne = currentBounds.getNorthEast();
    var sw = currentBounds.getSouthWest();

    var selection = {
        south: sw.lat,
        west: sw.lng,
        north: ne.lat,
        east: ne.lng,
        area: currentArea,
        polygon: currentPolygonVertices
    };

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(selection));
    } else {
        alert('Communication with plugin failed. Please try again.');
    }
}

async function searchLocation() {
    var searchInput = document.getElementById('searchInput');
    var searchBtn = document.getElementById('searchBtn');
    var searchResults = document.getElementById('searchResults');
    var query = searchInput.value.trim();

    if (!query) return;

    searchBtn.disabled = true;
    searchBtn.textContent = 'Searching...';

    try {
        var response = await fetch(
            'https://nominatim.openstreetmap.org/search?format=json&q=' + encodeURIComponent(query) + '&limit=5',
            { headers: { 'User-Agent': 'DesignBuilder-OSM-Plugin' } }
        );

        if (!response.ok) throw new Error('Search failed with status: ' + response.status);

        displaySearchResults(await response.json());
    } catch (error) {
        searchResults.innerHTML = '<div class="search-no-results">Search failed: ' + error.message + '<br>Please try again.</div>';
        searchResults.style.display = 'block';
    } finally {
        searchBtn.disabled = false;
        searchBtn.textContent = 'Search';
    }
}

function displaySearchResults(results) {
    var searchResults = document.getElementById('searchResults');

    if (results.length === 0) {
        searchResults.innerHTML = '<div class="search-no-results">No results found. Try a different search term.</div>';
        searchResults.style.display = 'block';
        return;
    }

    var html = '';
    results.forEach(function (result, index) {
        html += '<div class="search-result-item" onclick="selectSearchResult(' + index + ')">';
        html += '<div class="search-result-name">' + escapeHtml(result.display_name.split(',')[0]) + '</div>';
        html += '<div class="search-result-address">' + escapeHtml(result.display_name) + '</div>';
        html += '</div>';
    });

    searchResults.innerHTML = html;
    searchResults.style.display = 'block';
    window.searchResultsData = results;
}

function selectSearchResult(index) {
    var result = window.searchResultsData[index];
    document.getElementById('searchResults').style.display = 'none';

    var lat = parseFloat(result.lat);
    var lon = parseFloat(result.lon);

    if (searchMarker) map.removeLayer(searchMarker);

    searchMarker = L.marker([lat, lon]).addTo(map);
    searchMarker.bindPopup('<b>' + escapeHtml(result.display_name.split(',')[0]) + '</b><br>' + escapeHtml(result.display_name)).openPopup();

    if (result.boundingbox) {
        var bbox = result.boundingbox;
        map.fitBounds([
            [parseFloat(bbox[0]), parseFloat(bbox[2])],
            [parseFloat(bbox[1]), parseFloat(bbox[3])]
        ]);
    } else {
        map.setView([lat, lon], 15);
    }

    document.getElementById('searchInput').value = '';
}

function handleSearchKeyPress(event) {
    if (event.key === 'Enter') searchLocation();
}

function escapeHtml(text) {
    var div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

document.addEventListener('click', function (event) {
    var searchResults = document.getElementById('searchResults');
    var searchContainer = document.getElementById('searchContainer');
    if (!searchContainer.contains(event.target) && !searchResults.contains(event.target)) {
        searchResults.style.display = 'none';
    }
});

window.onload = initMap;
