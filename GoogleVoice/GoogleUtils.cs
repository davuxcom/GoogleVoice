using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace GoogleVoice
{
    class GoogleUtils
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 
                                                              DateTimeKind.Utc);

        public static DateTime UnixTimeToDateTime(string text)
        {
            // FIXME Google gives us timestamps like 1321121029995, I don't know
            // what this extended format is called, not a unix ts
            text = text.Substring(0, 10);
            double seconds = double.Parse(text, CultureInfo.InvariantCulture);
            return Epoch.AddSeconds(seconds);
        }

    }
}
