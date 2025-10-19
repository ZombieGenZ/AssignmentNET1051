(function (window, document) {
    function getElements(container, selector) {
        return Array.prototype.slice.call(container.querySelectorAll(selector));
    }

    function initContainer(container) {
        if (!container || container.dataset.bulkInitialized === 'true') {
            return;
        }

        const formId = container.dataset.bulkForm;
        const form = formId ? document.getElementById(formId) : container.querySelector('form');

        if (!form) {
            container.dataset.bulkInitialized = 'true';
            return;
        }

        const selectAll = container.querySelector('[data-bulk-select-all]');
        const actionBar = container.querySelector('[data-bulk-actions]');
        const countEl = container.querySelector('[data-bulk-count]');
        const enableTargets = getElements(container, '[data-bulk-enable]');
        const emptyMessage = container.dataset.bulkEmptyMessage || 'Vui lòng chọn ít nhất một mục.';
        const confirmTemplate = form.dataset.confirm || container.dataset.bulkConfirm || '';

        function selectedItems() {
            return getElements(container, '[data-bulk-item]:checked');
        }

        function allItems() {
            return getElements(container, '[data-bulk-item]');
        }

        function updateState() {
            const all = allItems();
            const selected = selectedItems();
            const count = selected.length;

            if (countEl) {
                countEl.textContent = count.toString();
            }

            if (actionBar) {
                actionBar.classList.toggle('d-none', count === 0);
            }

            enableTargets.forEach(target => {
                target.disabled = count === 0;
            });

            if (selectAll) {
                selectAll.disabled = all.length === 0;
                selectAll.checked = count > 0 && count === all.length;
                if ('indeterminate' in selectAll) {
                    selectAll.indeterminate = count > 0 && count < all.length;
                }
            }
        }

        container.addEventListener('change', function (event) {
            const target = event.target;
            if (target.matches('[data-bulk-select-all]')) {
                const items = allItems();
                items.forEach(item => {
                    item.checked = target.checked;
                });
                updateState();
            } else if (target.matches('[data-bulk-item]')) {
                updateState();
            }
        });

        form.addEventListener('submit', function (event) {
            const selected = selectedItems();
            const count = selected.length;

            if (count === 0) {
                event.preventDefault();
                if (emptyMessage) {
                    window.alert(emptyMessage);
                }
                return;
            }

            const submitter = event.submitter;
            let messageTemplate = confirmTemplate;

            if (submitter && submitter.dataset && submitter.dataset.bulkConfirm) {
                messageTemplate = submitter.dataset.bulkConfirm;
            }

            if (!messageTemplate && container.dataset.bulkConfirm) {
                messageTemplate = container.dataset.bulkConfirm;
            }

            if (messageTemplate) {
                const message = messageTemplate.replace('{count}', count.toString());
                if (!window.confirm(message)) {
                    event.preventDefault();
                    return;
                }
            }
        });

        updateState();
        container.dataset.bulkInitialized = 'true';
    }

    function initAll() {
        getElements(document, '[data-bulk-container]').forEach(initContainer);
    }

    window.AdminBulkActions = {
        init: initAll,
        initContainer: initContainer
    };

    document.addEventListener('DOMContentLoaded', initAll);
})(window, document);
