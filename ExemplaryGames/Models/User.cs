using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ExemplaryGames.Models
{
    public class User
    {
        /*
         * Lines 13-25
         * This is used to communicate with SQL to create a User table with the following columns
         * string.Empty is used to avoid null reference issues
         * It is similar to what we did in Mobile App Development with SQLite
        */
        public int Id { get; set; } //Primary key

        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;


        // Navigation property for related Game entities
        // SQL is bidirecitonal, so we need to specify both ends of the relationship
        public ICollection<Game> GamesForSale { get; set; } = new List<Game>();
        public ICollection<Offer> Offers { get; set; } = new List<Offer>();

    }
}
