var dataTable;

//khi trang CoverTypes/Index được gọi
$(document).ready(function () {
    loadDataTable();
});

function loadDataTable() {
    dataTable = $('#tblData').DataTable({
        "ajax": {
            "url": "/Admin/CoverTypes/GetAll",
        },
        "columns": [
            { "data": "name", "width": "60%" },
            {
                "data": "id",
                "render": function (data) {
                    return `
                        <div class="text-center">
                            <a href="/Admin/CoverTypes/Upsert/${data}" class="btn btn-success pointer-event text-white">
                                <i class="fas fa-edit"></i>
                            </a>
                            <a onclick=Delete("/Admin/CoverTypes/Delete/${data}") class="btn btn-danger pointer-event text-white">
                                <i class="fas fa-trash-alt"></i>
                            </a>
                        </div>
                    `;
                }, "width" : "40%"
            }
        ]
    });
}

function Delete(url) {
    swal({
        title: "Are you sure you want to delete?",
        data: "Once delete, you will not able to restore the data",
        icon: "warning",
        buttons: true,
        dangerMode: true
    }).then((willDelete) => {
        if (willDelete) {
            $.ajax({
                type: "DELETE",
                url: url,
                success: function (data) {
                    if (data.success) {
                        toastr.success(data.message);
                        dataTable.ajax.reload();
                    } else {
                        toastr.error(data.message);
                    }
                }
            });
        }
    });
}