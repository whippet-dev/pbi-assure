(() => {
    let pending = null;

    function cancel() {
        if (!pending) return;
        document.removeEventListener("focusin", pending.onFocus, true);
        document.removeEventListener("pointerdown", pending.onPointer, true);
        document.removeEventListener("keydown", pending.onKey, true);
        document.removeEventListener("visibilitychange", pending.onVisibility);
        pending = null;
    }

    window.pbiAssureAnalysisFocus = {
        begin(trigger) {
            cancel();
            const state = { allowed: document.activeElement === trigger };
            // Disabling the trigger can leave focus on body. That alone is not a request to move
            // elsewhere; an actual keyboard, pointer or focus interaction is.
            state.onFocus = event => {
                if (event.target !== trigger && event.target !== document.body) state.allowed = false;
            };
            state.onPointer = event => { if (!trigger.contains(event.target)) state.allowed = false; };
            state.onKey = () => { state.allowed = false; };
            state.onVisibility = () => { if (document.hidden) state.allowed = false; };
            pending = state;
            document.addEventListener("focusin", state.onFocus, true);
            document.addEventListener("pointerdown", state.onPointer, true);
            document.addEventListener("keydown", state.onKey, true);
            document.addEventListener("visibilitychange", state.onVisibility);
        },
        finish(heading) {
            const allowed = pending?.allowed && document.hasFocus();
            cancel();
            if (allowed && heading?.isConnected) heading.focus();
        },
        cancel
    };
})();
