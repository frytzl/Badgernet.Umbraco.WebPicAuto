function WebPicAutoController($scope, $http, umbRequestHelper) {
    let vm = this;
    let baseApiUrl = "backoffice/Api/WebPicAuto/";

    function init() {


        umbRequestHelper.resourcePromise(
            $http.get(baseApiUrl + "GetSettings")
        ).then(function (data) {
            vm.settings = JSON.parse(data);
            vm.sizeSavedConverting = humanFileSize(vm.settings.WpaBytesSavedConverting);
            vm.sizeSavedResizing = humanFileSize(vm.settings.WpaBytesSavedResizing);
        });

        vm.commit = function(){
            umbRequestHelper.resourcePromise(
                $http.post(baseApiUrl + "SetSettings",JSON.stringify(vm.settings))
            ).then(function (response){
                alert(response);
            });
        };
        
        vm.toggle = function (propName){
            let currentValue = vm.settings[propName];
            vm.settings[propName] = !currentValue;
        };
        
        $scope.setProperty = function (propName, value){
            vm.settings[propName] = value;
        };

        function humanFileSize(bytes, si=false, dp=1) {
            const thresh = si ? 1000 : 1024;
            
            if (Math.abs(bytes) < thresh) {
                return bytes + ' B';
            }
            
            const units = si
                ? ['kB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB']
                : ['KiB', 'MiB', 'GiB', 'TiB', 'PiB', 'EiB', 'ZiB', 'YiB'];
            
            let u = -1;
            const r = 10**dp;
            
            do {
                bytes /= thresh;
                ++u;
            } while (Math.round(Math.abs(bytes) * r) / r >= thresh && u < units.length - 1);
            return bytes.toFixed(dp) + ' ' + units[u];
        }

    }
    init();
    


}

angular.module("umbraco").controller("Badgernet.Umbraco.WebPicAutoController", WebPicAutoController);