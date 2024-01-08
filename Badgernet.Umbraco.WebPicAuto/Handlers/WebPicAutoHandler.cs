using Badgernet.Umbraco.WebPicAuto.Helpers;
using Badgernet.Umbraco.WebPicAuto.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;
using File = System.IO.File;
using Umbraco.Cms.Core.Scoping;
using System.Text.Json.Nodes;




namespace Badgernet.Umbraco.WebPicAuto.Handlers
{
    public class WebPicAutoHandler(IWebHostEnvironment hostEnvironment, 
                                   IOptions<WebPicAutoSettings> options,
                                   IMediaService mediaService,
                                   IScopeProvider scopeProvider,
                                   MediaUrlGeneratorCollection mediaUrlGenerator) : INotificationAsyncHandler<MediaSavingNotification>
    {
        public Task HandleAsync(MediaSavingNotification notification, CancellationToken cancellationToken)
        {
            bool resizingEnabled = options.Value.WpaEnableResizing;
            bool convertingEnabled = options.Value.WpaEnableConverting;
            int convertQuality = options.Value.WpaConvertQuality;
            int targetWidth = options.Value.WpaTargetWidth;
            int targetHeight = options.Value.WpaTargetHeight;
            bool keepOriginals = options.Value.WpaKeepOriginals;
            WpaConvertMode convertMode = options.Value.WpaConvertMode;

            //Prevent Options being out of bounds 
            if (targetHeight < 1) targetHeight = 1;
            if (targetWidth < 1) targetWidth = 1;
            if (convertQuality < 1) convertQuality = 1;
            if (convertQuality > 100) convertQuality = 100;


            foreach(var media in notification.SavedEntities)
            {
                string originalPath = GetMediaPath(media);
                string targetPath = CreateNewPath(originalPath);

                if (media == null) continue;
                if (string.IsNullOrEmpty(media.ContentType.Alias) || !media.ContentType.Alias.Equals("image", StringComparison.CurrentCultureIgnoreCase)) continue; //Skip if not an image 
                if (string.IsNullOrEmpty(originalPath) || string.IsNullOrEmpty(targetPath)) continue; //Skip if paths not good
                if (media.Id > 0) continue; //Skip any not-new images

                Size originalSize = new();
                try
                {
                    originalSize.Width = int.Parse(media.GetValue<string>("umbracoWidth"));
                    originalSize.Height = int.Parse(media.GetValue<string>("umbracoHeight"));
                }
                catch
                {
                    continue; //Skip if dimension cannot be parsed 
                }

                using var scope = scopeProvider.CreateCoreScope(autoComplete: true);
                using var _ = scope.Notifications.Suppress();

                var needsResizing = originalSize.Width > targetWidth || originalSize.Height > targetHeight;
                if(needsResizing && resizingEnabled)
                {
                    var targetSize = new Size(targetWidth, targetHeight);
                    if(ResizeImage(originalPath, targetPath, targetSize ))
                    {
                        if(!keepOriginals)
                        {
                            if(TryDeleteFile(originalPath))
                            {
                                var jsonProperty = media.GetValue<string>("umbracoFile");
                                if(jsonProperty != null)
                                {
                                    var jsonNode = JsonNode.Parse(jsonProperty);
                                    string pathValue = jsonNode["src"].GetValue<string>();
                                    jsonNode["src"] = Pa;
                                }

                                media.SetValue("umbracoWidth", targetWidth);
                                media.SetValue("umbracoHeight", targetHeight);

                                
                               
                            }
                        }
                    }
                }

                if(convertingEnabled)
                {

                }

            }
        }

        private static bool ResizeImage(string filePath, string targetPath, Size targetSize)
        {
            try
            {
                using var img = Image.Load(filePath);

                img.Mutate(img =>
                {
                    img.Resize(targetSize.Width, 0);
                    img.Resize(0, targetSize.Height);
                    img.AutoOrient();
                });

                img.Save(targetPath);

                return true;
            }
            catch
            {
                return false;
            }
        }
        private string CreateNewPath(string filePath)
        {
            var newPath = string.Empty;

            var retry = 1;
            FileInfo fileInfo = new(filePath);
            string? folderPath = fileInfo.DirectoryName;
            var fileExtension = fileInfo.Extension;
            string fullFileName = fileInfo.Name;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullFileName);

            do
            {
                newPath = $"{folderPath}\\{fileNameWithoutExtension}_processed_{retry}{fileExtension}";
                retry++;
            }
            while (System.IO.File.Exists(newPath));

            return newPath;
        }
        private string GetMediaPath(IMedia media)
        {
            var mediaPath = media.GetUrl("umbracoFile", mediaUrlGenerator);
            var webRootPath = hostEnvironment.WebRootPath.Replace("\\", "/");

            if (string.IsNullOrEmpty(mediaPath) || string.IsNullOrEmpty(webRootPath)) return string.Empty;

            return webRootPath + mediaPath;

        }
        private static bool TryDeleteFile(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName); 
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

         


    }
}
