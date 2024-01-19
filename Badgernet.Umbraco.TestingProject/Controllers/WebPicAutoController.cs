using System.Collections.Generic;
using System.Linq;
using Badgernet.Umbraco.WebPicAuto.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Authorization;

namespace Badgernet.Umbraco.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class WebPicAutoController : UmbracoAuthorizedJsonController
    {
        private readonly WebPicAutoSettings? _settings;

        public WebPicAutoController(IConfiguration configuration)
        {
            _settings = configuration.GetSection("WebPicAuto").Get<WebPicAutoSettings>();
           
        }

        public string GetSettings()
        {
            if(_settings == null)
            {
                return JsonConvert.SerializeObject(new WebPicAutoSettings()); //Return default values
            }
            return JsonConvert.SerializeObject(_settings);
        }



    }
}