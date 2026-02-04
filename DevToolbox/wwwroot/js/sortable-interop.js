window.SortableInterop = {
    instances: {},

    init: function (elementId, dotNetRef) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.error('SortableInterop: Element not found:', elementId);
            return;
        }
        
        if (this.instances[elementId]) {
            this.instances[elementId].destroy();
        }

        this.instances[elementId] = new Sortable(element, {
            animation: 150,
            handle: '.drag-handle',
            ghostClass: 'sortable-ghost',
            chosenClass: 'sortable-chosen',
            dragClass: 'sortable-drag',
            onEnd: function (evt) {
                dotNetRef.invokeMethodAsync('OnSortChanged', evt.oldIndex, evt.newIndex);
            }
        });
    },

    destroy: function (elementId) {
        if (this.instances[elementId]) {
            this.instances[elementId].destroy();
            delete this.instances[elementId];
        }
    }
};
