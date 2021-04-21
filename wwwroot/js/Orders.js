var dataTable;

$(document).ready(function () {
    //Khi trang web load xong thì dự vào status mà load
    var url = window.location.search; //lấy dịa chỉ trang web
    if (url.includes("inprocess")) {
        //dựa vào tham số cuối cùng mà truyền đường dẫn thích hợp vào hàm loadDataTable
        loadDataTable("GetOrderList?status=inprocess");
    } else if (url.includes("pending")) {
        loadDataTable("GetOrderList?status=pending");
    } else if (url.includes("completed")) {
        loadDataTable("GetOrderList?status=completed");
    } else if (url.includes("rejected")) {
        loadDataTable("GetOrderList?status=rejected");
    } else {
        loadDataTable("GetOrderList?status=all");
    }
});

function loadDataTable(url) {
    dataTable = $('#tblData').DataTable({
        "ajax": {
            "url": "/Admin/Orders/" + url
        },
        "columns": [
            { "data": "id", "width": "10%" },
            { "data": "name", "width": "15%" },
            { "data": "phoneNumber", "width": "15%" },
            { "data": "applicationUser.email", "width": "15%" },
            { "data": "orderStatus", "width": "15%" },
            { "data": "orderTotal", "width": "15%" },
            {
                "data": "id",
                "render": function (data) {
                    return `
                        <div class="text-center">
                            <a href="/Admin/Orders/Details/${data}" class="btn btn-success pointer-event text-white">
                                <i class="fas fa-edit"></i>
                            </a>
                        </div>
                    `;
                }, "width" : "5%"
            }
        ]
    });
}