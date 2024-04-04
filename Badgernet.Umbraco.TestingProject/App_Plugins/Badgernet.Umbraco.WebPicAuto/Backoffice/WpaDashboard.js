function WebPicAutoController($scope, $http, umbRequestHelper) {
    let vm = this;
    let baseApiUrl = "backoffice/Api/WebPicAuto/";
    const RESIZE_IMAGES = 1;
    const CONVERT_IMAGES = 2;
    const RESIZE_CONVERT_IMAGES = 3;


    $scope.selectedImages = new Set();
    $scope.selectImage = function (element) {

        let overlay = element.children[0];

        if(!$scope.selectedImages.has(element.id)){
            $scope.selectedImages.add(element.id);

            if(overlay)
                overlay.classList.add("wpaSelectedImg");

        }
        else{
            $scope.selectedImages.delete(element.id);
        
            if (overlay)
                overlay.classList.remove("wpaSelectedImg");
        }
    }

    $scope.selectAllImages = function () {

        var elements = document.getElementsByClassName("wpaSelectableImg");
        var btn = document.getElementById("wpaSelectUnselectImages");

        var t = btn.innerHTML;

        //Unselect all images
        if (t == "Unselect all") {

            $scope.selectedImages.clear();//Remove 

            for (let i = 0; i < elements.length; i++) {
                elements[i].children[0].classList.remove("wpaSelectedImg");                
            }
            btn.innerHTML = "Select all";
        }
        //Select all images
        else if ( t == "Select all")  {

            for (let i = 0; i < elements.length; i++) {
                elements[i].children[0].classList.add("wpaSelectedImg");
                if (!$scope.selectedImages.has(elements[i].id)) {
                    $scope.selectedImages.add(elements[i].id);
                }
            }
            btn.innerHTML = "Unselect all";
        } 
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

        vm.processExistingImages = function (action) {

            switch (action) {
                case CONVERT_IMAGES:
                    alert("Convert");
                    break;
                case RESIZE_IMAGES:
                    alert("Resize");
                    break;
                case RESIZE_CONVERT_IMAGES:
                    alert("Both");
                    break;
                default:
                    alert(action);
            }

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