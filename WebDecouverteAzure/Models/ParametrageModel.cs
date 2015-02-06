using System.ComponentModel.DataAnnotations;

namespace WebDecouverteAzure.Models
{
    public class ParametrageModel
    {
        [Required(ErrorMessage = "*")]
        [Range(5, 18000, ErrorMessage = "La durée doit être comprise entre 5s et 5 heures")]
        public int Duration { get; set; }

        public string Result { get; set; }
    }
}
