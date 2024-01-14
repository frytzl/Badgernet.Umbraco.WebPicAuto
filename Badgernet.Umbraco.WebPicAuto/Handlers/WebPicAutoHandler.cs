using Badgernet.Umbraco.WebPicAuto.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
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
using SixLabors.ImageSharp.Formats.Webp;




namespace Badgernet.Umbraco.WebPicAuto.Handlers
{
    public class WebPicAutoHandler(IWebHostEnvironment hostEnvironment, 
                                   IOptions<WebPicAutoSettings> options,
                                   ICoreScopeProvider scopeProvider,
                                   MediaUrlGeneratorCollection mediaUrlGenerator) : INotificationHandler<MediaSavingNotification>
    {
        public void Handle(MediaSavingNotification notification)
        {
            bool resizingEnabled = options.Value.WpaEnableResizing;
            bool convertingEnabled = options.Value.WpaEnableConverting;
            int convertQuality = options.Value.WpaConvertQuality;
            bool ignoreAspectRatio = options.Value.WpaIgnoreAspectRatio;
            int targetWidth = options.Value.WpaTargetWidth;
            int targetHeight = options.Value.WpaTargetHeight;
            bool keepOriginals = options.Value.WpaKeepOriginals;
            string convertMode = options.Value.WpaConvertMode;
            string ignoreKeyword = options.Value.WpaIgnoreKeyword;

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


                using var scope = scopeProvider.CreateCoreScope(autoComplete: true);
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

            }
        }

        /// <summary>
        /// Resizes Image to fit into targetSize mantaining aspect ratio.
        /// </summary>
        /// <param name="sourcePath">Path to imagefile to be converted</param>
        /// <param name="targetPath">Path where to save converted image</param>
        /// <param name="targetSize">Size box for image to be fit into</param>
        /// <returns>Image size after resizing if successfull, null if resizing failed</returns>
        private static Size? ResizeImage(string sourcePath, string targetPath, Size targetSize, bool ignoreAspectRatio)
        {
            try
            {
                using var img = Image.Load(sourcePath);

                var newSize = CalculateNewSize(img.Size, targetSize, ignoreAspectRatio);

                img.Mutate(img =>
                {
                    img.Resize(newSize.Width,newSize.Height);
                });

                img.Mutate(x => x.AutoOrient());

                img.Save(targetPath);

                return img.Size;
            }
            catch
            {
                return null;
            }
        }


        private static Size CalculateNewSize(Size currentSize, Size targetSize, bool ignoreAspectRatio )
        {
            var newWidth = currentSize.Width;
            var newHeight = currentSize.Height;

            if (currentSize.Width > targetSize.Width || currentSize.Height > targetSize.Height)
            {
                if(ignoreAspectRatio)
                {
                    return targetSize;
                }

                double ratio = (double)currentSize.Width / (double)currentSize.Height;

                if (ratio > 1)
                {
                    newWidth = targetSize.Width;
                    newHeight = (int)(newWidth / ratio);
                    if (newHeight > targetSize.Height)
                    {
                        newHeight = targetSize.Height;
                        newWidth = (int)(newHeight * ratio);
                    }
                }
                else
                {
                    newHeight = targetSize.Height;
                    newWidth = (int)(newHeight * ratio);

                    if (newWidth > targetSize.Width)
                    {
                        newHeight = (int)(newWidth / ratio);
                        newWidth = targetSize.Width;
                    }
                }
            }
            return new Size(newWidth,newHeight);
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


            newPath = newPath.Replace("\\","/"); 
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

        private Size? GetTargetFromFileName(string fileName)
        {
            if (!fileName.StartsWith("wparesize_")) return null;
            if (fileName.Length < 11) return null;

            try
            {
                var size = new Size(int.MaxValue, int.MaxValue);

                var buffer = string.Empty;
                for (int i = 10; i < fileName.Length; i++)
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
            catch 
            {
                return null;
            }

            
        }
    }
}
