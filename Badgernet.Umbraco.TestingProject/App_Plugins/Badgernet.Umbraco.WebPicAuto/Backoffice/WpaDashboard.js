function WebPicAutoController($scope, $http, umbRequestHelper) {
    let vm = this;
    let baseApiUrl = "backoffice/Api/WebPicAuto/";
    let selectedImages = new Set();
    
    $scope.testFunc = function(){
        alert("test test");
    }
    
    $scope.selectImage = function (element){
        if(!selectedImages.has(element.id)){
            selectedImages.add(element.id);
            element.classList.add("wpaSelectedImg");

        }
        else{
            selectedImages.delete(element.id);
            element.classList.remove("wpaSelectedImg");
        }
    }

    $scope.selectAllImages = function (){
        var images = angular.element(element.getElementsByClassName("wpaSelectableImg"));
        
    }
    

    function init() {
        //Retrieves settings from server on load
        umbRequestHelper.resourcePromise($http.get(baseApiUrl + "GetSettings"))
            .then(function (data) {
                    vm.settings = JSON.parse(data);
                    vm.sizeSavedConverting = humanFileSize(vm.settings.WpaBytesSavedConverting);
                    vm.sizeSavedResizing = humanFileSize(vm.settings.WpaBytesSavedResizing);
                }
            );

        //Sends settings to server for saving
        vm.commit = function(){
            umbRequestHelper.resourcePromise(
                $http.post(baseApiUrl + "SetSettings",JSON.stringify(vm.settings))
            ).then(function (response){
                alert(response);
            });
        };

        vm.getAllImages = function (){
            umbRequestHelper.resourcePromise( $http.get(baseApiUrl + "GetAllImages") )
                .then(function(data){
                    vm.allImages = JSON.parse(data);
                });
        }
        
        vm.checkMedia = function (){
            umbRequestHelper.resourcePromise( $http.get(baseApiUrl + "CheckMedia"))
                .then(function (data){
                   vm.mediaCheckResults = JSON.parse(data);
                });
        }

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