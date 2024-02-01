using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Umbraco.Cms.Core.Hosting;

namespace Badgernet.WebPicAuto.Settings;

public interface IWpaSettingsProvider
{
    WpaSettings GetSettings();
    bool PersistToFile(WpaSettings settings);
}

public class WpaSettingsProvider : IWpaSettingsProvider
{
    private readonly string _settingsPath;
    public WpaSettingsProvider(string filePath)
    {
        _settingsPath = filePath;
    } 
    
    public WpaSettings GetSettings()
    {
        var jsonString = string.Empty;
        var fileLock = new object();

        lock (fileLock)
        {
            try
            {
                using var fStream = File.Open(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var streamReader = new StreamReader(fStream,Encoding.UTF8);
                jsonString = streamReader.ReadToEnd();
            }
            catch
            {
                //LOG
            }
        }
        
        WpaSettings settings;
        try
        {
            settings = JsonSerializer.Deserialize<WpaSettings>(jsonString);
        }
        catch
        {
            return new WpaSettings(); //Return Defaults 
        }
        
        return settings;

    }

    public bool PersistToFile(WpaSettings settings)
    {
        var fileLock = new object();
        var jsonString = JsonSerializer.Serialize(settings);

        lock (fileLock)
        {
            try
            {
                using var fStream = File.Open(_settingsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var streamWriter = new StreamWriter(fStream, Encoding.UTF8);
                streamWriter.Write(jsonString);
                return true;
            }
            catch 
            {
                //LOG 
                return false;
            }
        }
    }
}