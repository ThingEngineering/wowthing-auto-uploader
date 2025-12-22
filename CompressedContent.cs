using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace WowthingAutoUploader;

// https://programmer.help/blogs/httpclient-and-aps.net-web-api-compression-and-decompression-of-request-content.html
public class CompressedContent : HttpContent
{
    private readonly HttpContent _originalContent;

    public CompressedContent(HttpContent content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        _originalContent = content;

        foreach (var header in _originalContent.Headers)
        {
            Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        Headers.ContentEncoding.Add("gzip");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        using (var gzipStream = new GZipStream(stream, CompressionMode.Compress, true))
        {
            await _originalContent.CopyToAsync(gzipStream);
        }
    }
}
