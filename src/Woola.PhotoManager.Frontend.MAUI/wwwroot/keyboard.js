(function () {
    window.showShortcuts = false;

    window.toggleShortcuts = function () {
        window.showShortcuts = !window.showShortcuts;
        return window.showShortcuts;
    };

    window.getShortcutsState = function () {
        return window.showShortcuts;
    };
})();
