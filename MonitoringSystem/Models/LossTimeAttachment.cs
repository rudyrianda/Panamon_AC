using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MonitoringSystem.Models
{
    public class LossTimeAttachment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string RecordSource { get; set; } // "Assembly"

        [Required]
        public int RecordId { get; set; }

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; }

        [Required]
        [MaxLength(255)]
        public string SavedFileName { get; set; }

        [Required]
        [MaxLength(1000)]
        public string FilePath { get; set; }

        [Required]
        public DateTime UploadedAt { get; set; }
    }
}
