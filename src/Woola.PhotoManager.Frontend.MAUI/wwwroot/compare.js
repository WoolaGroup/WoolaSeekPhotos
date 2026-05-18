(function () {
    let isDragging = false;
    let container = null;
    let slider = null;
    let rightImg = null;

    function initCompareSlider() {
        container = document.getElementById('compare-container');
        slider = document.getElementById('compare-slider');
        rightImg = document.getElementById('compare-right');

        if (!container || !slider || !rightImg) return;

        slider.addEventListener('mousedown', startDrag);
        slider.addEventListener('touchstart', startDrag);
    }

    function startDrag(e) {
        isDragging = true;
        document.addEventListener('mousemove', onDrag);
        document.addEventListener('mouseup', stopDrag);
        document.addEventListener('touchmove', onDrag);
        document.addEventListener('touchend', stopDrag);
        e.preventDefault();
    }

    function onDrag(e) {
        if (!isDragging) return;
        const rect = container.getBoundingClientRect();
        const x = (e.clientX || e.touches[0].clientX) - rect.left;
        const percent = Math.max(0, Math.min(100, (x / rect.width) * 100));
        slider.style.left = percent + '%';
        rightImg.style.clipPath = 'inset(0 0 0 ' + percent + '%)';
    }

    function stopDrag() {
        isDragging = false;
        document.removeEventListener('mousemove', onDrag);
        document.removeEventListener('mouseup', stopDrag);
        document.removeEventListener('touchmove', onDrag);
        document.removeEventListener('touchend', stopDrag);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCompareSlider);
    } else {
        initCompareSlider();
    }

    window.initCompareSlider = initCompareSlider;
})();
