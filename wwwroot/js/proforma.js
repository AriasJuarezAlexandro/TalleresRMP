// Grilla dinámica de productos de la proforma (Create / Edit).
// Se auto-inicializa cuando existe la tabla #tablaProductos en la página.
(function () {
    function init() {
        const tbody = document.getElementById('cuerpoProductos');
        const plantillaEl = document.getElementById('plantillaFila');
        const btnAgregar = document.getElementById('btnAgregarFila');
        if (!tbody || !plantillaEl || !btnAgregar) return;

        const plantilla = plantillaEl.innerHTML;
        // Las filas existentes usan índices 0..n-1; el siguiente key arranca en n.
        let nextKey = tbody.querySelectorAll('.fila-producto').length;

        function recalcularFila(fila) {
            const cant = parseFloat(fila.querySelector('.js-cant').value) || 0;
            const precio = parseFloat(fila.querySelector('.js-precio').value) || 0;
            fila.querySelector('.js-importe').value = (cant * precio).toFixed(2);
        }

        function renumerarItems() {
            tbody.querySelectorAll('.fila-producto').forEach(function (fila, idx) {
                const item = fila.querySelector('.js-item');
                if (item) item.value = idx + 1;
            });
        }

        function recalcularTotal() {
            let total = 0;
            tbody.querySelectorAll('.js-importe').forEach(function (i) {
                total += parseFloat(i.value) || 0;
            });
            const totalEl = document.getElementById('totalGeneral');
            if (totalEl) totalEl.value = total.toFixed(2);
        }

        function enlazarFila(fila) {
            fila.querySelectorAll('.js-cant, .js-precio').forEach(function (input) {
                input.addEventListener('input', function () {
                    recalcularFila(fila);
                    recalcularTotal();
                });
            });
            const quitar = fila.querySelector('.js-quitar');
            if (quitar) {
                quitar.addEventListener('click', function () {
                    fila.remove();
                    renumerarItems();
                    recalcularTotal();
                });
            }
        }

        btnAgregar.addEventListener('click', function () {
            const html = plantilla.replace(/__KEY__/g, nextKey++);
            const tmp = document.createElement('tbody');
            tmp.innerHTML = html.trim();
            const fila = tmp.firstElementChild;
            tbody.appendChild(fila);
            enlazarFila(fila);
            renumerarItems();
        });

        // Enlazar filas ya presentes (caso Edit), numerar y calcular el total inicial.
        tbody.querySelectorAll('.fila-producto').forEach(enlazarFila);
        renumerarItems();
        recalcularTotal();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
