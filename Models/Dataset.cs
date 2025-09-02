using System.ComponentModel.DataAnnotations.Schema;

namespace BIDashboardBackend.Models
{
    // datasets（邏輯資料集，一個 dataset 可包含多個 batch）
    [Table("datasets")]
    public sealed class Dataset
    {
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public long? OwnerId { get; init; }                        // FK -> users.id（可為 null）
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
