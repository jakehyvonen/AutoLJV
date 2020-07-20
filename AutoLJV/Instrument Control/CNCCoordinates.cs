using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoLJV.Instrument_Control
{
    /// <summary>
    /// contains dictionaries relating gcode coordinates to physical pixel positions for various substrates
    /// </summary>
    public static class CNCCoordinates
    {
        public static Dictionary<string, string> StandardXinYanBTS1 = new Dictionary<string, string>
        {
            {"PixelA","G1 X6.3 Z2.5" },
            {"PixelB","G1 X0 Z5.5" },
            {"PixelC","G1 X3 Z12.0" },
            {"PixelD","G1 X9.3 Z8.8" }
        };
        public static Dictionary<string, string> NewVisionBTS1 = new Dictionary<string, string>
        {
           {"PixelA", @"G1 X6.6 Z4.4" },
            {"PixelB", @"G1 X1.4 Z5.0" },
            {"PixelC", @"G1 X1.4 Z10.5" },
            {"PixelD", @"G1 X7.0 Z10.5" }
        };
        public static Dictionary<string, string> StandardXinYanBTS2 = new Dictionary<string, string>
        {
             {"PixelA","G1 X12.3 Z27.6" },
            {"PixelB","G1 X4.4 Z30.6" },
            {"PixelC","G1 X8.2 Z37.3" },
            {"PixelD","G1 X16.3 Z34.1" }
        };
        public static Dictionary<string, string> NewVisionBTS2 = new Dictionary<string, string>
        {
           {"PixelA", @"G1 X13 Z30" },
            {"PixelB", @"G1 X5.8 Z30.3" },
            {"PixelC", @"G1 X6.9 Z36" },
            {"PixelD", @"G1 X14 Z36" }
        };
        public static Dictionary<string, Dictionary<string, string>> BTS1Coords = new Dictionary<string, Dictionary<string, string>>
        {
            {"XinYan",StandardXinYanBTS1 },
            {"NewVision",NewVisionBTS1 }
        };
        public static Dictionary<string, Dictionary<string, string>> BTS2Coords = new Dictionary<string, Dictionary<string, string>>
        {
            {"XinYan",StandardXinYanBTS2 },
            {"NewVision",NewVisionBTS2 }
        };
    }
}
