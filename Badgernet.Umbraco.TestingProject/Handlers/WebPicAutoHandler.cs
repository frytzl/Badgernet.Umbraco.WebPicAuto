using Badgernet.WebPicAuto.Settings;
using Microsoft.AspNetCore.Hosting;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;
using File = System.IO.File;
using Umbraco.Cms.Core.Scoping;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.Formats.Webp;
using Badgernet.WebPicAuto.Helpers;


namespace Badgernet.WebPicAuto.Handlers
{
    public class WebPicAutoHandler : INotificationHandler<MediaSavingNotification>
    {
        private readonly IWpaSettingsProvider _settingsProvider;
        private readonly IWebPicHelper _webPicHelper;
        private readonly ICoreScopeProvider _scopeProvider;
        private readonly ILogger<WebPicAutoHandler> _logger;
        
        public WebPicAutoHandler(IWpaSettingsProvider wpaSettingsProvider,
                                 IWebPicHelper webPicHelper,
                                 ICoreScopeProvider scopeProvider,
                                 ILogger<WebPicAutoHandler> logger)
        {
            _settingsProvider = wpaSettingsProvider;
            _scopeProvider = scopeProvider;
            _webPicHelper = webPicHelper;
            _logger = logger;
        }
        public void Handle(MediaSavingNotification notification)
        {
            var wpaSettings = _settingsProvider.GetFromFile();
            
            bool resizingEnabled = wpaSettings.WpaEnableResizing;
            bool convertingEnabled = wpaSettings.WpaEnableConverting;
            int convertQuality = wpaSettings.WpaConvertQuality;
            bool ignoreAspectRatio = wpaSettings.WpaIgnoreAspectRatio;
            int targetWidth = wpaSettings.WpaTargetWidth;
            int targetHeight = wpaSettings.WpaTargetHeight;
            bool keepOriginals = wpaSettings.WpaKeepOriginals;
            string convertMode = wpaSettings.WpaConvertMode;
            string ignoreKeyword = wpaSettings.WpaIgnoreKeyword;

            //Prevent Options being out of bounds 
            if (targetHeight < 1) targetHeight = 1;
            if (targetWidth < 1) targetWidth = 1;
            if (convertQuality < 1) convertQuality = 1;
            if (convertQuality > 100) convertQuality = 100;


            foreach(var mediaEntity in notification.SavedEntities)
            {
                if (mediaEntity == null) continue;
                if (string.IsNullOrEmpty(mediaEntity.ContentType.Alias) || !mediaEntity.ContentType.Alias.Equals("image", StringComparison.CurrentCultureIgnoreCase)) continue; //Skip if not an image 
                
                string generatedFileNameSuffix = string.Empty;
                string originalFilePath = _webPicHelper.GetMediaPath(mediaEntity);
                string processedFilePath = CreateNewPath(originalFilePath, out generatedFileNameSuffix);
                Size originalSize = new();


                if (string.IsNullOrEmpty(originalFilePath) || string.IsNullOrEmpty(processedFilePath)) continue; //Skip if paths not good
                if (mediaEntity.Id > 0) continue; //Skip any not-new images

                if (Path.GetFileNameWithoutExtension(originalFilePath).Contains(ignoreKeyword,StringComparison.CurrentCultureIgnoreCase)) 
                {
                    processedFilePath = originalFilePath.Replace(ignoreKeyword, string.Empty);
                    File.Move(originalFilePath, processedFilePath, true);

                    var jsonString = mediaEntity.GetValue<string>("umbracoFile");

                    if (jsonString == null) continue;

                    var propNode = JsonNode.Parse((string)jsonString);
                    string? path = propNode!["src"]!.GetValue<string>();
                    path = path.Replace(ignoreKeyword, string.Empty);

                    propNode["src"] = path;

                    mediaEntity.SetValue("umbracoFile", propNode.ToJsonString());
                    if(mediaEntity.Name != null)
                    {
                        mediaEntity.Name = mediaEntity.Name.Replace(ignoreKeyword, string.Empty,StringComparison.CurrentCultureIgnoreCase);
                    }

                    continue;
                }
                
                using var scope = _scopeProvider.CreateCoreScope(autoComplete: true);
                using var _ = scope.Notifications.Suppress();

                try
                {
                    var widthValue = mediaEntity.GetValue<string>("umbracoWidth");
                    var heightValue = mediaEntity.GetValue<string>("umbracoHeight");

                    if (widthValue != null && heightValue != null)
                    {
                        originalSize.Width = int.Parse(widthValue);
                        originalSize.Height = int.Parse(heightValue);
                    }
                }
                catch
                {
                    continue; //Skip if dimensions cannot be parsed 
                }

                //Override appsettings targetSize if provided in image filename
                var parsedTargetSize = GetTargetFromFileName(Path.GetFileNameWithoutExtension(originalFilePath));
                if(parsedTargetSize != null)
                {
                    targetWidth = parsedTargetSize.Value.Width;
                    targetHeight = parsedTargetSize.Value.Height;
                }

                //Image resizing part
                var wasResized = false;
                var needsResizing = originalSize.Width > targetWidth || originalSize.Height > targetHeight;
                if(needsResizing && resizingEnabled)
                {
                    var newSize = _webPicHelper.ResizeImageFile(originalFilePath, processedFilePath, new Size(targetWidth, targetHeight), ignoreAspectRatio);

                    if(newSize != null)
                    {
                        //Calculate file size difference
                        var bytesSaved = _webPicHelper.FileSizeDiff(originalFilePath, processedFilePath);
                        wpaSettings.WpaBytesSavedResizing += bytesSaved;
                        wpaSettings.WpaResizerCounter++;


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

                        mediaEntity.SetValue("umbracoWidth", newSize.Value.Width);
                        mediaEntity.SetValue("umbracoHeight", newSize.Value.Height);

                        wasResized = true;
                    }
                }

                //Image converting part
                var wasConverted = false;
                if(convertingEnabled && !originalFilePath.ToLower().EndsWith(".webp"))
                {
                    var sourceFilePath = string.Empty;
                    var tempFilePath = string.Empty;

                    sourceFilePath = wasResized ? processedFilePath : originalFilePath;

                    tempFilePath = processedFilePath;
                    processedFilePath = Path.ChangeExtension(processedFilePath, ".webp");

                    if(_webPicHelper.ConvertImageFile(sourceFilePath, processedFilePath, convertMode, convertQuality))
                    {

                        TryDeleteFile(tempFilePath);
                        var jsonString = mediaEntity.GetValue("umbracoFile");

                        if (jsonString != null)
                        {
                            var propNode = JsonNode.Parse((string)jsonString);
                            var path = propNode!["src"]!.GetValue<string>();

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
                
                        FileInfo targetImg = new(processedFilePath);
                        mediaEntity.SetValue("umbracoExtension", "webp");
                        mediaEntity.SetValue("umbracoBytes", targetImg.Length);

                        wasConverted = true;
                    }
                }
                
                //Deleting original files
                if(!keepOriginals && wasResized || wasConverted)
                {
                    TryDeleteFile(originalFilePath);
                }
            }

            //Write settings to file to preserve saved bytes values   
            _settingsProvider.PersistToFile(wpaSettings);
        }
  
        private static string CreateNewPath(string filePath, out string generatedSuffix)
        {
            string newPath;

            var retry = 1;
            FileInfo fileInfo = new(filePath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);

            do
            {
                generatedSuffix = $"_processed_{retry}";
                newPath = $"{fileInfo.DirectoryName}\\{fileNameWithoutExtension}{generatedSuffix}{fileInfo.Extension}";
                retry++;
            }
            while (File.Exists(newPath));

            //Return cleaned up path 
            return newPath.Replace("\\", "/");
        }
        private bool TryDeleteFile(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName); 
                }

                return true;
            }
            catch(Exception e)
            {
                _logger.LogError("WebPicAuto: error while deleting file: {0}", e.Message);
                return false;
            }
        }
        private Size? GetTargetFromFileName(string fileName)
        {
            if (!fileName.StartsWith("wparesize_")) return null;
            if (fileName.Length < 11) return null;

            try
            {
                var size = new Size(int.MaxValue, int.MaxValue);

                var buffer = string.Empty;
                for (var i = 10; i < fileName.Length; i++)
                {
                    if (fileName[i] == '_')
                    {
                        if (size.Width == int.MaxValue)
                        {
                            size.Width = int.Parse(buffer);
                            buffer = string.Empty;
                        }
                        else
                        {
                            size.Height = int.Parse(buffer);
                            return size;
                        }
                    }
                    else
                    {
                        buffer += fileName[i];
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError("WebPicAuto: error: {0}", e.Message);
                return null;
            }
        }
    }
}
