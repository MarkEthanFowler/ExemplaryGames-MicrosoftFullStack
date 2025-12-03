using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using ExemplaryGames.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;


namespace ExemplaryGames.Controllers
{
    public class GamesController : Controller
    {
        private readonly AppDbContext context;
        private readonly IWebHostEnvironment env;

        public GamesController(AppDbContext context, IWebHostEnvironment env)
        {
            this.context = context;
            this.env = env;
        }

        // GET: /Games
        //IActionResult represents any kind of HTTP response and returns that response. Control what response you want to send back to the client.
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? search)
        {
            //No SQL has been executed this is a query builder or a recipe for a SQL query
            var query = context.Games//represents the games table in the database
                .Include(game => game.Seller)//Include the related Seller entity for each game, for later access to game.Seller.FirstName without extra queries
                .AsQueryable();//Mkes the result an IQueryable allowing us to add filters later if we wish

            if(!string.IsNullOrWhiteSpace(search))//if search is not null, "", " "
            {
                var term = search.Trim().ToLower();//remove whitespace and convert to lowercase

                query = query.Where(g =>//add the where clause to filter the games
                    g.Title.ToLower().Contains(term) ||//if in the title
                    g.Details.ToLower().Contains(term));//or in the details
            }

            var games = await query.ToListAsync();//execute the SQL statement and convert it to a list

            return View(games);//render the razor view Views/Games/Index.cshtml passing the list of games as the model
        }

        // GET: /Games/Details/#
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            if(id < 0)//invalid id
            {
                return NotFound();
            }

            var game = await context.Games
                .Include(game => game.Seller)
                .Include(game => game.Offers)
                .ThenInclude(offer => offer.Buyer)
                .FirstOrDefaultAsync(game => game.Id == id);//Find the game whose ID equals the id passed into the controller and await the results

            if (game == null)
            {
                return NotFound();
            }

            //if the game is inactive, only sellers may view it
            if(!game.Active)
            {
                var currentUserId = GetCurrentUserId();//get the current users id

                if(currentUserId == null)//is the current user cannot be found
                {
                    //throw error
                    TempData["ErrorMessage"] = "You are not logged in";
                    //redirect to the login page
                    return RedirectToAction("Login", "Users");
                }

                if(game.SellerId != currentUserId.Value)//if you are not the seller
                {
                    return Forbid();//forbid this action
                }
            }

