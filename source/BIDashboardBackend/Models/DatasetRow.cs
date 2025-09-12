using System.ComponentModel.DataAnnotations.Schema;

namespace BIDashboardBackend.Models
{
    [Table("dataset_rows")]
    public class DatasetRow
    {
        public long Id { get; set; }
        public long BatchId { get; set; }

        // 這裡用 string 來承接 jsonb；需要操作時再以 System.Text.Json 解析
        public string RowJson { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }
}
