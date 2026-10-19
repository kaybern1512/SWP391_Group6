using System;
using System.IO;
using System.Net.Http;

namespace DentalClinic.Web.Models.Api;

public class ApiFileStreamResult : IDisposable
{
    public Stream? Stream { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string? FileName { get; set; }
    public int StatusCode { get; set; } = 200;
    public HttpResponseMessage? ResponseMessage { get; set; }

    public void Dispose()
    {
        Stream?.Dispose();
        ResponseMessage?.Dispose();
    }
}
