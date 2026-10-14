const key = 'workout-in-progress';
window.workoutState = {
    load: () => { try { return localStorage.getItem(key); } catch { return null; } },
    save: json => { try { localStorage.setItem(key, json); } catch { } },
    clear: () => { try { localStorage.removeItem(key); } catch { } }
};
