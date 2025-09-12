namespace BIDashboardBackend.DTOs.Request
{
    public class UploadCsvDto
    {
        public IFormFile File { get; set; } = default!;
    }
}