            return View(game);
        }

        // GET: /Games/Create
        [HttpGet]
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Games/Create
        [HttpPost]//handles form submissions
        [ValidateAntiForgeryToken]//Protects against Cross-Site Request Forgery (CSRF) attacks by ensuring that the form submission includes a valid anti-forgery token.
        [Authorize]
        public async Task<IActionResult> Create(Game game, IFormFile? image)//uses the form fields for the game model, called model binding
        {                                                   //Remember the ? in IFormFile? means nullable or that it can be null
            if (!ModelState.IsValid)//used for validation attributes
            {
                return View(game);
            }

            //Get current user's ID from auth cookie
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if(string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                //something went wrong with auth send them to login
                return RedirectToAction("Login", "Users");
            }

            var currentUserId = GetCurrentUserId(); //read the id from the logged in users claims
            if(currentUserId == null)
            {
                return RedirectToAction("Login", "Users");//if it fails send them to Login page
            }

            game.SellerId = userId;

            if (image != null && image.Length > 0)//if the user selected a file and the file is not empty
            {

                //Get the folder images
                //Path.Combine is used to create safe valid file system path by combining path segements ex: /wwwroot/ + /images/
                var uploadFolder = Path.Combine(env.WebRootPath, "images");//env.WebRootPath is the path to the wwwroot folder, and the folder we want inside it

                //Ensure directory exists
                Directory.CreateDirectory(uploadFolder);

                //Create a unique file name
                var extension = Path.GetExtension(image.FileName); //File type ex: jpeg
                var fileName = Guid.NewGuid().ToString() + extension;

                var filePath = Path.Combine(uploadFolder, fileName); //Build the location where the file will get saved

                // Save to the disk
                /*
                 * using: create a resource in this case a FileStream
                 * new FileStream: create a new file object, lets use write bytes into a real file on the disk
                 * (filePath, FileMode.Create): the file we want to write, and create a new file
                 */
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);//copy the uploaded image into the file stream
                }

                //we use this in <img src="...">
                game.ImagePath = "/images/" + fileName;
            }
            else
            {
                //No file uploaded
                game.ImagePath = "/images/exemplaryGamesLogo.png";
            }

            //Add(game): When you submit the Create form, MVC gives you a Game object filled with user input
            //When you submit the Create form, MVC gives you a Game object filled with user input
            context.Games.Add(game);

            //Look at all tracked objects and sends the appropriate SQL commands to SQL Server
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));//Redirect to the index page
        }

        //GET: /Games/Edit/5
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)//if id is invalid
            {
                return NotFound();//return 404 error
            }

            var game = await context.Games//Query Games table
                .Include(game => game.Seller)//Load related seller, optional
                .FirstOrDefaultAsync(game => game.Id == id.Value);//return the game or null is it does not exist

            if(game == null)//if no game is found
            {
                return NotFound();//return 404 error
            }

            var currentUserId = GetCurrentUserId();//returns null if user is not properly logged in
            if(currentUserId == null || game.SellerId != currentUserId.Value)//if currentUserId is null or does not equal the seller id
            {
                return Forbid();//return 403 error
            }

            /* alternative for above
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Users");
            }

            if (game.SellerId != currentUserId.Value)
            {
                TempData["ErrorMessage"] = "You are not allowed to modify this listing.";
                return RedirectToAction("Profile", "Users");
            }
             */

            return View(game);//show edit form with game passed in
        }

        //POST: /Games/Edit/#
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, Game updatedGame, IFormFile? image)
        {
            if(id != updatedGame.Id)// if id is not equal to the updatedGames id
            {
                return NotFound();//return 404 error
            }

            var game = await context.Games.FindAsync(id);//get the tracked entity from the database
            if(game == null)//if it find nothing
            {
                return NotFound();//return 404 error
            }

            var currentUserId = GetCurrentUserId(); //returns null if user is not properly logged in
            if (currentUserId == null || game.SellerId != currentUserId.Value)//if currentUserId is null or does not equal the seller id
            {
                return Forbid();//return 403 error
            }

            if(!ModelState.IsValid)//if it fails
            {
                return View(updatedGame);//re display the form with validation messages
            }

            //change the data in the variables
            game.Title = updatedGame.Title;
            game.Price = updatedGame.Price;
            game.Details = updatedGame.Details;
            game.Condition = updatedGame.Condition;

            var oldImagePath = game.ImagePath;

            if(image != null && image.Length > 0)//if image is not null and not empty
            {
                var uploadsFolder = Path.Combine(env.WebRootPath, "images");//get the wwwroot path and the images directory
                Directory.CreateDirectory(uploadsFolder);//create the directory if it does not exist

                var fileName = Path.GetRandomFileName() + Path.GetExtension(image.FileName);//create a random file name and the extension ex .jpg
                var filePath = Path.Combine(uploadsFolder, fileName);//contstruct the path where we will save the image

                using (var stream = new FileStream(filePath, FileMode.Create))//create a new FileStream at the given path and store it in var stream
                {
                    await image.CopyToAsync(stream);//take the bytes of the uploaded file and copy them into the file on the disk async
                }

                game.ImagePath = "/images/" + fileName;//save the virtual path rather than the absolute path, /images/abc.jpg


                //delete the old image file if there was one
                if(!string.IsNullOrWhiteSpace(oldImagePath))
                {
                    //oldImagePath is like "/images/foo.jpg convert to physical path
                    //remove the first / : images/foo.jpg
                    //replace the other / with a \ : images\foo.jpg
                    //env.WebRootPath - D:\...\wwwroot
                    //combine for: D:\...\wwwroot\images\foo.jpg which gives us the physical path
                    var oldPhysicalPath = Path.Combine(env.WebRootPath, oldImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                    //if it exists
                    if(System.IO.File.Exists(oldPhysicalPath))
                    {
                        try//so it will not break on the user
                        {
                            //Delete it
                            System.IO.File.Delete(oldPhysicalPath);
                        }
                        catch
                        {

                        }
                    }
                }
            }

            try//check if the data in the database is in the state EF expected
            {
                await context.SaveChangesAsync();//write changes to DB
                TempData["SuccessMessage"] = "Game updated successfully";
            }
            catch(DbUpdateConcurrencyException)//Your call to SaveChangesAsync failed because the data in the database is not in the state EF expected
            {
                if(!context.Games.Any(game => game.Id == id))//Does a game with this ID even exist anymore?
                {
                    return Forbid(); //If the game was deleted, you should not be allowed to update it so permission denial
                }
                else
                {
                    throw;//rethrow the exception
                }
            }
            return RedirectToAction(nameof(Details), new { id = game.Id });//redirect to details with the updated game
        }

        //GET: /Games/Delete/#
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if(id == null)//if the passed id is null
            {
                return NotFound();//return 404 error
            }

            var game = await context.Games//Get the Games database
                .Include(game => game.Seller)//get the seller assoicate with the game
                .FirstOrDefaultAsync(game => game.Id == id.Value);//check if the game matches the id passed in

            if(game == null)//if the game is not found 
            {
                return NotFound();//return 404 error
            }

            var currentUserId = GetCurrentUserId(); //returns null if user is not properly logged in
            if (currentUserId == null || game.SellerId != currentUserId.Value) //if currentUserId is null or does not equal the seller id
            {
                return Forbid();//return 403 error
            }

            return View(game);
        }

        //POST: /Games/Delete/#
        [HttpPost, ActionName("Delete")]//ActionName("Delete"): allows us to use asp-action="Delete" for both GET and POST
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var game = await context.Games.FindAsync(id);//find the game
            if(game == null)//if not found
            {
                return NotFound();//return 404 error
            }

            var currentUserId = GetCurrentUserId(); //returns null if user is not properly logged in
            if (currentUserId == null || game.SellerId != currentUserId.Value)//if currentUserId is null or does not equal the seller id
            {
                return Forbid();//return 403 error
            }

            context.Games.Remove(game);//delete the game
            await context.SaveChangesAsync();//communicate it to the database

            TempData["SuccessMessage"] = "Your listing has been deleted";//show message

            return RedirectToAction(nameof(Index));//redirect to the index page
        }

        private int? GetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); // User is the ClaimsPrincipal, NameIdentifier is what we search for out of the user info
            if(int.TryParse(userIdString, out int userId))//if we can convert userIdString to an int store the value in userId
            {
                return userId;
            }
            return null;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> XmlFeed()
        {
            var games = await context.Games//Games database
                .Where(game => game.Active)//only include active games
                .OrderBy(game => game.Title)//in alphabetical order
                .ToListAsync();//query database and put results in a list

            // Build XML document:
            // <?xml version="1.0" encoding="utf-8"?>
            // <?xml-stylesheet type="text/xsl" href="/xslt/games.xsl"?>
            // <games> ... </games>
            var doc = new XDocument(//XDocument represents the entire xml document
                new XDeclaration("1.0", "utf-8", "yes"),//standard html stuff
                new XProcessingInstruction(//special instruction indicator
                    "xml-stylesheet",//tells it to use a style sheet
                    "type=\"text/xsl\" href=\"/xslt/games.xsl\""//points to the games.xsl file
                    ),
                //apply xsl and render html below
                new XElement("games",//create a game root element
                games.Select(g =>//foreach game
                    new XElement("game",//define a game element
                        new XElement("id", g.Id),//the game id element
                        new XElement("title", g.Title),//the game title element
                        new XElement("price", g.Price),//the game price element
                        new XElement("condition", g.Condition.ToString()),//the game condition element
                        new XElement("totalOffers", g.TotalOffers),//the game total offers element
                        new XElement("maxOffer", g.MaxOffer)//the game max offer element
                ))));

            return Content(doc.ToString(), "application/xml");//convert the doc to a string and sets a http response content type(this is xml and I have a stylesheet to apply)
        }
    }

}
