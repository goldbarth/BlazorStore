window.SortableInterop = {
    instances: {},

    init: function (elementId, dotNetRef) {
        const el = document.getElementById(elementId);
        if (!el) return;

        // wichtig: alte Instanz weg
        this.destroy(elementId);

        this.instances[elementId] = new Sortable(el, {
            handle: ".drag-handle",

            // OPTIONAL aber hilfreich:
            filter: ".video-item, a, button, input, textarea, select, img",
            preventOnFilter: false,

            animation: 150,
            onEnd: function (evt) {
                dotNetRef.invokeMethodAsync("OnSortChanged", evt.oldIndex, evt.newIndex);
            }
        });
    },

    destroy: function (elementId) {
        const inst = this.instances[elementId];
        if (inst) {
            inst.destroy();
            delete this.instances[elementId];
        }
    }
};
