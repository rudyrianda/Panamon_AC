document.addEventListener('DOMContentLoaded', function () {

    // ── PASSWORD TOGGLE ──
    const passwordFields = document.querySelectorAll('input[type="password"]');
    passwordFields.forEach(field => {
        // Wrap input dalam div relative supaya icon posisinya relatif ke input
        const wrapper = document.createElement('div');
        wrapper.style.position = 'relative';
        field.parentElement.insertBefore(wrapper, field);
        wrapper.appendChild(field);

        const toggleContainer = document.createElement('div');
        toggleContainer.className = 'password-toggle';
        toggleContainer.style.position = 'absolute';
        toggleContainer.style.right = '12px';
        toggleContainer.style.top = '50%';
        toggleContainer.style.transform = 'translateY(-50%)';
        toggleContainer.style.cursor = 'pointer';
        toggleContainer.style.zIndex = '5';

        const eyeIcon = document.createElement('i');
        eyeIcon.className = 'bi bi-eye';
        eyeIcon.style.fontSize = '1.2rem';
        toggleContainer.appendChild(eyeIcon);
        wrapper.appendChild(toggleContainer);

        toggleContainer.addEventListener('click', function () {
            if (field.type === 'password') {
                field.type = 'text';
                eyeIcon.className = 'bi bi-eye-slash';
            } else {
                field.type = 'password';
                eyeIcon.className = 'bi bi-eye';
            }
        });
    });

    // ── FORM SUBMIT — cek jQuery Unobtrusive Validation dulu ──
    const forms = document.querySelectorAll('form');
    forms.forEach(form => {
        form.addEventListener('submit', function () {
            // Cek apakah jQuery validation aktif dan form valid
            const $form = typeof $ !== 'undefined' ? $(form) : null;
            const isValid = $form && $form.validate
                ? $form.validate().form()
                : form.checkValidity();

            if (!isValid) return; // Ada error validasi, jangan disable tombol

            const submitButton = form.querySelector('button[type="submit"]');
            if (submitButton) {
                submitButton.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Processing...';
                submitButton.disabled = true;
            }
        });
    });

});