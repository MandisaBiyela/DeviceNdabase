using System;
using System.Collections.Generic;

namespace DeviceDesk.Modules.Phase1.Models
{
    public enum AllocationTypeDto
    {
        None = 0,
        Student = 1,
        Teacher = 2
    }

    public class DeviceAllocationDto
    {
        public Guid DeviceId { get; set; }
        public AllocationTypeDto AllocationType { get; set; }
        public string? StudentName { get; set; }
        public string? StudentIdNumber { get; set; }
        public string? TeacherName { get; set; }
        public string? TeacherPersalNumber { get; set; }
    }

    public class BulkAllocationRequest
    {
        public Guid BatchId { get; set; }
        public List<DeviceAllocationDto> Allocations { get; set; } = new();
    }
}

