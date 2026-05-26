// segmented control 드래그 지원 — pointer down/move/up 을 element 단위로 처리.
// .NET (Blazor) 의 OnMouseDown 은 시작 시점만 잡고, move/up 은 document 단위라서 JS 가 필요.
window.subTrackThemeSwitcher = (() => {
    const handlers = new WeakMap();

    function attach(el, dotnetRef) {
        if (!el) return;
        if (handlers.has(el)) return;

        let dragging = false;

        const onDown = (e) => {
            dragging = true;
        };
        const onMove = (e) => {
            if (!dragging) return;
            const x = e.clientX ?? (e.touches && e.touches[0] && e.touches[0].clientX);
            if (x == null) return;
            dotnetRef.invokeMethodAsync('OnPointerMoveJs', x);
        };
        const onUp = () => {
            if (!dragging) return;
            dragging = false;
            dotnetRef.invokeMethodAsync('OnPointerUpJs');
        };

        el.addEventListener('mousedown', onDown);
        el.addEventListener('touchstart', onDown, { passive: true });
        document.addEventListener('mousemove', onMove);
        document.addEventListener('touchmove', onMove, { passive: true });
        document.addEventListener('mouseup', onUp);
        document.addEventListener('touchend', onUp);

        handlers.set(el, { onDown, onMove, onUp });
    }

    function detach(el) {
        if (!el) return;
        const h = handlers.get(el);
        if (!h) return;
        el.removeEventListener('mousedown', h.onDown);
        el.removeEventListener('touchstart', h.onDown);
        document.removeEventListener('mousemove', h.onMove);
        document.removeEventListener('touchmove', h.onMove);
        document.removeEventListener('mouseup', h.onUp);
        document.removeEventListener('touchend', h.onUp);
        handlers.delete(el);
    }

    return { attach, detach };
})();
