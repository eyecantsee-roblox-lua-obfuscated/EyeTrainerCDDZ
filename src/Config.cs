using UnityEngine;

namespace CDDZTrainer
{
    /// <summary>Edit these values and rebuild to change keybinds / gun behaviour.</summary>
    internal static class Config
    {
        public static KeyCode MenuKey = KeyCode.Delete;   // open / close trainer menu
        public static KeyCode ImmunityKey = KeyCode.F2;   // toggle immunity (cannot die)
        public static KeyCode RapidFireKey = KeyCode.F3;  // toggle rapid fire
        public static KeyCode FreezeAllKey = KeyCode.F4;  // toggle freezing / unfreezing all other robots
        public static KeyCode KillAllKey = KeyCode.F5;    // kill every other robot
        public static KeyCode DumpKey = KeyCode.F9;       // write API diagnostics to a file
        public static KeyCode GunKey = KeyCode.G;         // spawn/enable / put away/disable the custom gun
        public static KeyCode FireKey = KeyCode.F;        // fire the gun (hold when rapid fire is on)

        // Deprecated original values before an update improved them:
        /*
        public const float NormalFireInterval = 0.35f;    // seconds between shots
        public const float RapidFireInterval = 0.04f;     // seconds between shots with rapid fire
        public const float Range = 400f;                  // hitscan range
        */
        

        // New assigned values (kept old one so if YOU fork this then you can understand it more or whatever, also so people don't commit updates to this edited numerical value, original has no lag btw and this doesn't either. :P
        public const float NormalFireInterval = 0.40f;    // seconds between shots
        public const float RapidFireInterval = 0.01f;     // seconds between shots with rapid fire
        public const float Range = 1000f;                 // hitscan range
    }
}
