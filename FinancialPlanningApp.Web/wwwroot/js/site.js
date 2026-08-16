// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
(() => {
    const updateModalElement = document.getElementById("updateAvailableModal");
    if (!updateModalElement || !window.bootstrap || updateModalElement.dataset.updateAutoshow !== "true") {
        return;
    }

    const storageKey = updateModalElement.dataset.updateStorageKey || "financial-planning:update-modal";
    try {
        if (window.sessionStorage.getItem(storageKey) === "shown") {
            return;
        }

        window.sessionStorage.setItem(storageKey, "shown");
    } catch {
        // If session storage is blocked, still show the update notification.
    }

    const modal = new bootstrap.Modal(updateModalElement);
    modal.show();
})();

(() => {
    let pendingForm = null;
    let pendingSubmitter = null;
    let bypassConfirm = false;

    const modalElement = document.getElementById("confirmActionModal");
    const titleElement = document.getElementById("confirmActionModalTitle");
    const bodyElement = document.getElementById("confirmActionModalBody");
    const confirmButton = document.getElementById("confirmActionModalConfirm");

    if (!modalElement || !titleElement || !bodyElement || !confirmButton || !window.bootstrap) {
        return;
    }

    const modal = new bootstrap.Modal(modalElement);

    document.addEventListener("submit", event => {
        if (bypassConfirm) {
            bypassConfirm = false;
            return;
        }

        const form = event.target;
        const submitter = event.submitter;
        const message = submitter?.dataset.confirmMessage || form.dataset.confirmMessage;
        if (!message) {
            return;
        }

        event.preventDefault();
        pendingForm = form;
        pendingSubmitter = submitter;

        titleElement.textContent = submitter?.dataset.confirmTitle || form.dataset.confirmTitle || "Confirm action";
        bodyElement.textContent = message;
        confirmButton.className = submitter?.dataset.confirmButtonClass || form.dataset.confirmButtonClass || "btn btn-danger";
        confirmButton.textContent = submitter?.dataset.confirmButtonText || form.dataset.confirmButtonText || "Confirm";
        modal.show();
    });

    confirmButton.addEventListener("click", () => {
        if (!pendingForm) {
            modal.hide();
            return;
        }

        const form = pendingForm;
        const submitter = pendingSubmitter;
        pendingForm = null;
        pendingSubmitter = null;
        bypassConfirm = true;
        modal.hide();

        if (submitter) {
            form.requestSubmit(submitter);
        } else {
            form.requestSubmit();
        }
    });

    modalElement.addEventListener("hidden.bs.modal", () => {
        pendingForm = null;
        pendingSubmitter = null;
    });
})();
