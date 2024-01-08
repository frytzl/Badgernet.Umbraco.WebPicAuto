using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Badgernet.Umbraco.WebPicAuto.Settings
{
    public class WebPicAutoSettings
    {
        public bool WpaEnableResizing { get; set; } = false;
        public bool WpaEnableConverting { get; set; } = false;
        public WpaConvertMode WpaConvertMode { get; set; } = WpaConvertMode.Lossy;
        public int WpaConvertQuality { get; set; } = 80;
        public int WpaTargetWidth { get; set; } = 1920;
        public int WpaTargetHeight { get; set; } = 1080;
        public bool WpaKeepOriginals { get; set; } = false;
    }

    public enum WpaConvertMode
    {
        Lossy,
        Lossles
    }
}
