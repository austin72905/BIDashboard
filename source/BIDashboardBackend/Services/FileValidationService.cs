using System.Text;

namespace BIDashboardBackend.Services
{
    /// <summary>
    /// 檔案驗證服務，提供完整的檔案類型和內容驗證
    /// </summary>
    public interface IFileValidationService
    {
        /// <summary>
        /// 驗證檔案是否為合法的 CSV 檔案
        /// </summary>
        /// <param name="file">上傳的檔案</param>
        /// <returns>驗證結果</returns>
        Task<FileValidationResult> ValidateCsvFileAsync(IFormFile file);
    }

    /// <summary>
    /// 檔案驗證結果
    /// </summary>
    public class FileValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class FileValidationService : IFileValidationService
    {
        private readonly ILogger<FileValidationService> _logger;

        // 允許的 MIME 類型
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "text/csv",
            "application/csv",
            "text/plain",
            "application/vnd.ms-excel"  // Excel 有時會將 CSV 識別為此類型
        };

        // CSV 檔案的魔術字節（Magic Bytes）檢查
        private static readonly byte[][] CsvMagicBytes = new[]
        {
            // UTF-8 BOM
            new byte[] { 0xEF, 0xBB, 0xBF },
            // UTF-16 LE BOM  
            new byte[] { 0xFF, 0xFE },
            // UTF-16 BE BOM
            new byte[] { 0xFE, 0xFF }
        };

        // 最大檔案大小（10MB）
        private const long MaxFileSize = 10 * 1024 * 1024;

        // 最大列數限制
        private const int MaxRowCount = 100000;

        // 最大欄位數限制
        private const int MaxColumnCount = 100;

        public FileValidationService(ILogger<FileValidationService> logger)
        {
            _logger = logger;
        }

        public async Task<FileValidationResult> ValidateCsvFileAsync(IFormFile file)
        {
            var result = new FileValidationResult { IsValid = true };

            try
            {
                // 1. 基本檔案驗證
                if (!ValidateBasicFile(file, result))
                    return result;

                // 2. 檔案名稱驗證
                if (!ValidateFileName(file.FileName, result))
                    return result;

                // 3. MIME 類型驗證
                if (!ValidateMimeType(file.ContentType, result))
                    return result;

                // 4. 檔案大小驗證
                if (!ValidateFileSize(file.Length, result))
                    return result;

                // 5. 檔案內容驗證
                if (!await ValidateFileContentAsync(file, result))
                    return result;

                _logger.LogInformation("檔案驗證通過: {FileName}, 大小: {FileSize} bytes", 
                    file.FileName, file.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "檔案驗證過程中發生錯誤: {FileName}", file.FileName);
                result.IsValid = false;
                result.ErrorMessage = "檔案驗證過程中發生內部錯誤";
                return result;
            }
        }

        private bool ValidateBasicFile(IFormFile file, FileValidationResult result)
        {
            if (file == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "檔案不能為空";
                return false;
            }

            if (file.Length == 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "檔案內容為空";
                return false;
            }

            return true;
        }

        private bool ValidateFileName(string fileName, FileValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                result.IsValid = false;
                result.ErrorMessage = "檔案名稱不能為空";
                return false;
            }

            // 檢查副檔名
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (extension != ".csv")
            {
                result.IsValid = false;
                result.ErrorMessage = "只允許上傳 .csv 格式的檔案";
                return false;
            }

            // 檢查檔案名稱中的危險字符
            var dangerousChars = new[] { "..", "/", "\\", ":", "*", "?", "\"", "<", ">", "|" };
            if (dangerousChars.Any(fileName.Contains))
            {
                result.IsValid = false;
                result.ErrorMessage = "檔案名稱包含不允許的字符";
                return false;
            }

            // 檢查檔案名稱長度
            if (fileName.Length > 255)
            {
                result.IsValid = false;
                result.ErrorMessage = "檔案名稱過長（最多 255 個字符）";
                return false;
            }

