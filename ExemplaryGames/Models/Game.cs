using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExemplaryGames.Models
{
    public class Game
    {
        /*
         * Lines 14-45
         * This is used to communicate with SQL to create a Game table with the following columns
         * string.Empty is used to avoid null reference issues
         * It is similar to what we did in Mobile App Development with SQLite
        */
        public int Id { get; set; } // Primary key

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        //Seller Foreign Key -> User
        public int SellerId { get; set; } //Refers to User Id column
        public User? Seller { get; set; } //The user associated with this game if there is one

        [Required]
        [Column(TypeName = "decimal(10,2)")] //Create a SQL column with the type decimal(10,2)
        [Range(0.01, 9999999999.99)] //value allowed for price between 0.01 and 10 billion
        public decimal Price { get; set; }

        [Required]
        public GameCondition Condition { get; set; }

        [Required]
        public string Details { get; set; } = string.Empty;

        //[Required]
        public string ImagePath { get; set; } = string.Empty;

        [Required]
        public bool Active { get; set; } = true;

        [Required]
        public int TotalOffers { get; set; } = 0;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal MaxOffer { get; set; } = 0m;

        //Navigation
        public ICollection<Offer> Offers { get; set; } = new List<Offer>();

    }
}
