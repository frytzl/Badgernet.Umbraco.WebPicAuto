using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Badgernet.Umbraco.WebPicAuto.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Authorization;

namespace Badgernet.Umbraco.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class WebPicAutoController : UmbracoAuthorizedJsonController
    {
        private readonly WebPicAutoSettings? _settings;
        private IConfiguration? _config;  

        public WebPicAutoController(IConfiguration configuration)
        {
            _config = configuration;
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

        public string SetSettings(JObject payload)
        {
            try
            {
                var settings = payload.ToObject<WebPicAutoSettings>();

                
                var a = 1;
                return "Success";
            }
            catch(Exception e)
            {
                return "Data not accepted!";
            }
        }
    }
}