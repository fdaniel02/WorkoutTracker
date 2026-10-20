const typeOrderKey = 'type-order';
window.typeOrder = {
    load: () => { try { return localStorage.getItem(typeOrderKey); } catch { return null; } },
    save: value => { try { localStorage.setItem(typeOrderKey, value); } catch { } }
};
