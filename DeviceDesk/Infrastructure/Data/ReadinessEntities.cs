using System;
using System.Collections.Generic;

namespace DeviceDesk.Infrastructure.Data
{
    public enum ReadinessState
    {
        Draft = 0,
        Submitted = 1,
        Reviewed = 2,
        Approved = 3,
        Rejected = 4,
        NeedsReview = 5,
        SchoolComplete = 6
    }

    public enum EvidenceKind
    {
        Photo = 0,
        Video = 1,
        Pdf = 2,
        Note = 3
    }

    public enum IssueSeverity
    {
        Info = 0,
        Minor = 1,
        Major = 2,
        Critical = 3
    }

    public class ReadinessReport
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EmisCode { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string SubmittedByUserId { get; set; } = string.Empty;
        public ReadinessState State { get; set; } = ReadinessState.Draft;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? SubmittedAt { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }

        public ICollection<ReadinessRoom> Rooms { get; set; } = new List<ReadinessRoom>();
        public ICollection<ReadinessEvidence> Evidence { get; set; } = new List<ReadinessEvidence>();
    }

    public class ReadinessRoom
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ReportId { get; set; }
        public ReadinessReport Report { get; set; } = null!;

        public string RoomCode { get; set; } = string.Empty; // e.g., "Grade1A"
        public string RoomName { get; set; } = string.Empty;
        public int Index { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public ICollection<ReadinessRoomItem> Items { get; set; } = new List<ReadinessRoomItem>();
        public ICollection<ReadinessEvidence> Evidence { get; set; } = new List<ReadinessEvidence>();
    }

    public class ReadinessRoomItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public ReadinessRoom Room { get; set; } = null!;

        public string ChecklistKey { get; set; } = string.Empty; // unique per room
        public bool Value { get; set; }
        public string? Notes { get; set; }
        public IssueSeverity? Severity { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class ReadinessEvidence
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ReportId { get; set; }
        public ReadinessReport Report { get; set; } = null!;

        public Guid? RoomId { get; set; }
        public ReadinessRoom? Room { get; set; }

        public Guid? RoomItemId { get; set; }
        public ReadinessRoomItem? RoomItem { get; set; }

        public EvidenceKind Kind { get; set; }
        public string StoragePath { get; set; } = string.Empty; // relative path under wwwroot
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string? Caption { get; set; }
        public bool IsPrimary { get; set; }
        public bool ForReview { get; set; }

        public string Sha256 { get; set; } = string.Empty; // dedupe
        public DateTimeOffset TakenAt { get; set; } = DateTimeOffset.UtcNow;
        public double? GpsLat { get; set; }
        public double? GpsLng { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}