using BIDashboardBackend.Models;

namespace BIDashboardBackend.Features.Ingest
{
    // 輔助工具
    //  探測 CSV 欄位與初步類型（可只做前 N 行），也可延後到 ETL 做強化檢測。
    public sealed class CsvSniffer
    {
        public async Task<(long totalRows, List<DatasetColumn> columns)> ProbeAsync(Stream csv, long batchId)
        {
            // TODO: 讀前 N 行推測欄位/型別；這裡先回傳空集合
            await Task.CompletedTask;
            return (0L, new List<DatasetColumn>());
        }
    }
}
