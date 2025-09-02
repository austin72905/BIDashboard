using BIDashboardBackend.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace BIDashboardBackend.Features.Ingest
{
    // 輔助工具
    //  探測 CSV 欄位與初步類型（可只做前 N 行），也可延後到 ETL 做強化檢測。
    public sealed class CsvSniffer
    {
        public async Task<(long totalRows, List<DatasetColumn> columns)> ProbeAsync(Stream csv, long batchId)
        {
            
            using var reader = new StreamReader(csv);
            using var csvReader = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            });

            // 讀 header
            await csvReader.ReadAsync();
            csvReader.ReadHeader();
            var headers = csvReader.HeaderRecord ?? Array.Empty<string>();





            // 初始化每個欄位的樣本集合
            var samples = headers.ToDictionary(h => h, h => new List<string>());

            int sampleLimit = 20;
            int sampleCount = 0;

            long totalRows = 0;



            

            // 讀取資料列
            while (await csvReader.ReadAsync())
            {
                totalRows++;
                foreach (var h in headers)
                {
                    var val = csvReader.GetField(h);
                    if (!string.IsNullOrWhiteSpace(val) && sampleCount < sampleLimit)
                    {
                        samples[h].Add(val);
                    }
                }

                if (sampleCount < sampleLimit) sampleCount++;
            }



            // 型別判斷
            var columns = new List<DatasetColumn>();
            foreach (var h in headers)
            {
                string detectedType = "text";
                var vals = samples[h];

                if (vals.Count > 0)
                {
                    if (vals.All(v => decimal.TryParse(v, out _)))
                    {
                        detectedType = "number";
                    }
                    else if (vals.All(v => DateTime.TryParse(v, out _)))
                    {
                        detectedType = "date";
                    }
                    else if (vals.Any(v => v.Contains("@")))
                    {
                        detectedType = "email";
                    }
                }

                columns.Add(new DatasetColumn
                {
                    BatchId = batchId,
                    SourceName = h,
                    DataType = detectedType
                });
            }

            return (totalRows, columns);
        }
    }
}
