using System;
using System.IO;

namespace DentalClinic.Application.Common.Models;

public class StorageFileStreamResult : IDisposable
{
    public Stream Stream { get; set; } = null!;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "imaging";
    public long? FileLength { get; set; }

    public void Dispose()
    {
        Stream?.Dispose();
    }
}
