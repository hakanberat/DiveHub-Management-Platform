
using DiveHub.Application;
using DiveHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Infrastructure;

public sealed class OperationsCatalogService(
    DiveHubDbContext db) : IOperationsCatalogService
{
    public async Task<IReadOnlyList<BoatItem>> GetBoatsAsync(
        CancellationToken ct = default)
    {
        return await db.Boats
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new BoatItem(
                x.Id, x.Name, x.Capacity, x.IsActive))
            .ToListAsync(ct);
    }

    public async Task<BoatItem> CreateBoatAsync(
        SaveBoatRequest request,
        CancellationToken ct = default)
    {
        var boat = new Boat
        {
            Name = request.Name.Trim(),
            Capacity = request.Capacity,
            IsActive = true
        };

        db.Boats.Add(boat);
        await db.SaveChangesAsync(ct);

        return ToBoatItem(boat);
    }

    public async Task<BoatItem?> UpdateBoatAsync(
        int id,
        SaveBoatRequest request,
        CancellationToken ct = default)
    {
        var boat = await db.Boats.FindAsync(
            new object[] { id }, ct);

        if (boat is null)
            return null;

        boat.Name = request.Name.Trim();
        boat.Capacity = request.Capacity;

        await db.SaveChangesAsync(ct);
        return ToBoatItem(boat);
    }

    public async Task<BoatItem?> SetBoatActiveAsync(
        int id,
        bool active,
        CancellationToken ct = default)
    {
        var boat = await db.Boats.FindAsync(
            new object[] { id }, ct);

        if (boat is null)
            return null;

        boat.IsActive = active;
        await db.SaveChangesAsync(ct);

        return ToBoatItem(boat);
    }

    public async Task<IReadOnlyList<InstructorItem>>
        GetInstructorsAsync(
            bool publicOnly,
            CancellationToken ct = default)
    {
        var query = db.StaffMembers
            .AsNoTracking()
            .AsQueryable();

        if (publicOnly)
        {
            query = query.Where(x =>
                x.IsActive && x.IsPublished);
        }

        return await query
            .OrderBy(x => x.FullName)
            .Select(x => new InstructorItem(
                x.Id,
                x.FullName,
                x.Biography,
                x.CertificationAgency,
                x.CertificationNumber,
                x.IsActive,
                x.IsPublished))
            .ToListAsync(ct);
    }

    public async Task<InstructorItem> CreateInstructorAsync(
        SaveInstructorRequest request,
        CancellationToken ct = default)
    {
        var instructor = new StaffMember
        {
            FullName = request.FullName.Trim(),
            Biography = request.Biography.Trim(),
            CertificationAgency =
                request.CertificationAgency.Trim(),
            CertificationNumber =
                request.CertificationNumber.Trim(),
            IsActive = true,
            IsPublished = false
        };

        db.StaffMembers.Add(instructor);
        await db.SaveChangesAsync(ct);

        return ToInstructorItem(instructor);
    }

    public async Task<InstructorItem?> UpdateInstructorAsync(
        int id,
        SaveInstructorRequest request,
        CancellationToken ct = default)
    {
        var instructor = await db.StaffMembers.FindAsync(
            new object[] { id }, ct);

        if (instructor is null)
            return null;

        instructor.FullName = request.FullName.Trim();
        instructor.Biography = request.Biography.Trim();
        instructor.CertificationAgency =
            request.CertificationAgency.Trim();
        instructor.CertificationNumber =
            request.CertificationNumber.Trim();

        await db.SaveChangesAsync(ct);

        return ToInstructorItem(instructor);
    }

    public async Task<InstructorItem?> SetInstructorActiveAsync(
        int id,
        bool active,
        CancellationToken ct = default)
    {
        var instructor = await db.StaffMembers.FindAsync(
            new object[] { id }, ct);

        if (instructor is null)
            return null;

        instructor.IsActive = active;
        await db.SaveChangesAsync(ct);

        return ToInstructorItem(instructor);
    }

    public async Task<InstructorItem?> SetInstructorPublishedAsync(
        int id,
        bool published,
        CancellationToken ct = default)
    {
        var instructor = await db.StaffMembers.FindAsync(
            new object[] { id }, ct);

        if (instructor is null)
            return null;

        instructor.IsPublished = published;
        await db.SaveChangesAsync(ct);

        return ToInstructorItem(instructor);
    }

    private static BoatItem ToBoatItem(Boat boat) =>
        new(
            boat.Id,
            boat.Name,
            boat.Capacity,
            boat.IsActive
        );

    private static InstructorItem ToInstructorItem(
        StaffMember instructor) =>
        new(
            instructor.Id,
            instructor.FullName,
            instructor.Biography,
            instructor.CertificationAgency,
            instructor.CertificationNumber,
            instructor.IsActive,
            instructor.IsPublished
        );
}
