// Board view preference helpers — persists "table" | "kanban" in localStorage
window.boardPrefs = {
    getView: function () {
        return localStorage.getItem('agentboard-view');
    },
    setView: function (view) {
        localStorage.setItem('agentboard-view', view);
    }
};
