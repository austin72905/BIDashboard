using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Models;
using System.Text;
using System.Text.Json;

namespace BIDashboardBackend.Repositories
{
    public sealed class DatasetRepository : IDatasetRepository
    {
        private readonly ISqlRunner _sql;

        public DatasetRepository(ISqlRunner sql) => _sql = sql;

        public async Task<long> CreateDatasetAsync(string name, long? ownerId = null)
        {
            const string sql = @"
                INSERT INTO datasets (name, owner_id, created_at, updated_at)
                VALUES (@name, @ownerId, NOW(), NOW())
                RETURNING id;";
            var id = await _sql.ScalarAsync<long>(sql, new { name, ownerId });
            return id;
        }

        public async Task<long> CreateBatchAsync(long datasetId, string fileName, long totalRows)
        {
            const string sql = @"
                INSERT INTO dataset_batches (dataset_id, source_filename, total_rows, status, created_at, updated_at)
                VALUES (@datasetId, @fileName, @totalRows, 'Pending', NOW(), NOW())
                RETURNING id;";
            var id = await _sql.ScalarAsync<long>(sql, new { datasetId, fileName, totalRows });
            return id;
        }

        public Task<int> SetBatchStatusAsync(long batchId, string status, string? errorMessage)
        {
            const string sql = @"
                UPDATE dataset_batches
                SET status = @status,
                    error_message = @errorMessage,
                    updated_at = NOW()
                WHERE id = @batchId;";
            return _sql.ExecAsync(sql, new { batchId, status, errorMessage });
        }

        public async Task<int> UpsertColumnsAsync(long batchId, IEnumerable<DatasetColumn> columns)
        {
            // 以 (batch_id, source_name) 唯一鍵做 UPSERT
            // 這裡用 UNNEST 一次送陣列，減少 round-trip
            var srcNames = new List<string>();
            var dataTypes = new List<string?>();
            var samples = new List<string?>();

            foreach (var c in columns)
            {
                srcNames.Add(c.SourceName);
                dataTypes.Add(c.DataType);
                samples.Add(c.SampleValue);
            }

            if (srcNames.Count == 0) return 0;

            const string sql = @"
                WITH incoming AS (
                  SELECT
                    @batchId::bigint                                AS batch_id,
                    t.src_name::text                                AS source_name,
                    t.dt::text                                      AS data_type,
                    t.sample::text                                  AS sample_value
                  FROM UNNEST(@src_names::text[], @data_types::text[], @samples::text[]) 
                       AS t(src_name, dt, sample)
                )
                INSERT INTO dataset_columns (batch_id, source_name, data_type, sample_value, created_at, updated_at)
                SELECT batch_id, source_name, data_type, sample_value, NOW(), NOW()
                FROM incoming
                ON CONFLICT (batch_id, source_name) DO UPDATE
                SET data_type    = EXCLUDED.data_type,
                    sample_value = EXCLUDED.sample_value,
                    updated_at   = NOW();";

            return await _sql.ExecAsync(sql, new
            {
                batchId,
                src_names = srcNames.ToArray(),

                data_types = dataTypes.ToArray(),
                samples = samples.ToArray()
            });
        }

        public async Task<int> UpsertMappingsAsync(long batchId, IEnumerable<DatasetMapping> mappings)
        {
            // 以 (batch_id, source_column) 為唯一鍵做映射資料的更新或刪除
            // 若 systemField = -1 (表示未映射) 則刪除該筆資料
            // 這裡使用 UNNEST 一次傳入陣列，降低與資料庫往返的次數
            var srcColumns = new List<string>();
            var sysFields = new List<int>(); // enum -> int

            foreach (var m in mappings)
            {
                srcColumns.Add(m.SourceColumn);
                sysFields.Add((int)m.SystemField);
            }

            if (srcColumns.Count == 0) return 0;

            const string sql = @"
                WITH incoming AS (
                  SELECT
                    @batchId::bigint AS batch_id,
                    t.src_col::text  AS source_column,
                    t.sys_f::int     AS system_field
                  FROM UNNEST(@src_columns::text[], @sys_fields::int[]) AS t(src_col, sys_f)
                ),
                to_delete AS (
                  DELETE FROM dataset_mappings dm
                  USING incoming i
                  WHERE dm.batch_id = i.batch_id
                    AND dm.source_column = i.source_column
                    AND i.system_field = -1
                  RETURNING dm.*
                )
                INSERT INTO dataset_mappings (batch_id, source_column, system_field, created_at, updated_at)
                SELECT i.batch_id, i.source_column, i.system_field, NOW(), NOW()
                FROM incoming i
                WHERE i.system_field <> -1
                ON CONFLICT (batch_id, source_column) DO UPDATE
                SET system_field = EXCLUDED.system_field,
                    updated_at   = NOW();";

            // 執行批次映射資料的新增、更新或刪除
            return await _sql.ExecAsync(sql, new
            {
                batchId,
                src_columns = srcColumns.ToArray(),
                sys_fields = sysFields.ToArray()
            });
        }

