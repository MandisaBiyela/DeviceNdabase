namespace DeviceDesk.Modules.Phase2.Models;

public enum Phase2Zone
{
    NewStock,
    RnR
}

// Dispatch: Device has passed ICT Center assessment & QA,
// and is now ready to be included in a POD for delivery.
// SchoolAllocation: Device has been allocated to a POD and Delivery Note,
// and is considered on its way to / at the school.
public enum Phase2Stage
{
    Received,
    PreAssessment,
    DetailedInspection,
    HardwareDept,
    SoftwareDept,
    QualityAssessment,
    Dispatch,
    SchoolAllocation,
    WarrantyReturn,
    Quarantine,
    Disposal,
    AwaitingDispatch
}

public enum InspectionCategory
{
    HardwareFailure,
    SoftwareIssueUpgrade,
    NoIssuesFound,
    Quarantine,
    Disposal,
    WarrantyReturn
}

public enum PickingSlipStatus
{
    Draft,
    ReadyForPicking,
    PickingInProgress,
    Completed,
    Cancelled
}

public enum RepairStatus
{
    PendingAuthorization = 1,
    AwaitingParts = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}
