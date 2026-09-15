// ClinicQ shared client scripts.
(function ($) {
    'use strict';

    // Any table marked .js-datatable becomes a DataTable with sensible defaults.
    // data-order="[[1,&quot;desc&quot;]]" and data-page-length="25" can override per table.
    $(function () {
        $('table.js-datatable').each(function () {
            var $t = $(this);
            $t.DataTable({
                pageLength: $t.data('page-length') || 25,
                order: $t.data('order') || [],
                language: { search: 'Filter:' }
            });
        });

        // Confirm destructive or state-changing actions.
        $(document).on('submit', 'form[data-confirm]', function (e) {
            if (!window.confirm($(this).data('confirm'))) {
                e.preventDefault();
            }
        });
    });
})(jQuery);