            return true;
        }

        private bool ValidateMimeType(string? contentType, FileValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                result.Warnings.Add("無法檢測檔案的 MIME 類型，將基於檔案內容進行進一步驗證");
                return true; // 允許繼續，但會在內容驗證中檢查
            }

            if (!AllowedMimeTypes.Contains(contentType))
            {
                result.IsValid = false;
                result.ErrorMessage = $"不支援的檔案類型: {contentType}。只允許 CSV 檔案";
                return false;
            }

            return true;
        }

        private bool ValidateFileSize(long fileSize, FileValidationResult result)
        {
            if (fileSize > MaxFileSize)
            {
                result.IsValid = false;
                result.ErrorMessage = $"檔案大小超過限制（最大 {MaxFileSize / (1024 * 1024)} MB）";
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateFileContentAsync(IFormFile file, FileValidationResult result)
        {
            try
            {
                using var stream = file.OpenReadStream();
                
                // 檢查檔案頭部（Magic Bytes）
                var buffer = new byte[1024];
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                
                if (bytesRead == 0)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "檔案內容為空";
                    return false;
                }

                // 重置流位置
                stream.Position = 0;

                // 嘗試讀取並驗證 CSV 內容
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                
                var lineCount = 0;
                var columnCount = 0;
                var hasHeader = false;

                while (!reader.EndOfStream && lineCount < 10) // 只檢查前 10 行來驗證格式
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    lineCount++;

                    // 檢查是否包含危險內容
                    if (ContainsDangerousContent(line))
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"檔案內容包含潛在危險字符（第 {lineCount} 行）";
                        return false;
                    }

                    // 解析 CSV 行
                    var columns = ParseCsvLine(line);
                    
                    if (lineCount == 1)
                    {
                        columnCount = columns.Length;
                        hasHeader = true;

                        // 檢查欄位數量
                        if (columnCount > MaxColumnCount)
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"欄位數量超過限制（最大 {MaxColumnCount} 個欄位）";
                            return false;
                        }

                        // 檢查標題行是否合理
                        if (!ValidateHeaderRow(columns, result))
                            return false;
                    }
                    else
                    {
                        // 檢查資料行欄位數量是否一致
                        if (columns.Length != columnCount)
                        {
                            result.Warnings.Add($"第 {lineCount} 行的欄位數量與標題行不一致");
                        }
                    }
                }

                // 快速估算總行數（用於檢查是否超過限制）
                stream.Position = 0;
                var estimatedRowCount = await EstimateRowCountAsync(stream);
                if (estimatedRowCount > MaxRowCount)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"檔案行數超過限制（最大 {MaxRowCount} 行）";
                    return false;
                }

                if (!hasHeader)
                {
                    result.Warnings.Add("檔案似乎沒有標題行");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "驗證檔案內容時發生錯誤");
                result.IsValid = false;
                result.ErrorMessage = "檔案內容格式無效或損壞";
                return false;
            }
        }

        private bool ContainsDangerousContent(string line)
        {
            // 檢查 SQL 注入模式
            var sqlPatterns = new[]
            {
                "drop table", "delete from", "insert into", "update set", "create table",
                "exec ", "execute ", "xp_", "sp_", "union select", "' or ", "\" or ",
                "'; drop", "\"; drop", "/*", "*/", "--", "@@"
            };

            // 檢查 XSS 模式
            var xssPatterns = new[]
            {
                "<script", "</script>", "javascript:", "vbscript:", "onload=", "onerror=", 
                "onclick=", "onmouseover=", "onfocus=", "onblur=", "expression(",
                "<iframe", "<object", "<embed", "<link", "<meta"
            };

            var lowerLine = line.ToLowerInvariant();

            return sqlPatterns.Any(pattern => lowerLine.Contains(pattern)) ||
                   xssPatterns.Any(pattern => lowerLine.Contains(pattern));
        }

        private string[] ParseCsvLine(string line)
        {
            // 簡單的 CSV 解析器（處理基本的逗號分隔和引號）
            var result = new List<string>();
            var inQuotes = false;
            var currentField = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // 雙引號轉義
                        currentField.Append('"');
                        i++; // 跳過下一個引號
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            result.Add(currentField.ToString());
            return result.ToArray();
        }

        private bool ValidateHeaderRow(string[] headers, FileValidationResult result)
        {
            // 檢查標題是否為空
            if (headers.All(string.IsNullOrWhiteSpace))
            {
                result.IsValid = false;
                result.ErrorMessage = "CSV 檔案的標題行不能全部為空";
                return false;
            }

            // 檢查是否有重複的標題
            var duplicates = headers
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .GroupBy(h => h.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Any())
            {
                result.Warnings.Add($"發現重複的欄位標題: {string.Join(", ", duplicates)}");
            }

            // 檢查標題長度
            var longHeaders = headers
                .Where(h => !string.IsNullOrWhiteSpace(h) && h.Length > 100)
                .ToList();

            if (longHeaders.Any())
            {
                result.Warnings.Add("部分欄位標題過長（建議少於 100 個字符）");
            }

            return true;
        }

        private async Task<int> EstimateRowCountAsync(Stream stream)
        {
            const int sampleSize = 10240; // 讀取 10KB 來估算
            var buffer = new byte[sampleSize];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            
            if (bytesRead == 0) return 0;

            var sampleText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var lineCount = sampleText.Count(c => c == '\n');
            
            if (bytesRead < sampleSize)
            {
                // 檔案很小，直接返回實際行數
                return lineCount;
            }

            // 估算總行數
            var avgBytesPerLine = bytesRead / Math.Max(lineCount, 1);
            var estimatedTotalRows = (int)(stream.Length / avgBytesPerLine);
            
            return estimatedTotalRows;
        }
    }
}
