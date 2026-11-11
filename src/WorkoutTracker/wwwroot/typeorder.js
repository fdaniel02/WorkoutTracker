const typeOrderKey = 'type-order';
window.typeOrder = {
    load: () => { try { return localStorage.getItem(typeOrderKey); } catch { return null; } },
    save: value => { try { localStorage.setItem(typeOrderKey, value); } catch { } }
};
const homeViewKey = 'home-view';
window.homeView = {
    load: () => { try { return localStorage.getItem(homeViewKey); } catch { return null; } },
    save: value => { try { localStorage.setItem(homeViewKey, value); } catch { } }
};
