function localState(key) {
    return {
        load: () => { try { return localStorage.getItem(key); } catch { return null; } },
        save: json => { try { localStorage.setItem(key, json); } catch { } },
        clear: () => { try { localStorage.removeItem(key); } catch { } }
    };
}
window.workoutState = localState('workout-in-progress');
window.routineState = localState('routine-in-progress');
