// poi-admin-map.js — Leaflet map picker cho trang POI Admin
// Hiển thị toàn bộ POI dưới dạng marker và cho phép click/kéo để chọn tọa độ.

window.poiAdminMap = (() => {
    // Map instances keyed by elementId
    const _maps = {};

    // Màu marker
    const COLOR_NORMAL  = '#3b82f6'; // xanh dương
    const COLOR_EDITING = '#ef4444'; // đỏ - POI đang chỉnh sửa
    const COLOR_PICKED  = '#f59e0b'; // vàng cam - điểm vừa click

    function _makeCircleMarker(lat, lng, color, radius, title) {
        return L.circleMarker([lat, lng], {
            radius: radius,
            fillColor: color,
            color: '#fff',
            weight: 2,
            opacity: 1,
            fillOpacity: 0.9,
        }).bindTooltip(title, { permanent: false, direction: 'top' });
    }

    /**
     * Khởi tạo (hoặc reinit) Leaflet map trong element có id = elementId.
     * @param {string} elementId   - id của div chứa map
     * @param {number} centerLat   - vĩ độ trung tâm ban đầu
     * @param {number} centerLng   - kinh độ trung tâm ban đầu
     * @param {string} markersJson - JSON array các POI { id, lat, lng, name, isEditing }
     * @param {DotNetObjectReference} dotNetRef - tham chiếu Blazor để gọi callback
     */
    function init(elementId, centerLat, centerLng, markersJson, dotNetRef) {
        const el = document.getElementById(elementId);
        if (!el) return;

        // Destroy bản đồ cũ nếu đã tồn tại
        if (_maps[elementId]) {
            _maps[elementId].map.remove();
            delete _maps[elementId];
        }

        // Tạo map mới
        const map = L.map(elementId, { zoomControl: true }).setView([centerLat, centerLng], 15);

        // Tile layer OpenStreetMap
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 19,
        }).addTo(map);

        const state = {
            map,
            dotNetRef,
            markerLayer: L.layerGroup().addTo(map),
            pickedMarker: null,
        };
        _maps[elementId] = state;

        // Render markers từ JSON
        _renderMarkers(state, markersJson);

        // Click trên map → gọi callback Blazor
        map.on('click', (e) => {
            const { lat, lng } = e.latlng;
            _setPickedMarker(state, lat, lng);
            dotNetRef.invokeMethodAsync('OnMapClicked', lat, lng);
        });

        // Fix: Đảm bảo map render đúng khi container vừa được mount
        setTimeout(() => map.invalidateSize(), 200);
    }

    /**
     * Chỉ refresh danh sách marker mà không tạo lại map.
     */
    function refreshMarkers(elementId, markersJson, panLat, panLng) {
        const state = _maps[elementId];
        if (!state) return;

        _renderMarkers(state, markersJson);

        if (panLat != null && panLng != null) {
            state.map.setView([panLat, panLng], state.map.getZoom());
        }
    }

    /**
     * Xóa và vẽ lại tất cả POI marker.
     */
    function _renderMarkers(state, markersJson) {
        state.markerLayer.clearLayers();

        let markers;
        try { markers = JSON.parse(markersJson); }
        catch { return; }

        markers.forEach(poi => {
            if (!poi.lat || !poi.lng) return;
            const color  = poi.isEditing ? COLOR_EDITING : COLOR_NORMAL;
            const radius = poi.isEditing ? 10 : 7;
            const label  = `📍 ${poi.name} (${poi.id})`;

            const m = _makeCircleMarker(poi.lat, poi.lng, color, radius, label);
            m.addTo(state.markerLayer);
        });
    }

    /**
     * Đặt/di chuyển marker đã pick (điểm vàng cam).
     */
    function _setPickedMarker(state, lat, lng) {
        if (state.pickedMarker) {
            state.pickedMarker.setLatLng([lat, lng]);
        } else {
            state.pickedMarker = L.marker([lat, lng], {
                draggable: true,
                title: 'Picked location – drag to adjust',
                icon: L.divIcon({
                    className: '',
                    html: `<div style="
                        width:18px; height:18px;
                        background:${COLOR_PICKED};
                        border:3px solid #fff;
                        border-radius:50%;
                        box-shadow:0 2px 6px rgba(0,0,0,0.4);
                        cursor:crosshair;
                    "></div>`,
                    iconSize: [18, 18],
                    iconAnchor: [9, 9],
                }),
            });
            state.pickedMarker.addTo(state.map);

            // Kéo marker cũng trigger callback
            state.pickedMarker.on('dragend', (e) => {
                const pos = e.target.getLatLng();
                state.dotNetRef.invokeMethodAsync('OnMapClicked', pos.lat, pos.lng);
            });
        }
    }

    /**
     * Dọn dẹp khi Blazor component dispose.
     */
    function destroy(elementId) {
        const state = _maps[elementId];
        if (!state) return;
        state.map.remove();
        delete _maps[elementId];
    }

    return { init, refreshMarkers, destroy };
})();
