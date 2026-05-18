using System;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase2.Data;

namespace DeviceDesk.Modules.Phase2.Models
{
    public class Phase2AllocationListItemDto
    {
        public int Phase2DeviceId { get; set; }
        public string Serial { get; set; } = string.Empty;
        public Phase2Zone Zone { get; set; }
        public Phase2Stage Stage { get; set; }
        public int? SchoolId { get; set; }
        public string? SchoolName { get; set; }
        public bool? QaPassed { get; set; }
        public bool IsInStorage { get; set; }
        public AllocationType AllocationType { get; set; }
        public string? StudentName { get; set; }
        public string? StudentIdNumber { get; set; }
        public string? TeacherName { get; set; }
        public string? TeacherPersalNumber { get; set; }
    }
}
