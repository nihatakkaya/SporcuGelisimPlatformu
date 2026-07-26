(() => {
    const formatPhone = value => {
        const digits = value.replace(/\D/g, "").slice(0, 11);
        const parts = [
            digits.slice(0, 4),
            digits.slice(4, 7),
            digits.slice(7, 9),
            digits.slice(9, 11)
        ].filter(Boolean);
        return parts.join(" ");
    };

    document.addEventListener("input", event => {
        const input = event.target;
        if (!(input instanceof HTMLInputElement) || !input.matches("[data-phone-mask]")) {
            return;
        }

        input.value = formatPhone(input.value);
    });
})();
