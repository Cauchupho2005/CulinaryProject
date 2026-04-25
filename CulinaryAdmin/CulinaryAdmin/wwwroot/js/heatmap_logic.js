window.heatmapMap = null;
window.heatLayer = null;

window.drawAdminHeatmap = (mapId, points) => {
    // 1. Khởi tạo bản đồ nếu chưa có
    if (!window.heatmapMap) {
        // Mặc định ban đầu ở HCM
        window.heatmapMap = L.map(mapId).setView([10.7769, 106.7009], 14);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '© OpenStreetMap - TourGuide Enterprise'
        }).addTo(window.heatmapMap);
    }

    // 2. Xóa đốm nhiệt cũ
    if (window.heatLayer) {
        window.heatmapMap.removeLayer(window.heatLayer);
    }

    // 3. Vẽ đốm nhiệt mới
    if (points && points.length > 0) {
        window.heatLayer = L.heatLayer(points, {
            radius: 25,
            blur: 15,
            maxZoom: 17,
            gradient: { 0.4: 'blue', 0.6: 'cyan', 0.7: 'lime', 0.8: 'yellow', 1.0: 'red' }
        }).addTo(window.heatmapMap);

        window.heatmapMap.setView([points[0][0], points[0][1]]);
    }
};