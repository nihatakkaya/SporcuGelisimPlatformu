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
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        if (input.matches("[data-phone-mask]")) {
            input.value = formatPhone(input.value);
            return;
        }

        if (input.matches("[data-picker-search]")) {
            filterPicker(input);
        }
    });

    document.addEventListener("click", event => {
        const button = event.target;
        if (!(button instanceof HTMLButtonElement) || !button.matches("[data-picker-search-button]")) {
            return;
        }

        const input = button.parentElement?.querySelector("[data-picker-search]");
        if (input instanceof HTMLInputElement) {
            filterPicker(input);
        }
    });

    const filterPicker = input => {
        const targetSelector = input.getAttribute("data-search-target");
        if (!targetSelector) {
            return;
        }

        const list = document.querySelector(targetSelector);
        if (!list) {
            return;
        }

        const query = input.value.trim().toLocaleLowerCase("tr-TR");
        list.querySelectorAll("[data-search-text]").forEach(row => {
            const text = (row.getAttribute("data-search-text") || row.textContent || "").toLocaleLowerCase("tr-TR");
            row.hidden = query.length > 0 && !text.includes(query);
        });
    };
})();
