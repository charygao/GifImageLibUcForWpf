using System.IO;
using System.Net;

namespace GifImageLib
{
    class WebReadState
    {
        public WebRequest   WebRequest;
        public MemoryStream MemoryStream;
        public Stream       ReadStream;
        public byte[]       Buffer;
    }
}