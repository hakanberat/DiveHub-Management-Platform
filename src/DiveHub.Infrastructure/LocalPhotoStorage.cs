
using System.Text.RegularExpressions;
using DiveHub.Application;
using ImageMagick;
using Microsoft.Extensions.Configuration;

namespace DiveHub.Infrastructure;

public sealed class LocalPhotoStorage : IPhotoStorage
{
    // Maximum uploaded image size: 5 MB
    private const long MaxInputSize = 5 * 1024 * 1024;

    // Maximum image dimensions and pixel count
    private const int MaxDimension = 8000;
    private const ulong MaxPixels = 24_000_000;

    // Maximum output width or height
    private const uint OutputDimension = 1920;

    // Allowed storage paths
    private static readonly Regex SafePath = new(
        @"^(?:dive-sites|activities|instructors)/[1-9]\d*/[a-f0-9]{32}\.webp\z",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant);

    private readonly string _root;

    // --------------------------------------------------
    // CONSTRUCTOR
    // --------------------------------------------------

    public LocalPhotoStorage(IConfiguration configuration)
    {
        var root = configuration["MediaStorage:Root"];

        if (string.IsNullOrWhiteSpace(root) ||
            !Path.IsPathFullyQualified(root))
        {
            throw new InvalidOperationException(
                "MediaStorage:Root must be an absolute directory path.");
        }

        _root = Path.GetFullPath(root);

        Directory.CreateDirectory(_root);
    }

    // --------------------------------------------------
    // DIVE SITE PHOTOS
    // --------------------------------------------------

    public Task<StoredPhoto> SaveAsync(
        int diveSiteId,
        string originalFileName,
        Stream source,
        long length,
        CancellationToken cancellationToken = default)
    {
        return SaveCoreAsync(
            "dive-sites",
            diveSiteId,
            originalFileName,
            source,
            length,
            cancellationToken);
    }

    // --------------------------------------------------
    // COURSE AND ACTIVITY PHOTOS
    // --------------------------------------------------

    public Task<StoredPhoto> SaveActivityAsync(
        int activityId,
        string originalFileName,
        Stream source,
        long length,
        CancellationToken cancellationToken = default)
    {
        return SaveCoreAsync(
            "activities",
            activityId,
            originalFileName,
            source,
            length,
            cancellationToken);
    }

    // --------------------------------------------------
    // INSTRUCTOR PROFILE PHOTOS
    // --------------------------------------------------

    public Task<StoredPhoto> SaveInstructorAsync(
        int instructorId,
        string originalFileName,
        Stream source,
        long length,
        CancellationToken cancellationToken = default)
    {
        return SaveCoreAsync(
            "instructors",
            instructorId,
            originalFileName,
            source,
            length,
            cancellationToken);
    }

    // --------------------------------------------------
    // SHARED PHOTO PROCESSING AND STORAGE
    // --------------------------------------------------

