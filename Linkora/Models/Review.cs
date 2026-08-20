using Linkora.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Review
{
    [Key]
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public int TargetUserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ProductId { get; set; }

    [ForeignKey("AuthorId")]
    public virtual User Author { get; set; }
    [ForeignKey("TargetUserId")]
    public virtual User TargetUser { get; set; }
    [ForeignKey("ProductId")]
    public virtual Product Product { get; set; }
}