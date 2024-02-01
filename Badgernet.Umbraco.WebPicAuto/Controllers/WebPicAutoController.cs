using Badgernet.WebPicAuto.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Authorization;

namespace Badgernet.WebPicAuto.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class WebPicAutoController : UmbracoAuthorizedJsonController
    {
        private readonly IWpaSettingsProvider _settingsProvider;
        private readonly ILogger<WebPicAutoController> _logger;
        private WpaSettings? _currentSettings;

        public WebPicAutoController(IWpaSettingsProvider settingsProvider, ILogger<WebPicAutoController> logger)
        {
            _settingsProvider = settingsProvider;
            _logger = logger;
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
    }
}