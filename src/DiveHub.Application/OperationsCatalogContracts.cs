
namespace DiveHub.Application;

public sealed record BoatItem(
    int Id,
    string Name,
    int Capacity,
    bool IsActive
);

public sealed record InstructorItem(
    int Id,
    string FullName,
    string Biography,
    string CertificationAgency,
    string CertificationNumber,
    bool IsActive,
    bool IsPublished
);

public sealed record SaveBoatRequest(
    string Name,
    int Capacity
);

public sealed record SaveInstructorRequest(
    string FullName,
    string Biography,
    string CertificationAgency,
    string CertificationNumber
);

public sealed record SetActiveRequest(bool IsActive);

public sealed record SetPublishedRequest(bool IsPublished);

public interface IOperationsCatalogService
{
    Task<IReadOnlyList<BoatItem>> GetBoatsAsync(
        CancellationToken ct = default);

    Task<BoatItem> CreateBoatAsync(
        SaveBoatRequest request,
        CancellationToken ct = default);

    Task<BoatItem?> UpdateBoatAsync(
        int id,
        SaveBoatRequest request,
        CancellationToken ct = default);

    Task<BoatItem?> SetBoatActiveAsync(
        int id,
        bool active,
        CancellationToken ct = default);

    Task<IReadOnlyList<InstructorItem>> GetInstructorsAsync(
        bool publicOnly,
        CancellationToken ct = default);

    Task<InstructorItem> CreateInstructorAsync(
        SaveInstructorRequest request,
        CancellationToken ct = default);

    Task<InstructorItem?> UpdateInstructorAsync(
        int id,
        SaveInstructorRequest request,
        CancellationToken ct = default);

    Task<InstructorItem?> SetInstructorActiveAsync(
        int id,
        bool active,
        CancellationToken ct = default);

    Task<InstructorItem?> SetInstructorPublishedAsync(
        int id,
        bool published,
        CancellationToken ct = default);
}
