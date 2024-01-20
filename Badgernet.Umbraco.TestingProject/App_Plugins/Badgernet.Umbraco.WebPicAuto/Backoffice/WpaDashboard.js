function WebPicAutoController($scope, $http, umbRequestHelper) {
    let vm = this;
    let baseApiUrl = "backoffice/Api/WebPicAuto/";

    function init() {
        umbRequestHelper.resourcePromise(
            $http.get(baseApiUrl + "GetSettings")
        ).then(function (data) {
            vm.settings = JSON.parse(data);
        });

        $scope.commit = function(){
            umbRequestHelper.resourcePromise(
                $http.post(baseApiUrl + "SetSettings",JSON.stringify(vm.settings))
            ).then(function (){
                alert("Success")
            });
        }
    }
    init();
    

}

angular.module("umbraco").controller("Badgernet.Umbraco.WebPicAutoController", WebPicAutoController);