using Badgernet.Umbraco.WebPicAuto.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;
using File = System.IO.File;
using Umbraco.Cms.Core.Scoping;
using System.Text.Json.Nodes;
using SixLabors.ImageSharp.Formats.Webp;




namespace Badgernet.Umbraco.WebPicAuto.Handlers
{
    public class WebPicAutoHandler(IWebHostEnvironment hostEnvironment, 
                                   IOptions<WebPicAutoSettings> options,
                                   IMediaService mediaService,
                                   ICoreScopeProvider scopeProvider,
                                   MediaUrlGeneratorCollection mediaUrlGenerator) : INotificationHandler<MediaSavingNotification>
    {
        public void Handle(MediaSavingNotification notification)
        {
            bool resizingEnabled = options.Value.WpaEnableResizing;
            bool convertingEnabled = options.Value.WpaEnableConverting;
            int convertQuality = options.Value.WpaConvertQuality;
            int targetWidth = options.Value.WpaTargetWidth;
            int targetHeight = options.Value.WpaTargetHeight;
            bool keepOriginals = options.Value.WpaKeepOriginals;
            string convertMode = options.Value.WpaConvertMode;

            //Prevent Options being out of bounds 
            if (targetHeight < 1) targetHeight = 1;
            if (targetWidth < 1) targetWidth = 1;
            if (convertQuality < 1) convertQuality = 1;
            if (convertQuality > 100) convertQuality = 100;


            foreach(var mediaEntity in notification.SavedEntities)
            {
                string generatedFileNameSuffix = string.Empty;
                string originalFilePath = GetMediaPath(mediaEntity);
                string processedFilePath = CreateNewPath(originalFilePath, out generatedFileNameSuffix);
                Size originalSize = new();

                if (mediaEntity == null) continue;
                if (string.IsNullOrEmpty(mediaEntity.ContentType.Alias) || !mediaEntity.ContentType.Alias.Equals("image", StringComparison.CurrentCultureIgnoreCase)) continue; //Skip if not an image 
                if (string.IsNullOrEmpty(originalFilePath) || string.IsNullOrEmpty(processedFilePath)) continue; //Skip if paths not good
                if (mediaEntity.Id > 0) continue; //Skip any not-new images

                try
                {
                    var widthValue = mediaEntity.GetValue<string>("umbracoWidth");
                    var heightValue = mediaEntity.GetValue<string>("umbracoHeight");

                    if(widthValue != null && heightValue != null)
                    {
                        originalSize.Width = int.Parse(widthValue);
                        originalSize.Height = int.Parse(heightValue);
                    }
                }
                catch
                {
                    continue; //Skip if dimensions cannot be parsed 
                }

                using var scope = scopeProvider.CreateCoreScope(autoComplete: true);
                using var _ = scope.Notifications.Suppress();



                //Image resizing part
                var wasResized = false;
                var needsResizing = originalSize.Width > targetWidth || originalSize.Height > targetHeight;
                if(needsResizing && resizingEnabled)
                {
                    if(ResizeImage(originalFilePath, processedFilePath, new Size(targetWidth, targetHeight)))
                    {
                        var imagePathJson = mediaEntity.GetValue<string>("umbracoFile");
                        if (imagePathJson != null)
                        {
                            for (int i = imagePathJson.Length - 1; i >= 0; i--)
                            {
                                if (imagePathJson[i] == '.')
                                {
                                    imagePathJson = imagePathJson.Insert(i, generatedFileNameSuffix);
                                    break;
                                }
                            }
                            mediaEntity.SetValue("umbracoFile", imagePathJson);
                        }

                        mediaEntity.SetValue("umbracoWidth", targetWidth);
                        mediaEntity.SetValue("umbracoHeight", targetHeight);

                        wasResized = true;
                    }
                }

                //Image converting part
                if(convertingEnabled && !originalFilePath.ToLower().EndsWith(".webp"))
                {
                    var sourceFilePath = string.Empty;
                    var tempFilePath = string.Empty;

                    if(wasResized)
                    {
                        sourceFilePath = processedFilePath;
                    }
                    else
                    {
                        sourceFilePath = originalFilePath;
                    }


                    tempFilePath = processedFilePath;
                    processedFilePath = Path.ChangeExtension(processedFilePath, ".webp");


                    if(ConvertImage(sourceFilePath, processedFilePath, convertMode, convertQuality))
                    {

                        TryDeleteFile(tempFilePath);

                        var jsonString = mediaEntity.GetValue("umbracoFile");

                        if (jsonString != null)
                        {
                            var propNode = JsonNode.Parse((string)jsonString);
                            string? path = propNode!["src"]!.GetValue<string>();
                            
                            if(!wasResized)
                            {
                                var dirPath = Path.GetDirectoryName(path);
                                var fileName = Path.GetFileNameWithoutExtension(path);
                                fileName += generatedFileNameSuffix + ".webp";
                                fileName = Path.Combine(dirPath, fileName);
                                fileName = fileName.Replace('\\', '/');
                                propNode["src"] = fileName;
                            }
                            else
                            {
                                propNode["src"] = Path.ChangeExtension(path, ".webp");
                            }

                            mediaEntity.SetValue("umbracoFile", propNode.ToJsonString());
                        }

                        mediaEntity.SetValue("umbracoExtension", "webp");
                        FileInfo imgInfo = new(processedFilePath);
                        mediaEntity.SetValue("umbracoBytes", imgInfo.Length);
                    }
                }  
                
                //Deleting original files
                if(!keepOriginals)
                {
                    TryDeleteFile(originalFilePath);
                }

                mediaService.Save(mediaEntity);

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
        private static bool ConvertImage(string sourcePath, string targetPath, string convertMode, int convertQuality)
        {
            WebpEncoder? encoder; 
            
            switch (convertMode) 
            {
                case "lossy":
                {
                    encoder = new WebpEncoder()
                    {
                        Quality = convertQuality,
                        FileFormat = WebpFileFormatType.Lossy
                    };
                    break;
                }
                   
                case "lossless":
                {
                    encoder = new WebpEncoder()
                    {
                        Quality = convertQuality,
                        FileFormat = WebpFileFormatType.Lossless
                    };
                    break;
                }
                default:
                    {
                        encoder = new WebpEncoder()
                        {
                            Quality = convertQuality,
                            FileFormat = WebpFileFormatType.Lossy
                        };
                        break;
                    }
            }

            try
            {
                var img = Image.Load(sourcePath);
                img.Save(targetPath, encoder!);

                return true;
            }
            catch 
            {
                return false;
            }

        }
        private static string CreateNewPath(string filePath, out string generatedSuffix)
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
                generatedSuffix = $"_processed_{retry}";
                newPath = $"{folderPath}\\{fileNameWithoutExtension}{generatedSuffix}{fileExtension}";
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
