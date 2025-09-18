using System.ComponentModel.DataAnnotations;

namespace BIDashboardBackend.DTOs.Request
{
    /// <summary>
    /// CSV 檔案上傳請求 DTO，包含完整的檔案驗證
    /// </summary>
    public class UploadCsvDto
    {
        /// <summary>
        /// 上傳的 CSV 檔案
        /// </summary>
        [Required(ErrorMessage = "檔案不能為空")]
        [FileValidation]
        public IFormFile File { get; set; } = default!;
    }

    /// <summary>
    /// 檔案驗證屬性
    /// </summary>
    public class FileValidationAttribute : ValidationAttribute
    {
        private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
        private static readonly string[] AllowedExtensions = { ".csv" };
        private static readonly string[] AllowedContentTypes = { 
            "text/csv", 
            "application/csv", 
            "text/plain",
            "application/vnd.ms-excel"
        };

        public override bool IsValid(object? value)
        {
            if (value is not IFormFile file)
            {
                ErrorMessage = "無效的檔案";
                return false;
            }

            // 檢查檔案大小
            if (file.Length == 0)
            {
                ErrorMessage = "檔案內容不能為空";
                return false;
            }

            if (file.Length > MaxFileSize)
            {
                ErrorMessage = $"檔案大小不能超過 {MaxFileSize / (1024 * 1024)} MB";
                return false;
            }

            // 檢查檔案副檔名
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                ErrorMessage = "只允許上傳 .csv 檔案";
                return false;
            }

            // 檢查 MIME 類型（如果有提供）
            if (!string.IsNullOrEmpty(file.ContentType) && 
                !AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                ErrorMessage = $"不支援的檔案類型: {file.ContentType}";
                return false;
            }

            // 檢查檔案名稱安全性
            if (ContainsDangerousPath(file.FileName))
            {
                ErrorMessage = "檔案名稱包含不安全的字符";
                return false;
            }

            return true;
        }

        private static bool ContainsDangerousPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return true;

            var dangerousPatterns = new[] { "..", "/", "\\", ":", "*", "?", "\"", "<", ">", "|" };
            return dangerousPatterns.Any(fileName.Contains);
        }
    }
}
