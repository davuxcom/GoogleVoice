using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GoogleVoice
{
    public class GVLoginException : ApplicationException
    {
        public GVLoginException(string message) : base(message) {}
    }
}