        public Task<IReadOnlyList<DatasetColumn>> GetColumnsAsync(long batchId)
        {
            const string sql = @"
                SELECT id, batch_id AS BatchId, source_name AS SourceName, 
                       data_type AS DataType, sample_value AS SampleValue, 
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM dataset_columns
                WHERE batch_id = @batchId
                ORDER BY source_name;";
            return _sql.QueryAsync<DatasetColumn>(sql, new { batchId });
        }

        public Task<IReadOnlyList<DatasetColumnWithMapping>> GetColumnsWithMappingAsync(long batchId)
        {
            const string sql = @"
                SELECT 
                    dc.id AS Id,
                    dc.batch_id AS BatchId,
                    dc.source_name AS SourceName,
                    dc.data_type AS DataType,
                    dc.sample_value AS SampleValue,
                    dc.created_at AS CreatedAt,
                    dc.updated_at AS UpdatedAt,
                    dm.system_field AS MappedSystemField,
                    dm.id AS MappingId,
                    dm.created_at AS MappingCreatedAt
                FROM dataset_columns dc
                LEFT JOIN dataset_mappings dm ON dc.batch_id = dm.batch_id 
                    AND dc.source_name = dm.source_column
                WHERE dc.batch_id = @batchId
                ORDER BY dc.source_name;";
            return _sql.QueryAsync<DatasetColumnWithMapping>(sql, new { batchId });
        }

        public async Task<HashSet<string>> GetAvailableSourceColumnsAsync(long batchId)
        {
            const string sql = @"
                SELECT source_name
                FROM dataset_columns
                WHERE batch_id = @batchId;";
            var columns = await _sql.QueryAsync<string>(sql, new { batchId });
            return new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<DatasetBatch?> GetBatchAsync(long batchId)
        {
            const string sql = @"
                SELECT id AS Id, dataset_id AS DatasetId, source_filename AS SourceFilename, 
                       total_rows AS TotalRows, status AS Status, error_message AS ErrorMessage,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM dataset_batches
                WHERE id = @batchId;";
            var batches = await _sql.QueryAsync<DatasetBatch>(sql, new { batchId });
            return batches.FirstOrDefault();
        }

        public async Task<long> BulkCopyRowsAsync(long batchId, Stream csvStream, CancellationToken ct)
        {
            // 讀 header
            using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var header = await reader.ReadLineAsync();
            if (header is null) return 0;

            var headers = ParseCsv(header);
            const int batchSize = 1000;

            var dataJsonList = new List<string>(batchSize);

            long inserted = 0;

            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync();
                if (line is null) break;

                var cells = ParseCsv(line);
                var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Count; i++)
                {
                    obj[headers[i]] = i < cells.Count && cells[i].Length > 0 ? cells[i] : null;
                }

                dataJsonList.Add(JsonSerializer.Serialize(obj));

                if (dataJsonList.Count >= batchSize)
                {
                    inserted += await FlushRowsAsync(batchId, dataJsonList);
                    dataJsonList.Clear();
                }
            }

            if (dataJsonList.Count > 0)
            {
                inserted += await FlushRowsAsync(batchId, dataJsonList);
            }

            return inserted;
        }

        public async Task<string[]> GetAffectedMetricKeysAsync(long datasetId)
        {
            const string sql = @"
SELECT DISTINCT m.system_field
FROM dataset_mappings m
WHERE m.batch_id = @datasetId
ORDER BY m.system_field;";
            var rows = await _sql.QueryAsync<string>(sql, new { datasetId });
            return rows.Count == 0 ? Array.Empty<string>() : rows as string[] ?? rows.ToArray();
        }

