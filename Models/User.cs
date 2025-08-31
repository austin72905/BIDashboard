using System.ComponentModel.DataAnnotations.Schema;

namespace BIDashboardBackend.Models
{
    [Table("Users")]
    public class User
    {
        public int Id { get; set; }

        public string Email { get; set; }

        public string DisplayName { get; set; }

        // firebase id
        public string Uid { get; set; }


        public DateTime LastLoginAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
