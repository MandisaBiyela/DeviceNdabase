namespace DeviceDesk.Modules.Phase0.Models
{
    public record ImportResultDto(Guid batchId, int added, int duplicates, int invalid, int total);
}