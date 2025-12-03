using Microsoft.EntityFrameworkCore;

namespace ExemplaryGames.Models
{
    // This class replaces app.js from the previous Node.js project
    // It is used to configure the database context for Entity Framework Core
    public class AppDbContext : DbContext
    {
        // Constructor that accepts DbContextOptions and passes them to the base DbContext class
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)//base options are the configuration options passed to the parent DbContext class
        {

        }

        public DbSet<User> Users { get; set; } //create the SQL Users table mapping to the User model
        public DbSet<Game> Games { get; set; } //create the SQL Games table mapping to the Game model
        public DbSet<Offer> Offers { get; set; } //create the SQL Offers table mapping to the Offer model


        //Configure relationships, constraints, indexes, and delete rules
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);//lets default EF core logic run

            // Unique Email
            // CREATE UNIQUE INDEX IX_Users_Email ON Users (Email)
            modelBuilder.Entity<User>()
                .HasIndex(user => user.Email)//Create a database index on the Email column, makes searches faster
                .IsUnique();//no duplicates allowed

            // User 1 - many Games
            modelBuilder.Entity<Game>()
                .HasOne(game => game.Seller)//A Game has ONE Seller
                .WithMany(user => user.GamesForSale)//A User has MANY games
                .HasForeignKey(game => game.SellerId)//Use SellerId as the foreign key
                .OnDelete(DeleteBehavior.Restrict);//SQL will NOT let you delete a User if they still have games

            // User 1 - many Offers
            modelBuilder.Entity<Offer>()
                .HasOne(offer => offer.Buyer)//A offer has ONE buyer
                .WithMany(user => user.Offers)//A User has MANY offers
                .HasForeignKey(offer => offer.BuyerId)//Each Offer has exactly one Buyer
                .OnDelete(DeleteBehavior.Restrict);//you can’t delete a user if they have offers

            // Game 1 - many Offers
            modelBuilder.Entity<Offer>()
                .HasOne(offer => offer.Game)//An Offer belongs to one Game
                .WithMany(game => game.Offers)//A Game can have many Offers
                .HasForeignKey(offer => offer.GameId)//Use GameId as the foreign key
                .OnDelete(DeleteBehavior.Cascade);//If a Game is deleted → all its related Offers are deleted too
        }
    }
}
