namespace HtmlMinifyMiddleware.Tests
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Owin;
    using Xunit;
    using Xunit.Abstractions;

    public class HtmlMinifyTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public HtmlMinifyTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Blah()
        {
            // 1 Block copy array into buffer
            // 2 Memory stream read
            // 3 Move trailing bytes to front
            // 4 Block copy array into buffer repeat

            string s = new string('\u03D6', 2);
            int bufferLength = 4096;

            var encoding = Encoding.UTF8;
            var bytes = encoding.GetBytes(s);
            _testOutputHelper.WriteLine(bytes.Length.ToString());

            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length-1);
            _testOutputHelper.WriteLine(arraySegment.Count.ToString());

            var memoryStream = new MemoryStream(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
            using(var reader = new StreamReader(memoryStream, Encoding.UTF8))
            {
                var read = reader.Read();
                _testOutputHelper.WriteLine(read.ToString());
                _testOutputHelper.WriteLine("MS POS " + memoryStream.Position);

                /*char c = (char) read;
                _testOutputHelper.WriteLine(c.ToString());*/

                read = reader.Read();
                _testOutputHelper.WriteLine(read.ToString());
                _testOutputHelper.WriteLine("MS POS " + memoryStream.Position);

                read = reader.Read();
                _testOutputHelper.WriteLine(read.ToString());
                _testOutputHelper.WriteLine("MS POS " + memoryStream.Position);
            }

            _testOutputHelper.WriteLine(s);
        }

        [Fact]
        public async Task Should_minify_html()
        {
            var s = new string(' ', 10000);
            string html = "   <html>" + Environment.NewLine + s + "</html>";
            
            var appfunc = HtmlMinify.Middleware(async env =>
            {
                var context = new OwinContext(env);
                context.Response.ContentType = "text/html";
                var bytes = Encoding.UTF8.GetBytes(html);
                await context.Response.WriteAsync(bytes);
            });
            
            var handler = new OwinHttpMessageHandler(appfunc);
            using(var client = new HttpClient(handler))
            {
                var response = await client.GetAsync("http://localhost/");
                var body = await response.Content.ReadAsStringAsync();

                body.Should().Be(@"<html> </html>");
            }
        }

        [Fact]
        public async Task Should_minify_html_less_than_buffer_size()
        {
            var s = new string(' ', 10);
            string html = "   <html>" + Environment.NewLine + s + "</html>";

            var appfunc = HtmlMinify.Middleware(async env =>
            {
                var context = new OwinContext(env);
                context.Response.ContentType = "text/html";
                var bytes = Encoding.UTF8.GetBytes(html);
                await context.Response.WriteAsync(bytes);
            });

            var handler = new OwinHttpMessageHandler(appfunc);
            using (var client = new HttpClient(handler))
            {
                var response = await client.GetAsync("http://localhost/");
                var body = await response.Content.ReadAsStringAsync();

                body.Should().Be(@"<html> </html>");
            }
        }

        [Fact]
        public async Task Should_not_minify_plain_text()
        {
            string html = @"   <html>
                            </html>";

            var appfunc = HtmlMinify.Middleware(async env =>
            {
                var context = new OwinContext(env);
                context.Response.ContentType = "text/plain";
                var bytes = Encoding.UTF8.GetBytes(html);
                await context.Response.WriteAsync(bytes);
            });

            var handler = new OwinHttpMessageHandler(appfunc);
            using (var client = new HttpClient(handler))
            {
                var response = await client.GetAsync("http://localhost/");
                var body = await response.Content.ReadAsStringAsync();

                body.Should().Be(html);
            }
        }
    }
}