using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace BIDashboardBackend.Services
{
    /// <summary>
    /// 資料清理和防護服務，提供 SQL 注入和 XSS 防護
    /// </summary>
    public interface IDataSanitizationService
    {
        /// <summary>
        /// 清理 CSV 資料，移除潛在的危險內容
        /// </summary>
        /// <param name="csvData">原始 CSV 資料</param>
        /// <returns>清理後的 CSV 資料</returns>
        Task<string> SanitizeCsvDataAsync(string csvData);

        /// <summary>
        /// 清理單個欄位值
        /// </summary>
        /// <param name="fieldValue">欄位值</param>
        /// <returns>清理後的欄位值</returns>
        string SanitizeFieldValue(string fieldValue);

        /// <summary>
        /// 驗證欄位名稱是否安全
        /// </summary>
        /// <param name="columnName">欄位名稱</param>
        /// <returns>是否安全</returns>
        bool IsColumnNameSafe(string columnName);

        /// <summary>
        /// 清理欄位名稱
        /// </summary>
        /// <param name="columnName">原始欄位名稱</param>
        /// <returns>清理後的欄位名稱</returns>
        string SanitizeColumnName(string columnName);
    }

    public class DataSanitizationService : IDataSanitizationService
    {
        private readonly ILogger<DataSanitizationService> _logger;

        // SQL 注入關鍵字（不區分大小寫）
        private static readonly HashSet<string> SqlKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "EXEC", "EXECUTE",
            "UNION", "OR", "AND", "WHERE", "FROM", "INTO", "SET", "VALUES", "TABLE", "DATABASE",
            "TRUNCATE", "GRANT", "REVOKE", "DECLARE", "CAST", "CONVERT", "SUBSTRING", "CHAR",
            "ASCII", "WAITFOR", "DELAY", "XP_", "SP_", "OPENROWSET", "OPENDATASOURCE"
        };

        // SQL 注入模式
        private static readonly Regex[] SqlInjectionPatterns = new[]
        {
            new Regex(@"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION)\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"('|('')|(\b(OR|AND)\b.*(=|LIKE))|(--)|(/\*)|(\*/)|(\bUNION\b))", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"(\b(XP_|SP_)\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"(@@\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"(\bWAITFOR\b|\bDELAY\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        // XSS 模式
        private static readonly Regex[] XssPatterns = new[]
        {
            new Regex(@"<\s*script[^>]*>.*?</\s*script\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline),
            new Regex(@"<\s*(iframe|object|embed|applet|meta|link)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"javascript\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"vbscript\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"expression\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"<\s*svg[^>]*>.*?</\s*svg\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline)
        };

        // 允許的欄位名稱字符
        private static readonly Regex ValidColumnNamePattern = new Regex(@"^[a-zA-Z0-9_\u4e00-\u9fff\s\-\.]+$", RegexOptions.Compiled);

        public DataSanitizationService(ILogger<DataSanitizationService> logger)
        {
            _logger = logger;
        }

        public async Task<string> SanitizeCsvDataAsync(string csvData)
        {
            if (string.IsNullOrWhiteSpace(csvData))
                return string.Empty;

            try
            {
                var lines = csvData.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                var sanitizedLines = new List<string>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        sanitizedLines.Add(line);
                        continue;
                    }

                    var sanitizedLine = await SanitizeCsvLineAsync(line);
                    sanitizedLines.Add(sanitizedLine);
                }

                return string.Join("\n", sanitizedLines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理 CSV 資料時發生錯誤");
                throw new InvalidOperationException("資料清理失敗", ex);
            }
        }

        private async Task<string> SanitizeCsvLineAsync(string line)
        {
            var fields = ParseCsvLine(line);
            var sanitizedFields = new List<string>();

            foreach (var field in fields)
            {
                var sanitizedField = SanitizeFieldValue(field);
                sanitizedFields.Add(sanitizedField);
            }

            return string.Join(",", sanitizedFields.Select(EscapeCsvField));
        }

        public string SanitizeFieldValue(string fieldValue)
        {
            if (string.IsNullOrWhiteSpace(fieldValue))
                return fieldValue ?? string.Empty;

            var sanitized = fieldValue;

            try
            {
                // 1. 移除潛在的 SQL 注入攻擊
                sanitized = RemoveSqlInjection(sanitized);

                // 2. 移除潛在的 XSS 攻擊
                sanitized = RemoveXss(sanitized);

                // 3. 移除危險字符
                sanitized = RemoveDangerousCharacters(sanitized);

                // 4. 長度限制
                if (sanitized.Length > 1000)
                {
                    sanitized = sanitized.Substring(0, 1000);
                    _logger.LogWarning("欄位值過長，已截斷至 1000 字符");
                }

                return sanitized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理欄位值時發生錯誤: {FieldValue}", fieldValue);
                // 如果清理失敗，返回安全的空字符串
                return string.Empty;
            }
        }

        public bool IsColumnNameSafe(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return false;

            // 檢查長度
            if (columnName.Length > 64)
                return false;

            // 檢查是否包含有效字符
            if (!ValidColumnNamePattern.IsMatch(columnName))
                return false;

            // 檢查是否是 SQL 關鍵字
            if (SqlKeywords.Contains(columnName.Trim()))
                return false;

            // 檢查是否包含 SQL 注入模式
            if (SqlInjectionPatterns.Any(pattern => pattern.IsMatch(columnName)))
                return false;

            return true;
        }

        public string SanitizeColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return "Column";

            var sanitized = columnName.Trim();

            // 移除危險字符，只保留字母、數字、底線、中文字符、空格、橫線和點
            sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9_\u4e00-\u9fff\s\-\.]", "");

            // 如果是 SQL 關鍵字，添加前綴
            if (SqlKeywords.Contains(sanitized))
            {
                sanitized = $"Col_{sanitized}";
            }

            // 長度限制
            if (sanitized.Length > 64)
            {
                sanitized = sanitized.Substring(0, 64);
            }

            // 如果清理後為空，提供預設名稱
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "Column";
            }

            return sanitized;
        }

        private string RemoveSqlInjection(string input)
        {
            var result = input;

            // 移除 SQL 注入模式
            foreach (var pattern in SqlInjectionPatterns)
            {
                result = pattern.Replace(result, "");
            }

            // 移除單引號和雙引號的組合攻擊
            result = Regex.Replace(result, @"['""](\s*(OR|AND)\s*['""]?\w+['""]?\s*=|--)", "", RegexOptions.IgnoreCase);

            // 移除註釋符號
            result = result.Replace("--", "").Replace("/*", "").Replace("*/", "");

            return result;
        }

        private string RemoveXss(string input)
        {
            var result = input;

            // 移除 XSS 模式
            foreach (var pattern in XssPatterns)
            {
                result = pattern.Replace(result, "");
            }

            // HTML 編碼特殊字符
            result = HttpUtility.HtmlEncode(result);

            return result;
        }

        private string RemoveDangerousCharacters(string input)
        {
            var result = input;

            // 移除控制字符（除了常見的空白字符）
            result = Regex.Replace(result, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

            // 移除可能導致問題的 Unicode 字符
            result = Regex.Replace(result, @"[\uFEFF\u200B-\u200D\uFE00-\uFE0F]", "");

            return result;
        }

        private string[] ParseCsvLine(string line)
        {
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
                        currentField.Append('"');
                        i++;
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

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return field;

            // 如果欄位包含逗號、引號或換行符，需要用引號包圍
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                // 將欄位中的引號轉義為雙引號
                var escaped = field.Replace("\"", "\"\"");
                return $"\"{escaped}\"";
            }

            return field;
        }
    }
}
