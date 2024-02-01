using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Badgernet.WebPicAuto.Settings
{
    public class WpaSettings
    {
        public bool WpaEnableResizing { get; set; } = false;
        public bool WpaEnableConverting { get; set; } = false;
        public string WpaConvertMode { get; set; } = "lossy";
        public int WpaConvertQuality { get; set; } = 80;
        public bool WpaIgnoreAspectRatio { get; set; } = false;
        public int WpaTargetWidth { get; set; } = 1920;
        public int WpaTargetHeight { get; set; } = 1080;
        public bool WpaKeepOriginals { get; set; } = false;
        public string WpaIgnoreKeyword {get;set;} = "wpaignore";

        public int WpaResizerCounter { get; set; } = 0;

        public int WpaConverterCounter { get; set; } = 0;
        public long WpaBytesSavedResizing { get; set; } = 0;
        public long WpaBytesSavedConverting{ get; set; } = 0;

    }
}
