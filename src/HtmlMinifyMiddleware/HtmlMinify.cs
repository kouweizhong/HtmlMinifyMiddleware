

namespace HtmlMinifyMiddleware
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Owin;
    using MidFunc =
        System.Func<System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>
            , System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>>;

    public static class HtmlMinify
    {
        public static MidFunc Middleware
        {
            get
            {
                return next => env =>
                {
                    var context = new OwinContext(env);

                    context.Response.Body = new HtmlMinifyStream(context.Response.Body, () => context.Response.ContentType);

                    return next(env);
                };
            }
        }
    }

    internal class HtmlMinifyStream : Stream
    {
        private readonly Stream _inner;
        private readonly Lazy<Action<byte[], int, int>> _lazyWrite;
        private const char EmptySpace = ' ';

        public HtmlMinifyStream(Stream inner, Func<string> getContentType)
        {
            _inner = inner;

            _lazyWrite = new Lazy<Action<byte[], int, int>>(() =>
            {
                if (!string.Equals(getContentType(), "text/html", StringComparison.OrdinalIgnoreCase))
                {
                    return _inner.Write;
                }

                var inputBuffer = new byte[4096];
                var memoryStream = new MemoryStream(inputBuffer); // Should be pooled
                int inputBufferOffset = 0;
                var outputCharBuffer = new char[Encoding.UTF8.GetMaxCharCount(4096)];
                var encoding = Encoding.UTF8;
                int charPosition = 0;

                //buffer length should not be greater than 4096
                return ((buffer, offset, count) =>
                {
                    Buffer.BlockCopy(buffer, offset, inputBuffer, inputBufferOffset, count);

                    using (var reader = new StreamReader(memoryStream, encoding, true, 4096, true))
                    {
                        char previous = EmptySpace;
                        int read = reader.Read();

                        while(read >= 0)
                        {
                            var c = (char) read;
                            if (c == EmptySpace && (charPosition == 0 || previous == EmptySpace))
                            {
                                previous = c;
                            }
                            else if(c == '\r' || c == '\n')
                            {
                                previous = EmptySpace;
                            }
                            else
                            {
                                outputCharBuffer[charPosition] = c;
                                previous = c;
                                charPosition++;
                            }
                            read = reader.Read();
                        }
                    }

                    int remainingBytes = inputBuffer.Length - (int)memoryStream.Position;
                    Buffer.BlockCopy(inputBuffer, (int)memoryStream.Position, inputBuffer, 0, remainingBytes);

                    var outputBytes = encoding.GetBytes(outputCharBuffer, 0, charPosition);
                    charPosition = 0;
                    _inner.Write(outputBytes, 0, outputBytes.Length);
                });
            });
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int pageSize = 4096;

            while (count > pageSize)
            {
                _lazyWrite.Value(buffer, offset, pageSize);
                count -= pageSize;
                offset += pageSize;
            }
            _lazyWrite.Value(buffer, offset, count);
        }

        public override bool CanRead
        {
            get { return _inner.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _inner.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _inner.CanWrite; }
        }

        public override long Length
        {
            get { return _inner.Length; }
        }

        public override long Position
        {
            get { return _inner.Position; }
            set { _inner.Position = value; }
        }
    }
}