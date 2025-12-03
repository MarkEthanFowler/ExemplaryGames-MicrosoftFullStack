using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace ExemplaryGames.Models
{
    public class Offer
    {
        /*
         * Lines 15-32
         * This is used to communicate with SQL to create a Offer table with the following columns
         * string.Empty is used to avoid null reference issues
         * It is similar to what we did in Mobile App Development with SQLite
        */
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        [Range(0.01, 9999999999.99)]
        public decimal Amount { get; set; }

        [Required]
        public OfferStatus Status { get; set; } = OfferStatus.Pending; //enum model from Enums.cs

        // Buyer Foreign Key -> User
        [Required]
        public int BuyerId { get; set; } //Refers to User Id column
        public User? Buyer { get; set; } //The user associated with this offer if there is one

        // Game Foreign Key -> Game
        public int GameId { get; set; } //Refers to Game Id column
        public Game? Game { get; set; } //The game associated with this offer if there is one
    }
}
