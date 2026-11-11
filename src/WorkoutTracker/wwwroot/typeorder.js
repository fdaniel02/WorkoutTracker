const typeOrderKey = 'type-order';
window.typeOrder = {
    load: () => { try { return localStorage.getItem(typeOrderKey); } catch { return null; } },
    save: value => { try { localStorage.setItem(typeOrderKey, value); } catch { } }
};
const typeFilterKey = 'type-filter';
window.typeFilter = {
    load: () => { try { return localStorage.getItem(typeFilterKey); } catch { return null; } },
    save: value => { try { localStorage.setItem(typeFilterKey, value); } catch { } }
};
const homeViewKey = 'home-view';
window.homeView = {
    load: () => { try { return localStorage.getItem(homeViewKey); } catch { return null; } },
    save: value => { try { localStorage.setItem(homeViewKey, value); } catch { } }
};
