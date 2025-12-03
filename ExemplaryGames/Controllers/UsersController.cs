using Microsoft.AspNetCore.Mvc;
using ExemplaryGames.Models;
using ExemplaryGames.Services;
using Microsoft.AspNetCore.Authentication; // needed to use HttpContext.SignInAsync and SignOutAsync
using Microsoft.AspNetCore.Authentication.Cookies; // CookieAuthenticationDefaults.AuthenticationScheme
using Microsoft.AspNetCore.Authorization; // allows us to use [Authorize]
using Microsoft.AspNetCore.Identity; // allows for the use of PasswordHasher<User> and IPasswordHasher<User>
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; // allows us to build and read claims - pieces of information about the user like their ID, email, name

namespace ExemplaryGames.Controllers
{
    public class UsersController : Controller
    {
        private readonly AppDbContext context;
        private readonly IPasswordHasher<User> passwordHasher; // allows us to hash passwords and verify hashes for user objects

        /*
         * ILoginRateLimiter: what it can do
         * LoginRateLimiter: how it is done
         * Protects the code if I where to change LoginRateLimiter later
         * Also allows us to use the same interface on different Limiter classes ie DatabaseLoginRateLimiter, MemoryCacheLoginRateLimiter for example not that we would make them
         */
        private readonly ILoginRateLimiter loginRateLimiter; // alows us to use the rate limiting logic

        public UsersController(AppDbContext context, IPasswordHasher<User> passwordHasher, ILoginRateLimiter loginRateLimiter)
        {
            this.context = context;
            this.passwordHasher = passwordHasher;
            this.loginRateLimiter = loginRateLimiter;
        }

        //GET: /Users/Register
        [HttpGet]
        public IActionResult Register()
        {
            if(User.Identity?.IsAuthenticated == true) // User.Identity holds info about the users identity which is nullable
            {
                return RedirectToAction("Profile"); // redirects to the profile page
            }

            return View(); //render this page ie Register
        }

        //POST: /Users/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string firstName, string lastName, string Email, string Password)
        {
            if(string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ModelState.AddModelError(string.Empty, "All fields are required.");//add an error saying all fields are required
                return View(); //render this page ie Register
            }

            //check if email already exists in the system
            bool emailExists = await context.Users.AnyAsync(user => user.Email == Email); //AnyAsync returns true if it finds a match, so it is looking for matching emails
            if(emailExists)
            {
                ModelState.AddModelError(string.Empty, "An account with this email already exists."); //add an error the email is taken
                return View(); //render this page ie Register
            }

            //Create a new user Entity/Object
            var user = new User
            {
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Email = Email.Trim()
            };

            user.PasswordHash = passwordHasher.HashPassword(user, Password);//Hash the password before adding it to the database, (Table, Field)

            context.Users.Add(user);
            await context.SaveChangesAsync();

            //This allows for automatically logging in after registration, its optional
            await SignInUserAsync(user); //Makes the claim identity and assigns a cookie

            TempData["SuccessMessage"] = "Account created successfully!";

            return RedirectToAction("Profile"); //redirects to the profile page

        }

        //GET: /Users/Login
        [HttpGet]
        public IActionResult Login()
        {
            if(User.Identity?.IsAuthenticated == true) // User.Identity holds info about the users identity which is nullable
            {
                return RedirectToAction("Profile"); // redirects to the profile page
            }

            return View(); //render this page ie Login
        }

        //POST: /Users/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if(string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Email and password are required."); //add an error saying email and password are required
                return View(); //Renders this page, Login
            }

            //build a key for rate limiting: combine IP + email lowercase
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";//Get the ip address and convert it to a string and if it is null have the default value of unknown-ip
            var key = $"{ip}:{email.Trim().ToLowerInvariant()}";//Create the unique key for the rate limiting process, normalized to lowercase and trimmed