        // --- helpers ---

        private Task<int> FlushRowsAsync(long batchId, List<string> dataJson)
        {
            // 用 UNNEST 批次插入，避免 1 行 1 次的 round-trip
            const string sql = @"
INSERT INTO dataset_rows (batch_id, row_json, created_at)
SELECT @batchId::bigint, t.js::jsonb, NOW()
FROM UNNEST(@jsons::text[]) AS t(js);";

            return _sql.ExecAsync(sql, new
            {
                batchId,
                jsons = dataJson.ToArray()
            });
        }

        private static List<string> ParseCsv(string line)
        {
            var res = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        sb.Append('\"'); // 轉義 "" -> "
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    res.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }
            res.Add(sb.ToString());
            return res;
        }

        public async Task<IReadOnlyList<UploadHistoryDto>> GetUploadHistoryAsync(long userId, int limit = 50, int offset = 0)
        {
            const string sql = @"
                SELECT 
                    db.id AS BatchId,
                    db.dataset_id AS DatasetId,
                    d.name AS DatasetName,
                    db.source_filename AS SourceFilename,
                    db.total_rows AS TotalRows,
                    db.status AS Status,
                    db.error_message AS ErrorMessage,
                    db.created_at AS CreatedAt,
                    db.updated_at AS UpdatedAt
                FROM dataset_batches db
                INNER JOIN datasets d ON db.dataset_id = d.id
                WHERE d.owner_id = @userId
                ORDER BY db.created_at DESC
                LIMIT @limit OFFSET @offset;";

            var batches = await _sql.QueryAsync<UploadHistoryDto>(sql, new { userId, limit, offset });
            
            // 為每個批次獲取欄位資訊
            var result = new List<UploadHistoryDto>();
            foreach (var batch in batches)
            {
                var columns = await GetBatchColumnsWithMappingAsync(batch.BatchId);
                batch.Columns = columns;
                result.Add(batch);
            }
            
            return result;
        }

        public async Task<UploadHistoryDto?> GetBatchDetailsAsync(long batchId, long userId)
        {
            const string sql = @"
                SELECT 
                    db.id AS BatchId,
                    db.dataset_id AS DatasetId,
                    d.name AS DatasetName,
                    db.source_filename AS SourceFilename,
                    db.total_rows AS TotalRows,
                    db.status AS Status,
                    db.error_message AS ErrorMessage,
                    db.created_at AS CreatedAt,
                    db.updated_at AS UpdatedAt
                FROM dataset_batches db
                INNER JOIN datasets d ON db.dataset_id = d.id
                WHERE db.id = @batchId AND d.owner_id = @userId;";

            var batch = await _sql.FirstOrDefaultAsync<UploadHistoryDto>(sql, new { batchId, userId });
            
            if (batch == null)
                return null;

            // 獲取欄位資訊
            var columns = await GetBatchColumnsWithMappingAsync(batchId);
            batch.Columns = columns;
            return batch;
        }

        /// <summary>
        /// 取得指定用戶可使用的所有資料集 ID
        /// </summary>
        /// <param name="userId">用戶 ID</param>
        /// <returns>資料集 ID 列表</returns>
        public Task<IReadOnlyList<long>> GetDatasetIdsByUserAsync(long userId)
        {
            const string sql = @"
                SELECT id
                FROM datasets
                WHERE owner_id = @userId
                ORDER BY id;";

            return _sql.QueryAsync<long>(sql, new { userId });
        }

        private async Task<IReadOnlyList<UploadHistoryColumnDto>> GetBatchColumnsWithMappingAsync(long batchId)
        {
            const string sql = @"
                SELECT 
                    dc.source_name AS SourceName,
                    dc.data_type AS DataType,
                    false AS IsNullable,
                    dm.system_field::text AS SystemField
                FROM dataset_columns dc
                LEFT JOIN dataset_mappings dm ON dc.batch_id = dm.batch_id AND dc.source_name = dm.source_column
                WHERE dc.batch_id = @batchId
                ORDER BY dc.source_name;";

            var columns = await _sql.QueryAsync<UploadHistoryColumnDto>(sql, new { batchId });
            return columns.ToList();
        }

    }

}