using Badgernet.WebPicAuto.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
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
        private readonly IWpaSettingsProvider _settingsProvider;
        private readonly ILogger<WebPicAutoController> _logger;
        private readonly IUmbracoContextAccessor _contextAccessor;
        private WpaSettings? _currentSettings;

        public WebPicAutoController(IWpaSettingsProvider settingsProvider, ILogger<WebPicAutoController> logger, IUmbracoContextAccessor contextAccessor)
        {
            _settingsProvider = settingsProvider;
            _logger = logger;
            _contextAccessor = contextAccessor;
        }

        public string GetSettings()
        {
            _currentSettings ??= _settingsProvider.GetSettings();
            return JsonConvert.SerializeObject(_currentSettings);
        }

        public string SetSettings(JObject settingsJson)
        {
            try
            {
                _currentSettings = settingsJson.ToObject<WpaSettings>();
                _settingsProvider.PersistToFile(_currentSettings!);
                
                return "Settings were saved.";
            }
            catch(Exception e)
            {
                _logger.LogError("Error when saving wpa settings: {0}", e.Message);
                return "Something went wrong, check logs.";
            }
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
        
        public string CheckMedia()
        {
            var allImages = GetAllImages();
            if (allImages == null) return string.Empty;

            _currentSettings ??= _settingsProvider.GetSettings();

            var optimizeCandidates = allImages.Where(img =>
                    (img.Width > _currentSettings.WpaTargetWidth || img.Height > _currentSettings.WpaTargetHeight) ||
                    (img.Extension != "webp" && img.Extension != "svg"))
                    .Select(img => img.Path);

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
