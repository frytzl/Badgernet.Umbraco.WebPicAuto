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


namespace Badgernet.WebPicAuto.Handlers
{
    public class WebPicAutoHandler : INotificationHandler<MediaSavingNotification>
    {
        private WpaSettings? _settings;
        private readonly IWpaSettingsProvider _settingsProvider;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ICoreScopeProvider _scopeProvider;
        private readonly ILogger<WebPicAutoHandler> _logger;
        
        private readonly MediaUrlGeneratorCollection _mediaUrlGeneratorCollection;

        public WebPicAutoHandler(IWebHostEnvironment hostEnvironment, 
                                 IWpaSettingsProvider wpaSettingsProvider,
                                 ICoreScopeProvider scopeProvider,
                                 ILogger<WebPicAutoHandler> logger,
                                 MediaUrlGeneratorCollection mediaUrlGenerator)
        {
            _settingsProvider = wpaSettingsProvider;
            _hostEnvironment = hostEnvironment;
            _scopeProvider = scopeProvider;
            _mediaUrlGeneratorCollection = mediaUrlGenerator;
            _logger = logger;
        }
        public void Handle(MediaSavingNotification notification)
        {
            _settings = _settingsProvider.GetSettings();
            
            bool resizingEnabled = _settings.WpaEnableResizing;
            bool convertingEnabled = _settings.WpaEnableConverting;
            int convertQuality = _settings.WpaConvertQuality;
            bool ignoreAspectRatio = _settings.WpaIgnoreAspectRatio;
            int targetWidth = _settings.WpaTargetWidth;
            int targetHeight = _settings.WpaTargetHeight;
            bool keepOriginals = _settings.WpaKeepOriginals;
            string convertMode = _settings.WpaConvertMode;
            string ignoreKeyword = _settings.WpaIgnoreKeyword;

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
                    var newSize = ResizeImage(originalFilePath, processedFilePath, new Size(targetWidth, targetHeight), ignoreAspectRatio);
                    if(newSize != null)
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

                        mediaEntity.SetValue("umbracoWidth", newSize.Value.Width);
                        mediaEntity.SetValue("umbracoHeight", newSize.Value.Height);

                        wasResized = true;
                    }
                }

