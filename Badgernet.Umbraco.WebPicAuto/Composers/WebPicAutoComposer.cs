﻿using Badgernet.WebPicAuto.Handlers;
using Badgernet.WebPicAuto.Settings;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;

namespace Badgernet.WebPicAuto.Composers
{
    internal class WebPicAutoComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            
            builder.AddNotificationHandler<MediaSavingNotification, WebPicAutoHandler>();
            builder.Services.AddSingleton<IWpaSettingsProvider>(x =>
            {
                var dir = System.Environment.CurrentDirectory;
                var settingsPath = dir + "/App_Plugins/Badgernet.Umbraco.WebPicAuto/Backoffice/WpaSettings.json"; 
                return new WpaSettingsProvider(settingsPath);
            });
        }



    }
}