    private async Task<StoredPhoto> SaveCoreAsync(
        string category,
        int resourceId,
        string originalFileName,
        Stream source,
        long length,
        CancellationToken ct)
    {
        // Validate resource ID and uploaded file size.
        if (resourceId <= 0 ||
            length <= 0 ||
            length > MaxInputSize)
        {
            throw new InvalidDataException(
                "Invalid image size.");
        }

        // Validate file extension.
        var extension = Path.GetExtension(
            originalFileName).ToLowerInvariant();

        if (extension is not
            (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            throw new InvalidDataException(
                "Only JPEG, PNG and WebP files are supported.");
        }

        // --------------------------------------------------
        // READ UPLOADED IMAGE WITH SIZE LIMIT
        // --------------------------------------------------

        using var buffer = new MemoryStream();

        var chunk = new byte[81920];

        long totalBytes = 0;

        while (true)
        {
            int bytesRead = await source.ReadAsync(
                chunk.AsMemory(),
                ct);

            if (bytesRead == 0)
                break;

            totalBytes += bytesRead;

            // Stop reading immediately if the limit is exceeded.
            if (totalBytes > MaxInputSize)
            {
                throw new InvalidDataException(
                    "Image exceeds the maximum allowed size of 5 MB.");
            }

            await buffer.WriteAsync(
                chunk.AsMemory(0, bytesRead),
                ct);
        }

        if (totalBytes != length)
        {
            throw new InvalidDataException(
                "Uploaded image length does not match the expected size.");
        }

        var inputBytes = buffer.ToArray();

        // --------------------------------------------------
        // VALIDATE FILE SIGNATURE
        // --------------------------------------------------

        bool isJpeg =
            inputBytes.Length >= 3 &&
            inputBytes[0] == 0xFF &&
            inputBytes[1] == 0xD8 &&
            inputBytes[2] == 0xFF;

        bool isPng =
            inputBytes.Length >= 8 &&
            inputBytes.AsSpan(0, 8).SequenceEqual(
                new byte[]
                {
                    137, 80, 78, 71, 13, 10, 26, 10
                });

        bool isWebp =
            inputBytes.Length >= 12 &&
            System.Text.Encoding.ASCII.GetString(
                inputBytes, 0, 4) == "RIFF" &&
            System.Text.Encoding.ASCII.GetString(
                inputBytes, 8, 4) == "WEBP";

        bool matches = extension switch
        {
            ".jpg" or ".jpeg" => isJpeg,
            ".png" => isPng,
            ".webp" => isWebp,
            _ => false
        };

        if (!matches)
        {
            throw new InvalidDataException(
                "File signature does not match the extension.");
        }

        // --------------------------------------------------
        // IMAGE VALIDATION AND WEBP CONVERSION
        // --------------------------------------------------

        byte[] outputBytes;

        try
        {
            var info = new MagickImageInfo(inputBytes);

            var expectedFormat = extension switch
            {
                ".jpg" or ".jpeg" => MagickFormat.Jpeg,
                ".png" => MagickFormat.Png,
                _ => MagickFormat.WebP
            };

            // Validate actual image format and dimensions.
            if (info.Format != expectedFormat ||
                info.Width == 0 ||
                info.Height == 0 ||
                info.Width > MaxDimension ||
                info.Height > MaxDimension ||
                (ulong)info.Width * info.Height > MaxPixels)
            {
                throw new InvalidDataException(
                    "Unsupported dimensions or image format.");
            }

            using var image = new MagickImage(inputBytes);

            // Correct image orientation.
            image.AutoOrient();

            // Resize only when necessary.
            if (image.Width > OutputDimension ||
                image.Height > OutputDimension)
            {
                var geometry = new MagickGeometry(
                    OutputDimension,
                    OutputDimension)
                {
                    Greater = true
                };

                image.Resize(geometry);
            }

            // Remove EXIF, GPS and other metadata.
            image.Strip();

            // Convert to optimized WebP.
            image.Format = MagickFormat.WebP;
            image.Quality = 82;

            outputBytes = image.ToByteArray();

            if (outputBytes.Length == 0 ||
                outputBytes.Length > MaxInputSize)
            {
                throw new InvalidDataException(
                    "Processed image is too large.");
            }
        }
        catch (MagickException ex)
        {
            throw new InvalidDataException(
                "Image could not be decoded.", ex);
        }

        // --------------------------------------------------
        // GENERATE SAFE FILE NAME AND PATH
        // --------------------------------------------------

        var fileName = $"{Guid.NewGuid():N}.webp";

        var relativePath =
            $"{category}/{resourceId}/{fileName}";

        var fullPath = ResolvePath(relativePath);

        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)!);

        // --------------------------------------------------
        // SAVE IMAGE TO PERMANENT STORAGE
        // --------------------------------------------------

        try
        {
            await using var output = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);

            await output.WriteAsync(
                outputBytes,
                ct);
        }
        catch
        {
            // Remove an incomplete file if saving fails.
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            throw;
        }

        return new StoredPhoto(
            fileName,
            relativePath);
    }

    // --------------------------------------------------
    // READ STORED PHOTO
    // --------------------------------------------------

    public Task<Stream?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(relativePath);

        try
        {
            Stream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

            return Task.FromResult<Stream?>(stream);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    // --------------------------------------------------
    // DELETE STORED PHOTO
    // --------------------------------------------------

    public Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(relativePath);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    // --------------------------------------------------
    // VALIDATE AND RESOLVE STORAGE PATH
    // --------------------------------------------------

    private string ResolvePath(string relativePath)
    {
        if (!SafePath.IsMatch(relativePath))
        {
            throw new InvalidOperationException(
                "Invalid stored media path.");
        }

        var path = relativePath.Replace(
            '/',
            Path.DirectorySeparatorChar);

        return Path.Combine(_root, path);
    }
}
