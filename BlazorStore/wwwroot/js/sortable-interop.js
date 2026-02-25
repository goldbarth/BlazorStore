window.SortableInterop = {
    instances: {},
    clickHandlers: {},

    init: function (elementId, dotNetRef) {
        const el = document.getElementById(elementId);
        if (!el) return;

        this.destroy(elementId);

        this.instances[elementId] = new Sortable(el, {
            handle: ".drag-handle",
            animation: 150,
            onEnd: function (evt) {
                // DOM-Änderung rückgängig machen - Blazor übernimmt das Rendering
                var parent = evt.from;
                var item = evt.item;
                var children = parent.children;
                if (evt.oldIndex < children.length) {
                    parent.insertBefore(item, children[evt.oldIndex]);
                } else {
                    parent.appendChild(item);
                }

                dotNetRef.invokeMethodAsync("OnSortChanged", evt.oldIndex, evt.newIndex);
            }
        });

        // Click-Handler separat registrieren, da SortableJS pointerdown abfängt
        var clickHandler = function (e) {
            var item = e.target.closest(".video-list-item");
            if (!item) return;

            if (e.target.closest(".drag-handle")) return;

            var videoId = item.getAttribute("data-video-id");
            if (videoId) {
                dotNetRef.invokeMethodAsync("OnVideoClicked", videoId);
            }
        };

        el.addEventListener("click", clickHandler);
        this.clickHandlers[elementId] = clickHandler;
    },

    destroy: function (elementId) {
        var inst = this.instances[elementId];
        if (inst) {
            inst.destroy();
            delete this.instances[elementId];
        }

        var el = document.getElementById(elementId);
        var handler = this.clickHandlers[elementId];
        if (el && handler) {
            el.removeEventListener("click", handler);
        }
        delete this.clickHandlers[elementId];
    }
};
