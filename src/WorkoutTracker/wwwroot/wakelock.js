// Needs a secure context (HTTPS or localhost).
// The browser drops the lock when the tab is hidden, so it's requested again when the tab is visible.
let lock = null;
let wanted = false;
let requesting = false;

async function request() {
    if (!wanted || lock || requesting || document.visibilityState !== 'visible') return;
    requesting = true;
    try {
        const l = await navigator.wakeLock?.request('screen');
        if (!l) return;
        // off() may have been called while the request was pending.
        if (!wanted) { await l.release(); return; }
        lock = l;
        l.addEventListener('release', () => { if (lock === l) lock = null; });
    } catch {
    } finally {
        requesting = false;
    }
}

document.addEventListener('visibilitychange', request);

window.wakeLock = {
    on: async () => { wanted = true; await request(); },
    off: async () => {
        wanted = false;
        const l = lock;
        lock = null;
        try { await l?.release(); } catch { }
    }
};
