function WebPicAutoController($scope, $http, umbRequestHelper) {
    let vm = this;
    let baseApiUrl = "backoffice/Api/WebPicAuto/";

     

    function init() {
        umbRequestHelper.resourcePromise(
            $http.get(baseApiUrl + "GetSettings")
        ).then(function (data) {
            vm.settings = JSON.parse(data);

            vm.options = [
                { name: "Carrot", value: "orange" },
                { name: "Cucumber", value: "green" },
                { name: "Aubergine", value: "purple" },
                { name: "Blueberry", value: "Blue" },
                { name: "Banana", value: "yellow" },
                { name: "Strawberry", value: "red" }
            ];
        });
    }

    init();
}





angular.module("umbraco").controller("Badgernet.Umbraco.WebPicAutoController", WebPicAutoController);