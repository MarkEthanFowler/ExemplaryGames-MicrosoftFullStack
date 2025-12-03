using System.Collections.Concurrent;//contains thread safe collection types, since multiple users might be logged in at once (Regular dictionary is not thread safe)

namespace ExemplaryGames.Services
{
    //Interface with the following contracted must be implemented classes
    public interface ILoginRateLimiter
    {
        /*
         * key: identifier we use to track login attempts; follows the IP:email naming convension (127.0.0.1:test@example.com)
         * out TimeSpan? retryAfter: 
         * * out: means the parameter is is an output from the method
         * * TimeSpan?: is a nullable duration until the time block expires
         * * retryAfter: variable name
         */
        bool IsBlocked(string key, out TimeSpan? retryAfter);

        /*
         * key: identifier we use to track login attempts; follows the IP:email naming convension (127.0.0.1:test@example.com)
         */
        void RegisterFailure(string key);

        /*
         * key: identifier we use to track login attempts; follows the IP:email naming convension (127.0.0.1:test@example.com)
         */
        void RegisterSuccess(string key);
    }


    /*
     * _ as a discard, “I don’t care about this value”, out _
     * _ as an unused parameter name, “I must accept this parameter but I’m not using it”, _ => something
     * _ in pattern matching, “match anything”, case _:
     * _ used as a variable name, Just a normal variable (rare & discouraged for clarity), int _ = 5;
     */
    public class LoginRateLimiter : ILoginRateLimiter
    {
        //Key: something like IP:email
        //Value: info about the attempts in the current window
        //Auxilliary Data Structure
        private class AttemptInfo
        {
            /*
             * Count the failed attempts in the current window
             * { get; set; } set up getter and setters automatically
             */
            public int Count { get; set; }

            /*
             * Start time of the current rate limit window
             */
            public DateTime WindowStart { get; set; }
        }

        //ConcurrentDictionary<Key: string, Value: ip:email> for thread safe reading and writing
        private readonly ConcurrentDictionary<string, AttemptInfo> attempts = new();// new(): short hand for new ConcurrentDictionary<string, AttemptInfo>()

        //Max Attempts
        private const int MaxAttempts = 5;

        //Time window before we can try again
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        public bool IsBlocked(string key, out TimeSpan? retryAfter)
        {
            retryAfter = null;// initialize the out parameter

            /*
             * trys to find the entry for the given variable key
             * if found: returns true, and sets info to the AttemptInfo instance
             * if not found: returns false and info remains null
             * * if (! ... ) return false no records for this key so it is not blocked
             */
            if (!attempts.TryGetValue(key, out var info))
            {
                return false;
            }

            var now = DateTime.UtcNow;// the current time it UTC

            //if the window has expired, it's not blocked anymore
            /*
             * now - info.WindowStart: is a timespan representing how much time has passed since the window started
             * > Window: if more time has passed that the configured window of 15 min then the current window is over
             */
            if (now - info.WindowStart > Window)
            {
                ///We’re not blocking them anymore because the time window has expired.
                return false;
            }

            //if the recorded failed attempts (info.Count) is greater than our maximum allowed failed attempts (MaxAttempts) we block them
            if (info.Count >= MaxAttempts)
            {
                /*
                 * (info.WindowStart + Window): is the timestamp at which this window expires
                 * - now: how much time is left until the block expires
                 */
                var remaining = (info.WindowStart + Window) - now;

                //Edge case guard, if the time should go negative clamp it to zero
                if (remaining < TimeSpan.Zero)
                {
                    //clamp to zero
                    remaining = TimeSpan.Zero;
                }

                //set the out parameter to the new timestamp, Try again in X minutes
                retryAfter = remaining;

                //yes the key is blocked
                return true;
            }

            //default not blocked
            return false;
        }

        public void RegisterFailure(string key)
        {
            var now = DateTime.UtcNow;//Get the current time in UTC

            //This is a dictionary
            //This code adds or updates the dictionary
            //AddOrUpdate(Key, addValueFactory: create new value if it does not exist, updateValueFactory: update an existing value if key does not exist)
            attempts.AddOrUpdate(
                key,//the key we are tracking ip:email
                /*
                 * _: key parameter we do not care about since we already have key
                 * Create a new AttemptInfo Object with Count at 1 and WindowStart at now
                 */
                _ => new AttemptInfo { Count = 1, WindowStart = now },
                /*
                 * _: key parameter we do not care about since we already have key
                 * existing: is the current AttemptInfo for that key
                 */
                (_, existing) =>
                {
                    //if the window expired
                    if (now - existing.WindowStart > Window)
                    {
                        //Reset the AttemptInfo
                        existing.Count = 1;
                        existing.WindowStart = now;
                    }
                    //if not expired
                    else
                    {
                        //increase count
                        existing.Count++;
                    }
                    //return the existing variable for the already exists block of code
                    return existing;
                });
        }

        public void RegisterSuccess(string key)
        {
            //On success, we can clear attempts so they can start fresh
            //or you could choose to keep them; clearing is more common
            attempts.TryRemove(key, out _);//out _: discard the removed value since we do not need it
        }
    }
}
