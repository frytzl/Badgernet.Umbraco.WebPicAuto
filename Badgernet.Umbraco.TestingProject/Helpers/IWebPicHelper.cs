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
        string GetFullPath(IMedia media);
        object? GetValue(IMedia media, string propName);
        void SetValue(IMedia media, string propName, object value);
        void ChangeExtention(IMedia media, string extention);
        void ChangeFilename(IMedia media, string filename);
        Size? ResizeImageFile(string sourcePath, string targetPath, Size sizeLimit, bool ignoreAspectRatio = false);
        bool ConvertImageFile(string sourcePath, string targetPath, string convertMode, int convertQuality);
        long FileSizeDiff(string referenceFilePath, string filePath);
        Size CalculateNewSize(Size originalSize, Size sizeLimit, bool ignoreAspectRatio = false);
        string GenerateAlternativePath(IMedia media);
        
    }


}
