using System;

namespace DeviceDesk.Infrastructure.Data.Enums
{
    public enum DeviceCategory
    {
        Unknown = 0,
        Laptop = 1,
        Desktop = 2,
        Printer = 3,
        VrHeadset = 4,
        Monitor = 5,
        Other = 99
    }

    public enum StorageArea
    {
        Unknown = 0,
        Phase2IctCenter = 2,
        Phase2DispatchReady = 6,
        AtSchool = 8,
        ScrapCage = 9
    }
}