                //Image converting part
                if(convertingEnabled && !originalFilePath.ToLower().EndsWith(".webp"))
                {
                    var sourceFilePath = string.Empty;
                    var tempFilePath = string.Empty;

                    sourceFilePath = wasResized ? processedFilePath : originalFilePath;

                    tempFilePath = processedFilePath;
                    processedFilePath = Path.ChangeExtension(processedFilePath, ".webp");

                    if(ConvertImage(sourceFilePath, processedFilePath, convertMode, convertQuality))
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
                    }
                }  
                
                //Deleting original files
                if(!keepOriginals)
                {
                    TryDeleteFile(originalFilePath);
                }
                
                
            }

            //Write settings to file to preserve saved bytes values   
            _settingsProvider.PersistToFile(_settings);
        }

        /// <summary>
        /// Resizes Image to fit into targetSize maintaining aspect ratio.
        /// </summary>
        /// <param name="sourcePath">Path to image-file to be converted</param>
        /// <param name="targetPath">Path where to save converted image</param>
        /// <param name="targetSize">Size box for image to be fit into</param>
        /// <param name="ignoreAspectRatio">If true image will always keep its aspect ratio</param>
        /// <returns>Image size after resizing if successful, null if resizing failed</returns>
        private Size? ResizeImage(string sourcePath, string targetPath, Size targetSize, bool ignoreAspectRatio)
        {
            try
            {
                using var img = Image.Load(sourcePath);
                var newSize = CalculateNewSize(img.Size, targetSize, ignoreAspectRatio);

                img.Mutate(x =>
                {
                    x.Resize(newSize.Width,newSize.Height);
                });
                img.Mutate(x => x.AutoOrient());
                img.Save(targetPath);


                //Calculate and save file size diff
                if (File.Exists(sourcePath) && File.Exists(targetPath))
                {
                    FileInfo sourceImg = new(sourcePath);
                    FileInfo resizedImg = new(targetPath);
                    _settings!.WpaBytesSavedResizing += sourceImg.Length - resizedImg.Length;
                    _settings!.WpaResizerCounter++;
                }
                
                return img.Size;
            }
            catch(Exception e)
            {
                _logger.LogError("WebPicAuto: error resizing image: {0}", e.Message);
                return null;
            }
        }


        private static Size CalculateNewSize(Size currentSize, Size targetSize, bool ignoreAspectRatio )
        {
            var newWidth = currentSize.Width;
            var newHeight = currentSize.Height;

            //Upscaling not intended -> return original size
            if (currentSize.Width <= targetSize.Width && currentSize.Height <= targetSize.Height)
                return new Size(newWidth, newHeight);
            
            if(ignoreAspectRatio)
            {
                return targetSize;
            }

            var aspectRatio = (double)currentSize.Width / (double)currentSize.Height;

            if (aspectRatio > 1)
            {
                newWidth = targetSize.Width;
                newHeight = (int)(newWidth / aspectRatio);
                if (newHeight > targetSize.Height)
                {
                    newHeight = targetSize.Height;
                    newWidth = (int)(newHeight * aspectRatio);
                }
            }
            else
            {
                newHeight = targetSize.Height;
                newWidth = (int)(newHeight * aspectRatio);

                if (newWidth > targetSize.Width)
                {
                    newHeight = (int)(newWidth / aspectRatio);
                    newWidth = targetSize.Width;
                }
            }
            return new Size(newWidth,newHeight);
        }

        private bool ConvertImage(string sourcePath, string targetPath, string convertMode, int convertQuality)
        {
            var encoder = convertMode switch
            {
                "lossy" => new WebpEncoder()
                {
                    Quality = convertQuality, 
                    FileFormat = WebpFileFormatType.Lossy
                },
                "lossless" => new WebpEncoder()
                {
                    Quality = convertQuality,
                    FileFormat = WebpFileFormatType.Lossless
                },
                _ => new WebpEncoder() 
                    { 
                        Quality = convertQuality, 
                        FileFormat = WebpFileFormatType.Lossy 
                    }
            };

            try
            {
                var img = Image.Load(sourcePath);
                img.Save(targetPath, encoder!);

                //Calculate file size diff and save it
                if (File.Exists(sourcePath) && File.Exists(targetPath))
                {
                    FileInfo originalImg = new(sourcePath);
                    FileInfo convertedImg= new(targetPath);
                    _settings!.WpaBytesSavedConverting += originalImg.Length - convertedImg.Length;
                    _settings!.WpaConverterCounter++;
                }

                return true;
            }
            catch(Exception e) 
            {
                _logger.LogError("WebPicAuto: error converting image: {0}", e.Message);
                return false;
            }
        }
        private static string CreateNewPath(string filePath, out string generatedSuffix)
        {
            string newPath;

            var retry = 1;
            FileInfo fileInfo = new(filePath);
            var folderPath = fileInfo.DirectoryName;
            var fileExtension = fileInfo.Extension;
            var fullFileName = fileInfo.Name;
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullFileName);

            do
            {
                generatedSuffix = $"_processed_{retry}";
                newPath = $"{folderPath}\\{fileNameWithoutExtension}{generatedSuffix}{fileExtension}";
                retry++;
            }
            while (File.Exists(newPath));


            newPath = newPath.Replace("\\","/"); 
            return newPath;
        }
        private string GetMediaPath(IMedia media)
        {
            var mediaPath = media.GetUrl("umbracoFile", _mediaUrlGeneratorCollection);
            var webRootPath = _hostEnvironment.WebRootPath.Replace("\\", "/");

            if (string.IsNullOrEmpty(mediaPath) || string.IsNullOrEmpty(webRootPath)) return string.Empty;

            return webRootPath + mediaPath;

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
