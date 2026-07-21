using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Axis;
using AxisMediator.Contracts;

namespace AxisStorage.CloudflareR2;

internal sealed class CloudflareR2StorageAdapter(IAxisMediatorAccessor accessor, IAmazonS3 s3Client, CloudflareR2Settings settings)
    : IAxisStorage, IAxisStorageContainer, IAxisStorageLister, IAxisStorageUrlResolver
{
    public Task<AxisResult> UploadAsync(string key, Stream content, string contentType)
        => AxisResult.TryAsync(async () =>
        {
            var ct = accessor.AxisMediator!.CancellationToken;
            ct.ThrowIfCancellationRequested();
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = settings.BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType
            }, ct);
        });

    public Task<AxisResult<Stream>> DownloadAsync(string key)
        => AxisResult.TryAsync(async () =>
        {
            var ct = accessor.AxisMediator!.CancellationToken;
            ct.ThrowIfCancellationRequested();
            var response = await s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = settings.BucketName,
                Key = key
            }, ct);
            return response.ResponseStream;
        });

    public Task<AxisResult> DeleteAsync(string key)
        => AxisResult.TryAsync(async () =>
        {
            var ct = accessor.AxisMediator!.CancellationToken;
            ct.ThrowIfCancellationRequested();
            await s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = settings.BucketName,
                Key = key
            }, ct);
        });

    public Task<AxisResult<bool>> ExistsAsync(string key)
    {
        var ct = accessor.AxisMediator!.CancellationToken;
        ct.ThrowIfCancellationRequested();
        return AxisResult.TryAsync(async () =>
        {
            try
            {
                await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = settings.BucketName,
                    Key = key
                }, ct);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        });
    }

    public Task<AxisResult<string>> GetPresignedUrlAsync(string key, TimeSpan expiration)
        => AxisResult.TryAsync(async () =>
        {
            var ct = accessor.AxisMediator!.CancellationToken;
            ct.ThrowIfCancellationRequested();
            var url = await s3Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
            {
                BucketName = settings.BucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expiration)
            });
            return url;
        });

    public Task<AxisResult<AxisStorageUrl>> GetServableUrlAsync(string key, TimeSpan expiration)
        => AxisResult.TryAsync(async () =>
        {
            var ct = accessor.AxisMediator!.CancellationToken;
            ct.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(settings.PublicUrl))
                return new AxisStorageUrl($"{settings.PublicUrl!.TrimEnd('/')}/{key}", IsPublic: true, ExpiresAt: null);

            var url = await s3Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
            {
                BucketName = settings.BucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expiration)
            });
            return new AxisStorageUrl(url, IsPublic: false, ExpiresAt: DateTime.UtcNow.Add(expiration));
        });

    public Task<AxisResult<bool>> ExistsAsync()
        => AxisResult.TryAsync(async () =>
        {
            var ct = accessor.AxisMediator!.CancellationToken;
            ct.ThrowIfCancellationRequested();
            try
            {
                await s3Client.GetBucketLocationAsync(new GetBucketLocationRequest
                {
                    BucketName = settings.BucketName
                }, ct);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        });

    public Task<AxisResult> EnsureExistsAsync()
        => AxisResult.TryAsync(async () =>
        {
            var ct = accessor.AxisMediator!.CancellationToken;
            ct.ThrowIfCancellationRequested();
            try
            {
                await s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = settings.BucketName
                }, ct);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "BucketAlreadyOwnedByYou")
            {
            }
        });

    // Note: bucket ACL (legacy grants API) is the most broadly S3-compatible way to detect public
    // access; some S3-compatible providers (e.g. Cloudflare R2) manage public access outside the
    // S3 API surface and may not honor this call the same way AWS S3/MinIO do.
    public Task<AxisResult<bool>> IsPubliclyAccessibleAsync()
        => AxisResult.TryAsync(async () =>
        {
            var ct = accessor.AxisMediator!.CancellationToken;
            ct.ThrowIfCancellationRequested();
            var acl = await s3Client.GetBucketAclAsync(new GetBucketAclRequest
            {
                BucketName = settings.BucketName
            }, ct);
            return acl.Grants.Any(grant =>
                grant.Grantee?.URI == "http://acs.amazonaws.com/groups/global/AllUsers" &&
                grant.Permission == S3Permission.READ);
        });

    public Task<AxisResult<IReadOnlyList<string>>> ListAsync(string prefix)
        => AxisResult.TryAsync(async () =>
        {
            var ct = accessor.AxisMediator!.CancellationToken;
            ct.ThrowIfCancellationRequested();
            List<string> keys = [];
            string? continuationToken = null;
            do
            {
                var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = settings.BucketName,
                    Prefix = prefix,
                    ContinuationToken = continuationToken
                }, ct);
                keys.AddRange((response.S3Objects ?? []).Select(o => o.Key));
                continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
            } while (continuationToken is not null);
            return (IReadOnlyList<string>)keys;
        });
}
