using Badgernet.Umbraco.WebPicAuto.Handlers;
using Badgernet.Umbraco.WebPicAuto.Settings;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Badgernet.Umbraco.WebPicAuto.Composers
{
    internal class WebPicAutoComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.AddNotificationHandler<MediaSavingNotification, WebPicAutoHandler>();
            builder.Services.Configure<WebPicAutoSettings>(builder.Config.GetSection("WebPicAuto"));
            builder.Services.AddSingleton(typeof(WebPicAutoSettings));
        }
    }
}
