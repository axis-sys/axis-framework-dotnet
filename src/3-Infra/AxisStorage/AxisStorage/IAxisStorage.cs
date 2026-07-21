namespace Axis;

public interface IAxisStorage
{
    Task<AxisResult> UploadAsync(string key, Stream content, string contentType);

    Task<AxisResult<Stream>> DownloadAsync(string key);

    Task<AxisResult> DeleteAsync(string key);

    Task<AxisResult<bool>> ExistsAsync(string key);

    Task<AxisResult<string>> GetPresignedUrlAsync(string key, TimeSpan expiration);
}
