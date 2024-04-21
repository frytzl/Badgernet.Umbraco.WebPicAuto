using Badgernet.WebPicAuto.Helpers;
using Badgernet.WebPicAuto.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using System.Runtime;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Authorization;
using uSync.Core;

namespace Badgernet.WebPicAuto.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class WebPicAutoController : UmbracoAuthorizedJsonController
    {


        private readonly IWebPicHelper _mediaHelper;
        private readonly IWebPicSettingProvider _settingsProvider;
        private readonly ILogger<WebPicAutoController> _logger;
        private readonly IUmbracoContextAccessor _contextAccessor;
        private WebPicSettings? _currentSettings;


        public WebPicAutoController(IWebPicSettingProvider settingsProvider, 
                                    ILogger<WebPicAutoController> logger, 
                                    IUmbracoContextAccessor contextAccessor, 
                                    IWebPicHelper mediaHelper)
        {
            _mediaHelper = mediaHelper;
            _settingsProvider = settingsProvider;
            _logger = logger;
            _contextAccessor = contextAccessor;

        }

        public string GetSettings()
        {
            _currentSettings ??= _settingsProvider.GetFromFile();
            return JsonConvert.SerializeObject(_currentSettings);
        }

        public string SetSettings(JObject settingsJson)
        {
            try
            {
                _currentSettings = settingsJson.ToObject<WebPicSettings>();
                _settingsProvider.PersistToFile(_currentSettings!);
                
                return "Settings were saved.";
            }
            catch(Exception e)
            {
                _logger.LogError("Error when saving wpa settings: {0}", e.Message);
                return "Something went wrong, check logs.";
            }
        }

        public string CheckMedia()
        {
            var allImages = GetAllImages();
            if (allImages == null) return string.Empty;

            _currentSettings ??= _settingsProvider.GetFromFile();

            var optimizeCandidates = allImages.Where(img =>
                    (img.Width > _currentSettings.WpaTargetWidth || img.Height > _currentSettings.WpaTargetHeight) ||
                    (img.Extension != "webp" && img.Extension != "svg"))
                    .Select(img => new { path = img.Path, id = img.Id });

            var toResizeCount = allImages.Count(img =>
                img.Width > _currentSettings.WpaTargetWidth || img.Height > _currentSettings.WpaTargetHeight);

            var toConvertCount = allImages.Count(img =>
                    img.Extension != "webp" && img.Extension != "svg");

            var result = new
            {
                toConvertCount = toConvertCount,
                toResizeCount = toResizeCount,
                optimizeCandidates = optimizeCandidates
            };

            return JsonConvert.SerializeObject(result);
        }

        [HttpPost]
        public string ProcessExistingImages(JObject requestJson)
        {

            if (requestJson == null) return "No data recieved";
            if (!requestJson.ContainsKey("ids") || !requestJson.ContainsKey("mode")) return "Bad Request";

            var imageIds = requestJson.Value<int[]>("ids");
            var processMode = requestJson.Value<string>("mode");

            if (imageIds == null || imageIds.Length == 0) return "You have to select some images first";

            //Read current WebPic Settings from file. 
            var wpaSettings = _settingsProvider.GetFromFile();

            foreach (var id in imageIds)
            {

                var image = _mediaHelper.GetMediaById(id);
                if (image == null)
                {
                    _logger.LogError($"Could not find media with id: {id}");
                    continue;
                }

                var imagePath = _mediaHelper.GetMediaPath(image);
               

                switch (processMode)
                {
                    case "OnlyConvert":
                        if(_mediaHelper.ConvertImageFile(imagePath, //imagePath, wpaSettings.WpaConvertMode, wpaSettings.WpaConvertQuality))
                        {
                            return "We did some converting.";
                        }

                        return "Something went wrong";

                    case "OnlyResize":
                        return "We did some resizing.";

                    case "ResizeAndConvert":
                        return "We did some work.";

                    default:
                        return $"Dont know what to do with mode: {processMode}";
                }

                

            }

            return "All done.";
        } 

        private IEnumerable<ImageInfo>? GetAllImages()
        {
            if (_contextAccessor .TryGetUmbracoContext(out var context) == false)
                return null;
      
            if (context.Content == null)
                return null;

            var mediaRoot = context.Media!.GetAtRoot();

            var images = mediaRoot.DescendantsOrSelf<IPublishedContent>()
                .OfTypes("Image")
                .Select(i => new ImageInfo()
                {
                    Id =i.Id,
                    File = (ImageCropperValue?) (i.GetProperty("UmbracoFile")?.GetValue() ?? null),
                    Width = (int) (i.GetProperty("UmbracoWidth")?.GetValue() ?? 0),
                    Height = (int) (i.GetProperty("UmbracoHeight")?.GetValue() ?? 0),
                    Extension = (string) (i.GetProperty("UmbracoExtension")?.GetValue() ?? string.Empty)
                });

            return images;
        }
        

    }


    public class ImageSelection
    {
        public int[] Ids { get; set; }
        public string Mode { get; set; }
    }
    public class ImageInfo()
    {
        public int Id { get; init; }
        public ImageCropperValue? File { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public string Extension { get; init; }
        public string? Path => File?.Src;
    }

}
