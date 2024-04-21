using Microsoft.Extensions.Hosting;
using SixLabors.ImageSharp;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;

namespace Badgernet.WebPicAuto.Helpers
{
    public interface IWebPicHelper
    {
        IMedia? GetMediaById(int id);
        string GetMediaPath(IMedia media);
        object? GetPropertyValue(IMedia media, string propName);
        void SetPropertyValue(IMedia media, string propName, object value);
        Size? ResizeImageFile(string sourcePath, string targetPath, Size sizeLimit, bool ignoreAspectRatio = false);
        bool ConvertImageFile(string sourcePath, string targetPath, string convertMode, int convertQuality);
        long FileSizeDiff(string referenceFilePath, string filePath);
        Size CalculateNewSize(Size originalSize, Size sizeLimit, bool ignoreAspectRatio = false);
        
    }


}
