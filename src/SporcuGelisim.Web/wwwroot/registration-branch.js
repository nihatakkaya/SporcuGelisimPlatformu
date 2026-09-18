(() => {
    const sync = () => {
        const role = document.querySelector('[data-registration-role]');
        const branch = document.querySelector('[data-registration-branch]');
        if (!role || !branch) return;
        const required = role.value === role.dataset.athleteRole;
        branch.required = required;
        branch.setAttribute('aria-required', String(required));
        document.querySelector('[data-branch-required]').hidden = !required;
        branch.setCustomValidity(required && !branch.value ? 'Sporcu kaydı oluşturmak için branş seçmelisiniz.' : '');
    };
    document.addEventListener('change', event => {
        if (event.target.matches('[data-registration-role], [data-registration-branch]')) sync();
    });
    document.addEventListener('DOMContentLoaded', sync);
    Blazor.addEventListener('enhancedload', sync);
    sync();
})();
