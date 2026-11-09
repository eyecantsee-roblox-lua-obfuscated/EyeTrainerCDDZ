using ModLibrary;
using UnityEngine;

namespace CDDZTrainer
{
    /// <summary>
    /// Entry point picked up by Mod-Bot. All it does is (re)create a persistent
    /// controller object; the real work happens in TrainerBehaviour.
    /// </summary>
    [MainModClass]
    public class Main : Mod
    {
        const string ControllerName = "CDDZTrainerMod_Controller";

        protected override void OnModRefreshed()
        {
            // Remove a controller left over from a previous load of this mod.
            GameObject old = GameObject.Find(ControllerName);
            if (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old);
            }

            GameObject go = new GameObject(ControllerName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<TrainerBehaviour>();
        }
    }
}
