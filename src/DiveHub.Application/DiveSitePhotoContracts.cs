
namespace DiveHub.Application;

public sealed record DiveSitePhotoDto(
    long Id,
    int DiveSiteId,
    string AltText
);

public sealed record StoredPhoto(
    string FileName,
    string RelativePath
);

public interface IPhotoStorage
{
    Task<StoredPhoto> SaveAsync(
        int diveSiteId,
        string originalFileName,
        Stream source,
        long length,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<StoredPhoto> SaveActivityAsync(
        int activityId,
        string originalFileName,
        Stream source,
        long length,
        CancellationToken cancellationToken = default);    

       Task<StoredPhoto> SaveInstructorAsync(
            int instructorId,
            string originalFileName,
            Stream source,
            long length,
            CancellationToken cancellationToken = default); 
}
