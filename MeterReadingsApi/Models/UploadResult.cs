namespace MeterReadingsApi.Models
{
    public class UploadResult
    {
        public int Success { get; set; }
        public int Failure { get; set; }
        public List<string> FailureReasons { get; set; } = new();
    }
}
