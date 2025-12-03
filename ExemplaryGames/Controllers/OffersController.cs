using ExemplaryGames.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ExemplaryGames.Controllers
{
    [Authorize] // All of the actions in this controller require the user to be logged in so we put this at class level
    public class OffersController : Controller
    {
        private readonly AppDbContext context;

        public OffersController(AppDbContext context)
        {
            this.context = context;
        }

        //Helper method to get the cuurent logged in users id from claims
        private int? GetCurrentUserId()//nullable
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);//grab the users id from claims

            //try to convert it to a integer and store in userId if successful
            if(int.TryParse(userIdString, out int userId))
            {
                //return the userId
                return userId;
            }
            //if it fails return null
            return null;
        }

        //GET: Offers?gameId=#
        public async Task<IActionResult> Index(int gameId)
        {
            var currentUserId = GetCurrentUserId();//get the current users id with the helper method
            if(currentUserId == null)//if it was not found
            {
                //redirect to login
                return RedirectToAction("Login", "Users");
            }

            //load the game and its offers and each offers buyer
            var game = await context.Games//query the Games Database
                .Include(game => game.Offers)//get the offers for this game
                .ThenInclude(offer => offer.Buyer)//get the buyer for this game
                .FirstOrDefaultAsync(game => game.Id == gameId);//execute the query returning the first game whos id matches gameId

            if(game == null)//if not found in the database
            {
                return NotFound(); //return 404 error
            }

            //ownership check only the seller may see offers for this game
            if(game.SellerId != currentUserId.Value)//.Value is safe since we already checked if null
            {
                //throw error
                TempData["ErrorMessage"] = "You are not allowed to view offers for this listing";
                //redirect to the profile page
                return RedirectToAction("Profile", "Users");
            }

            //pass the game as we go to offers index page
            return View(game);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(int gameId, decimal amount)
        {
            var currentUserId = GetCurrentUserId();//get the current users id with the helper method
            if (currentUserId == null)//if it was not found
            {
                if(IsAjaxRequest())
                {
                    return Unauthorized(new { message = "You must be logged in to make an offer." });
                }

                //redirect to login page
                return RedirectToAction("Login", "Users");
            }

            //load the game being offered on
            var game = await context.Games//query the game database
                .Include(game => game.Seller)//get the seller associated with the game
                .FirstOrDefaultAsync(game => game.Id == gameId);//execute the query returning the first game whos id matches gameId

            if (game == null)//if not found in the database
            {
                if(IsAjaxRequest())
                {
                    return NotFound(new { message = $"Game not found. {gameId}" });
                }

                //throw error
                TempData["ErrorMessage"] = $"Game not found. {gameId}";
                //redirect to the browse page
                return RedirectToAction("Index", "Games");
            }

            //prevent seller from making an offer on their own game
            if(game.SellerId == currentUserId.Value)
            {
                //throw error
                TempData["ErrorMessage"] = "You cannot make an offer on your own listing";
                //redirect to details page with the game id
                return RedirectToAction("Details", "Games", new { id = gameId });
            }

            //prevent offers on inactive games
            if(!game.Active)
            {
                //throw error
                TempData["ErrorMessage"] = "This game is no longer accepting offers.";
                //redirect to details page with the game id
                return RedirectToAction("Details", "Games", new { id = gameId });
            }

            //validation on the passed in amount
            if(amount <= 0)
            {
                //throw error
                TempData["ErrorMessage"] = "Offer amount must be greater than zero";
                //redirect to details page with the game id
                return RedirectToAction("Details", "Games", new { id = gameId });
            }

            //create a new offer entity
            var offer = new Offer
            {
                GameId = gameId,
                BuyerId = currentUserId.Value,
                Amount = amount,
                Status = OfferStatus.Pending
            };

            context.Offers.Add(offer);

            //increment the total offers field for the database and fields in the html pages
            game.TotalOffers += 1;

            //if the amount is greater than the max offer
            if(amount > game.MaxOffer)
            {
                //set this as the new max offer
                game.MaxOffer = amount;
            }

            await context.SaveChangesAsync();

            if(IsAjaxRequest())
            {
                return Json(new
                {
                    //offerForm.js
                    message = "Your offer has been submitted.",//$("#offerResult").text(result.message);
                    totalOffers = game.TotalOffers,//$("#totalOffersValue").text(result.totalOffers);
                    maxOffer = game.MaxOffer//$("#maxOfferValue").text(result.maxOffer);
                });
            }

            //success message for flash messages
            TempData["SuccessMessage"] = "Your offer has been submitted.";

            //redirect to details page with the game id 
            return RedirectToAction("Details", "Games", new { id = gameId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Accept(int offerId)
        {
            var currentUserId = GetCurrentUserId();//get the current users id with the helper method
            if (currentUserId == null)//if it was not found
            {
                //redirect to login page
                return RedirectToAction("Login", "Users");
            }

            //load the offer and its game so we can check ownership
            var offer = await context.Offers//query the Offers Database
                .Include(offer => offer.Game)//load the game associated with the offer
                .FirstOrDefaultAsync(offer => offer.Id == offerId);//execute the query returning the first offer whos id matches offerId

            if (offer == null)//if not found in the database
            {
                //throw error
                TempData["ErrorMessage"] = "Offer not found.";
                //redirect to the index page
                return RedirectToAction("Index", "Games");
            }

            if(offer.Game == null)//for saftey for whatever reason the connection between the game and offer does not exist
            {
                //throw error
                TempData["ErrorMessage"] = "Game for this offer could not be found";
                //redirect to the browse page
                return RedirectToAction("Index", "Games");
            }

            //ownership check only the seller of the game can accept offers on it
            if(offer.Game.SellerId != currentUserId.Value)
            {
                //throw error
                TempData["ErrorMessage"] = "You are not allowed to accept this offer.";
                //redirect to the profile page
                return RedirectToAction("Profile", "Users");
            }

            //Only allow accept pending offers
            if(offer.Status != OfferStatus.Pending)
            {
                //throw error
                TempData["ErrorMessage"] = "Only pending offers can be accepted.";
                //redirect to the offers index page with the game id (oofers for that game)
                return RedirectToAction("Index", new { gameId = offer.GameId });
            }

            //Load all offers for this game
            //store in a new variable offersForGame
            var offersForGame = await context.Offers//query the Offers Database
                .Where(o => o.GameId == offer.GameId)//Where o.GameId(foreign key) is equal to the game id of the current offer
                .ToListAsync();//query database asynchronously for all offers attached to the game and store them in a list

            foreach( var o in offersForGame)//search through the list of offers for the game
            {
                if(o.Id == offer.Id)//if the current offer o equals the current offers id
                {
                    o.Status = OfferStatus.Accepted;//change offerstatus to accepted
                }
                else if(o.Status == OfferStatus.Pending)//after the first check if it is still pending
                {
                    o.Status = OfferStatus.Rejected;//change offerstatus to rejected
                }
            }

            //since we accepted change the active field to false (no longer available)
            offer.Game.Active = false;

            //update the games totaloffers to reflect what we just changed using offersForGame
            offer.Game.TotalOffers = offersForGame.Count;

            //update the games maxoffers to reflect what we just changed using offersForGame
            offer.Game.MaxOffer = offersForGame.Max(o => o.Amount);

            await context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Offer accepted.";

            return RedirectToAction("Index", new { gameId = offer.GameId });
        }

        private bool IsAjaxRequest()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";//if the request that was sent has the header XMLHttpRequest then it means that it is an AJAX request
        }
    }
}