            //check if this key is currently blocked
            //returns true is the key has exceeded the max failures
            //returns false in it has not
            //out var retryAfter: retrieves how long the user must wait until they can retry
            if (loginRateLimiter.IsBlocked(key, out var retryAfter))
            {
                //gets the minutes until they can retry which is nullable with the set default value of 0
                //?? 0: replace null with 0
                var minutes = retryAfter?.TotalMinutes ?? 0;

                //rounds it to the nearest minute
                var rounded = (int)Math.Ceiling(minutes);

                //attach an error to the login page
                ModelState.AddModelError(string.Empty,//not tied to a specific input field, general error that appears on the screen not tied to a specific field on the screen
                    rounded > 0//condition for the messages below
                    ? $"Too many failed login attempts. Please try again in about {rounded} minute(s)."//the default message we expect, rounded > 0 true
                    : "Too many failed login attempts. Please try again later.");//rare edge case when rounded is 0. rounded > 0 false
            }

            var user = await context.Users.SingleOrDefaultAsync(users => users.Email == email); //Lookup the user in the database, returning that user or null
            if(user == null)
            {
                //record a failed attempt for this key
                loginRateLimiter.RegisterFailure(key);

                ModelState.AddModelError(string.Empty, "Invalid email or password"); //add an error saying email and password are invalid
                return View(); //Renders this page, Login
            }

            //the user, the hashed password in the system, plain text password
            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password); // Salt the provided password to check if they are the same password in the database
            if(verification == PasswordVerificationResult.Failed) //PasswordVerificationResult is an enum that return Success, Failed, SuccessRehashNeeded
            {
                //record a failed attempt for this key
                loginRateLimiter.RegisterFailure(key);

                ModelState.AddModelError(string.Empty, "Invalid email or password"); //add an error saying email and password are invalid
                return View(); //Renders this page, Login
            }

            //reset the limiter for this key
            loginRateLimiter.RegisterSuccess(key);

            await SignInUserAsync(user); //Makes the claim identity and assigns a cookie

            TempData["SuccessMessage"] = "You are now logged in.";

            return RedirectToAction("Profile"); //render this page ie Login
        }

        //POST: /Users/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            if(User.Identity?.IsAuthenticated == true) // User.Identity holds info about the users identity which is nullable
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); //Tells ASP.Net cores auth system to delete the auth cookie and clear the users identity from the current context
            }

            TempData["SuccessMessage"] = "You have been logged out.";

            return RedirectToAction("Index", "Home"); //send the user back to the home page after logging out ("Action Name" method inside controller, the controller we want to use)
        }

        //GET: /Users/Profile
        [HttpGet]
        [Authorize] //only allows authenticated users to access /Users/Profile
        public async Task<IActionResult> Profile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); // looks for the first claim with the name identifier which is the id in this case
            if(string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId)) // if the id is not a string
            {
                return RedirectToAction("Login"); //redirect to Login page
            }

            var user = await context.Users
                .Include(user => user.GamesForSale) //load the games associate with this user
                .Include(user => user.Offers) //load the offers associate with this user
                .ThenInclude(offer => offer.Game)//and associate that offer with the correct game
                .FirstOrDefaultAsync(user => user.Id == userId);//find the user with the given user id

            if(user == null)
            {
                return RedirectToAction("Login"); //redirect to the Login page
            }

            return View(user); //Render Views/Users/Profile.cshtml passing the User in with its GamesForSale and Offers collections
        }

        //Create the authenication cookie
        private async Task SignInUserAsync(User user)
        {
            //A claim is a piece of information abou the user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), //assign the id to the claim type name identifier
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"), // assign the first and last name to the claim type name
                new Claim(ClaimTypes.Email, user.Email)// asign the email to the claim type email
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme); //like a wallet of information about the user, this user has these claims

            var principal = new ClaimsPrincipal(identity); //Think of this like cards in the wallet; this is the user, with that identity

            /*
             * where cookie auth is actually issued
             * serialize the principal into an encrypted cookie
             * sends that cookie to the browser
             * when requested the browser sends the cookie back and restores the user object
             */
            await HttpContext.SignInAsync( CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }
    }
}
