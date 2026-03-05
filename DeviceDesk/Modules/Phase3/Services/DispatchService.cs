using DeviceDesk.Modules.Phase3.Data;
using DeviceDesk.Modules.Phase3.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase3.Services;

public class Phase3DispatchService
{
    private readonly Phase3DbContext _context;

    public Phase3DispatchService(Phase3DbContext context)
    {
        _context = context;
    }

    // PAGE 1: Dispatch Preparation (Dispatch Clerk)
    public async Task<List<DispatchPOD>> GetReadyForDispatchAsync()
    {
        return await _context.DispatchPODs
            .Where(p => p.Status == PODStatus.ReadyForDispatch)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<DispatchPOD?> ScanDeviceToDispatchAsync(string podNumber, string userId)
    {
        var pod = await _context.DispatchPODs
            .FirstOrDefaultAsync(p => p.PODNumber == podNumber && p.Status == PODStatus.ReadyForDispatch);

        if (pod == null) return null;

        pod.Status = PODStatus.InDispatch;
        await _context.SaveChangesAsync();

        return pod;
    }

    public async Task<DispatchTrip> CreateTripAsync(string tripRef, string driverName, string? driverUserId, 
        string vehicleReg, List<Guid> podIds, string createdBy)
    {
        var trip = new DispatchTrip
        {
            TripRef = tripRef,
            DriverName = driverName,
            DriverUserId = driverUserId,
            VehicleReg = vehicleReg,
            Status = TripStatus.Draft,
            CreatedByUserId = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.DispatchTrips.Add(trip);
        await _context.SaveChangesAsync();

        // Assign PODs to trip
        var pods = await _context.DispatchPODs
            .Where(p => podIds.Contains(p.PODId))
            .ToListAsync();

        foreach (var pod in pods)
        {
            pod.TripId = trip.TripId;
            pod.Status = PODStatus.AssignedToTrip;
        }

        await _context.SaveChangesAsync();
        return trip;
    }

    public async Task<bool> SendTripToDriverAsync(Guid tripId, string userId)
    {
        var trip = await _context.DispatchTrips.FindAsync(tripId);
        if (trip == null || trip.Status != TripStatus.Draft) return false;

        trip.Status = TripStatus.PendingAcceptance;
        trip.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    // PAGE 2: Transport & Handover (Driver)
    public async Task<List<DispatchTrip>> GetDriverTripsAsync(string driverUserId)
    {
        return await _context.DispatchTrips
            .Include(t => t.PODs)
            .Where(t => t.DriverUserId == driverUserId && 
                       (t.Status == TripStatus.PendingAcceptance || t.Status == TripStatus.InTransit))
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> AcceptTripAsync(Guid tripId, string driverUserId)
    {
        var trip = await _context.DispatchTrips
            .Include(t => t.PODs)
            .FirstOrDefaultAsync(t => t.TripId == tripId && t.DriverUserId == driverUserId);

        if (trip == null || trip.Status != TripStatus.PendingAcceptance) return false;

        trip.Status = TripStatus.InTransit;
        trip.DriverAccepted = true;
        trip.DriverAcceptedAt = DateTimeOffset.UtcNow;
        trip.UpdatedAt = DateTimeOffset.UtcNow;

        // Update PODs
        foreach (var pod in trip.PODs)
        {
            pod.Status = PODStatus.InTransit;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkPODDeliveredAsync(Guid podId, string driverUserId, 
        bool schoolSigned, string? signatoryName, bool hasExceptions, string? exceptionNotes)
    {
        var pod = await _context.DispatchPODs
            .Include(p => p.Trip)
            .FirstOrDefaultAsync(p => p.PODId == podId);

        if (pod == null || pod.Trip?.DriverUserId != driverUserId) return false;

        pod.Status = hasExceptions ? PODStatus.Exception : PODStatus.Delivered;
        pod.SchoolSigned = schoolSigned;
        pod.SchoolSignedAt = schoolSigned ? DateTimeOffset.UtcNow : null;
        pod.SchoolSignatoryName = signatoryName;
        pod.HasExceptions = hasExceptions;
        pod.ExceptionNotes = exceptionNotes;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UploadSignedPODAsync(Guid podId, long documentId, string userId)
    {
        var pod = await _context.DispatchPODs.FindAsync(podId);
        if (pod == null) return false;

        pod.SignedPODDocumentId = documentId;
        pod.SignedPODUploadedAt = DateTimeOffset.UtcNow;
        pod.SignedPODUploadedByUserId = userId;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CompleteTripAsync(Guid tripId, string driverUserId)
    {
        var trip = await _context.DispatchTrips
            .Include(t => t.PODs)
            .FirstOrDefaultAsync(t => t.TripId == tripId && t.DriverUserId == driverUserId);

        if (trip == null || trip.Status != TripStatus.InTransit) return false;

        // Check all PODs are delivered
        if (trip.PODs.Any(p => p.Status == PODStatus.InTransit || p.Status == PODStatus.AssignedToTrip))
            return false;

        trip.Status = TripStatus.Completed;
        trip.Completed = true;
        trip.CompletedAt = DateTimeOffset.UtcNow;
        trip.CompletedByUserId = driverUserId;
        trip.UpdatedAt = DateTimeOffset.UtcNow;

        // Move to debriefing
        trip.Status = TripStatus.InDebriefing;
        foreach (var pod in trip.PODs)
        {
            if (pod.Status == PODStatus.Delivered || pod.Status == PODStatus.Exception)
            {
                pod.Status = PODStatus.InDebriefing;
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    // PAGE 3: Debriefing (Dispatch QA)
    public async Task<List<DispatchTrip>> GetDebriefingTripsAsync()
    {
        return await _context.DispatchTrips
            .Include(t => t.PODs)
            .Where(t => t.Status == TripStatus.InDebriefing || t.Status == TripStatus.DebriefingFailed)
            .OrderBy(t => t.CompletedAt)
            .ToListAsync();
    }

    public async Task<bool> SubmitDebriefingAsync(Guid tripId, bool passed, string notes, string qaUserId)
    {
        var trip = await _context.DispatchTrips
            .Include(t => t.PODs)
            .FirstOrDefaultAsync(t => t.TripId == tripId);

        if (trip == null || (trip.Status != TripStatus.InDebriefing && trip.Status != TripStatus.DebriefingFailed))
            return false;

        trip.DebriefingPassed = passed;
        trip.DebriefingCompletedAt = DateTimeOffset.UtcNow;
        trip.DebriefingByUserId = qaUserId;
        trip.DebriefingNotes = notes;
        trip.UpdatedAt = DateTimeOffset.UtcNow;

        if (passed)
        {
            trip.Status = TripStatus.AwaitingSignOff;
            foreach (var pod in trip.PODs)
            {
                if (pod.Status == PODStatus.InDebriefing)
                {
                    pod.Status = PODStatus.AwaitingSignOff;
                }
            }
        }
        else
        {
            trip.Status = TripStatus.DebriefingFailed;
            foreach (var pod in trip.PODs)
            {
                if (pod.Status == PODStatus.InDebriefing)
                {
                    pod.Status = PODStatus.DebriefingFailed;
                }
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    // PAGE 4: Final Sign-Off (Dispatch Manager)
    public async Task<List<DispatchTrip>> GetSignOffTripsAsync()
    {
        return await _context.DispatchTrips
            .Include(t => t.PODs)
            .Where(t => t.Status == TripStatus.AwaitingSignOff)
            .OrderBy(t => t.DebriefingCompletedAt)
            .ToListAsync();
    }

    public async Task<bool> SubmitFinalSignOffAsync(Guid tripId, bool passed, string notes, string managerUserId)
    {
        var trip = await _context.DispatchTrips
            .Include(t => t.PODs)
            .FirstOrDefaultAsync(t => t.TripId == tripId && t.Status == TripStatus.AwaitingSignOff);

        if (trip == null) return false;

        trip.FinalSignOffPassed = passed;
        trip.FinalSignOffAt = DateTimeOffset.UtcNow;
        trip.FinalSignOffByUserId = managerUserId;
        trip.FinalSignOffNotes = notes;
        trip.UpdatedAt = DateTimeOffset.UtcNow;

        if (passed)
        {
            trip.Status = TripStatus.Closed;
            foreach (var pod in trip.PODs)
            {
                if (pod.Status == PODStatus.AwaitingSignOff)
                {
                    pod.Status = PODStatus.Closed;
                }
            }
        }
        else
        {
            // Send back to debriefing
            trip.Status = TripStatus.DebriefingFailed;
            foreach (var pod in trip.PODs)
            {
                if (pod.Status == PODStatus.AwaitingSignOff)
                {
                    pod.Status = PODStatus.DebriefingFailed;
                }
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    // General queries
    public async Task<DispatchTrip?> GetTripDetailsAsync(Guid tripId)
    {
        return await _context.DispatchTrips
            .Include(t => t.PODs)
            .FirstOrDefaultAsync(t => t.TripId == tripId);
    }

    public async Task<List<DispatchTrip>> GetAllTripsAsync()
    {
        return await _context.DispatchTrips
            .Include(t => t.PODs)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }
}
