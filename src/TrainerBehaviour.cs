using System;
using System.Reflection;
using UnityEngine;

namespace CDDZTrainer
{
    /// <summary>
    /// too lazy to explain but most things run every frame
    /// </summary>
    public class TrainerBehaviour : MonoBehaviour
    {
        bool menuOpen;
        bool gunEquipped;
        bool rapidFire;
        bool errorLogged;

        GameObject gun;
        float nextFire;

        string status = "";
        float statusUntil;

        int bullshit = -0; // bullshit variable, does nothing, here to make sure mod works when your testing it

        Rect window = new Rect(30f, 30f, 380f, 10f);

        void Start()
        {
            Say("EyeCantTrainer Loaded | " + Config.MenuKey + " = Menu Keybind | " + Config.GunKey + " = Gun Keybind");
        }

        void Update()
        {
            try
            {
                if (Input.GetKeyDown(Config.MenuKey)) SetMenu(!menuOpen);
                if (Input.GetKeyDown(Config.ImmunityKey)) SetImmunity(!ImmunityPatch.Enabled);
                if (Input.GetKeyDown(Config.RapidFireKey)) SetRapid(!rapidFire);
                if (Input.GetKeyDown(Config.GunKey)) SetGun(!gunEquipped);
                if (Input.GetKeyDown(Config.KillAllKey)) KillAll();
                if (Input.GetKeyDown(Config.DumpKey)) Dump();

                if (menuOpen)
                {
                    // for devs, don't change this, it unlocks then re-locks the cursor
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                if (ImmunityPatch.Enabled)
                {
                    // this is so scene change or the like doesn't change anything by adding a flag
                    Component p = GameApi.GetPlayer();
                    if (p != null && !GameApi.IsImmune(p))
                        GameApi.SetImmune(p, true);
                }

                if (gunEquipped)
                {
                    // self-explanatory, if you don't know then your a chud skidder
                    EnsureGun();
                    HandleFiring();
                }
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Debug.LogError("[CDDZTrainer] " + e);
                    Say("Error: " + e.GetBaseException().Message);
                }
            }
        }

        void OnDestroy()
        {
            DestroyGun();
            if (menuOpen)
            {
                // for devs don't touch, ensures mouse unlocks, above ensures gun cleanly deletes it's self, also reduces lag
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // ------------------------------------------------------------ toggles

        void Say(string message)
        {
            status = message;
            statusUntil = Time.unscaledTime + 3.5f;
            Debug.Log("[CDDZTrainer] " + message);
        }

        void SetMenu(bool open)
        {
            menuOpen = open;
            if (!open)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void SetImmunity(bool on)
        {
            // essentially uses in game immune flag then fallbacks to a harmony patch that blocks Character.Kill on the player
            bool patched = !on || ImmunityPatch.Install();
            ImmunityPatch.Enabled = on;

            Component player = GameApi.GetPlayer();
            bool flagged = (player != null) && GameApi.SetImmune(player, on);

            if (on && !flagged && !patched)
            {
                ImmunityPatch.Enabled = false;
                Say("Immunity unavailable: " + ImmunityPatch.Status);
                return;
            }
            Say("Immunity " + (on ? "ON" : "OFF") + ((on && !patched) ? " (basic mode)" : ""));
        }

        void SetRapid(bool on)
        {
            rapidFire = on;
            Say("Rapid fire " + (on ? "ON (hold " + Config.FireKey + ")" : "OFF"));
        }

        void SetGun(bool on)
        {
            gunEquipped = on;
            if (on)
            {
                EnsureGun();
                Say("Gun spawned/enabled. Press " + Config.FireKey + " to fire");
            }
            else
            {
                DestroyGun();
                Say("Gun put away/disabled");
            }
        }

        // un-needed/no longer needed, but it dumps api info which can be easier, remove this or expand it, idc
        void Dump()
        {
            string path = GameApi.DumpApiInfo();
            Say("API info written to " + path);
        }

        void KillAll()
        {
            Component player = GameApi.GetPlayer();
            int killed = 0;
            foreach (Component c in GameApi.GetEnemies(player))
            {
                if (c == null || !GameApi.IsAlive(c)) continue;
                if (GameApi.KillCharacter(c, player)) killed++;
            }
            Say("Killed " + killed + " enemy robot(s)");
        }

        // ---------------------------------------------------------------- gun

        void EnsureGun()
        {
            if (gun != null) return;

            Camera cam = GameApi.GetCamera();
            if (cam == null) return;

            gun = new GameObject("CDDZTrainer_Gun");
            gun.transform.SetParent(cam.transform, false);
            gun.transform.localPosition = new Vector3(0.32f, -0.28f, 0.7f);
            gun.transform.localRotation = Quaternion.identity;

            Color metal = new Color(0.25f, 0.27f, 0.3f);
            Color glow = new Color(0.1f, 0.9f, 1f);
            AddPart(gun.transform, new Vector3(0f, 0f, 0.15f), new Vector3(0.12f, 0.14f, 0.45f), metal);   // body
            AddPart(gun.transform, new Vector3(0f, 0.02f, 0.5f), new Vector3(0.06f, 0.06f, 0.4f), metal);  // barrel
            AddPart(gun.transform, new Vector3(0f, -0.14f, 0.05f), new Vector3(0.08f, 0.2f, 0.1f), metal); // grip
            AddPart(gun.transform, new Vector3(0f, 0.08f, 0.2f), new Vector3(0.03f, 0.03f, 0.3f), glow);   // energy strip
        }

        void DestroyGun()
        {
            if (gun != null)
            {
                UnityEngine.Object.Destroy(gun);
                gun = null;
            }
        }

        void HandleFiring()
        {
            if (menuOpen) return;

            bool wantFire = rapidFire ? Input.GetKey(Config.FireKey) : Input.GetKeyDown(Config.FireKey);
            if (!wantFire || Time.time < nextFire) return;

            nextFire = Time.time + (rapidFire ? Config.RapidFireInterval : Config.NormalFireInterval);
            Fire();
        }

        void Fire()
        {
            Camera cam = GameApi.GetCamera();
            if (cam == null) return;

            Component player = GameApi.GetPlayer();
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            Vector3 muzzle = (gun != null)
                ? gun.transform.TransformPoint(new Vector3(0f, 0.03f, 0.72f))
                : cam.transform.position + cam.transform.forward;

            Vector3 end = ray.origin + ray.direction * 150f;

            RaycastHit[] hits = Physics.RaycastAll(ray, Config.Range);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                Component character = GameApi.GetCharacterFrom(hit.collider);

                // Never shoot ourselves; ignore trigger volumes that aren't robots.
                if (character != null && player != null && character == player) continue;
                if (character == null && hit.collider.isTrigger) continue;

                end = hit.point;
                if (character != null && GameApi.IsAlive(character))
                {
                    GameApi.KillCharacter(character, player);
                    SpawnFlash(hit.point);
                }
                break;
            }

            SpawnTracer(muzzle, end);
        }

        // ------------------------------------------------------------- visuals

        static void AddPart(Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider col = part.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col); // visual only, must not block shots

            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            Paint(part, color);
        }

        static void Paint(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;

            // Prefer an unlit shader so the colour shows regardless of scene lighting.
            string[] shaderNames = { "Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default", "Standard" };
            foreach (string name in shaderNames)
            {
                Shader s = Shader.Find(name);
                if (s != null)
                {
                    r.material = new Material(s);
                    break;
                }
            }
            r.material.color = color;
        }

        static void SpawnTracer(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.05f) return;

            GameObject tracer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider col = tracer.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            tracer.transform.position = from + delta * 0.5f;
            tracer.transform.rotation = Quaternion.LookRotation(delta);
            tracer.transform.localScale = new Vector3(0.04f, 0.04f, length);
            Paint(tracer, new Color(0.2f, 0.95f, 1f));
            UnityEngine.Object.Destroy(tracer, 0.06f);
        }

        static void SpawnFlash(Vector3 point)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider col = flash.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            flash.transform.position = point;
            flash.transform.localScale = Vector3.one * 0.8f;
            Paint(flash, new Color(1f, 0.75f, 0.2f));
            UnityEngine.Object.Destroy(flash, 0.12f);
        }

        // ----------------------------------------------------------------- GUI

        // New additions to add a image to the menu gui as it's cooler than just a transparent black cube essentially
        private GUIStyle windowStyle;
        private GUIStyle toggleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private Texture2D bgTexture;
        private bool styleInitialized = false;

        private Color32[] originalPixels;

        // newer variables for settings
        private bool transparentImage = true;
        private bool imageBg = true;

        private int currentImageIndex = 0; // 0 = icon.png, 1 = icon2.png, 2 = icon3.png, 3 = icon4.png | suggested by EyeCantSee, made By EyeCantSee (im crazy </3)
        private int lastLoadedIndex = -1; // security blanket

        private string currImgName;
        private string nextCurrImgName;

        // Window/Category Variables
        private int currentTab = 0; // 0 = Settings, 1 = Mods 1, 2 = Mods 2, 3 = Mods 3, 4 = Credits
        // ^^^^^^^^^^^^^^^^^ --- New for tabs/categories

        // New additions to add a image to the menu gui as it's cooler than just a transparent black cube essentially
        void InitWindowStyle() // kinda hard-coded?
        {
            // if (styleInitialized) return; // no longer needed as settings have been introduced, moved lower in the code

            string modDirectory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string fileName = "icon.png"; // default fallback

            // Switch statement to pick the correct file based on the index rather than using the bloated shit code that didnt work
            switch (currentImageIndex)
            {
                // nextCurrImgName does next thing as say processes afterwards, so this processes it after and currImgName is for a tick updated visual
                case 0:
                    fileName = "icon.png";
                    currImgName = "Miku Uno";
                    nextCurrImgName = "Wifiskeleton Cat";
                    break;
                case 1:
                    fileName = "icon2.png";
                    currImgName = "Wifiskeleton Cat";
                    nextCurrImgName = "Hot Miku";
                    break;
                case 2:
                    fileName = "icon3.png";
                    currImgName = "Hot Miku";
                    nextCurrImgName = "Kawaii Depreso Miku";
                    break;
                case 3:
                    fileName = "icon4.png";
                    currImgName = "Kawaii Depreso Miku";
                    nextCurrImgName = "Miku Uno";
                    break;
            }

            // orig: string path = System.IO.Path.Combine(modDirectory, fileName);
            // new:
            string path = System.IO.Path.Combine(modDirectory, "Images", fileName); // add Images\ as we are moving towards putting images in a folder to keep them sorted better than previously ever since we added more than 1 icon file (in png format)

            // Restore original state
            if (!System.IO.File.Exists(path))
            {
                path = System.IO.Path.Combine(modDirectory, "Images", "icon.png");
            } // add image folder path

            // new non-hardcoded shit one that works:
            ///*
            if (System.IO.File.Exists(path))
            {
                //if (bgTexture == null)
                if (bgTexture == null || lastLoadedIndex != currentImageIndex) // update to allow extra images
                {
                    byte[] fileData = System.IO.File.ReadAllBytes(path);

                    // Older stuff for one image
                    /*
                    bgTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    bgTexture.LoadImage(fileData);

                    // Cache the original opaque pixels permanently on first load
                    originalPixels = bgTexture.GetPixels32();
                    */

                    // Newer stuff for extra images
                    ///*
                    if (bgTexture == null) bgTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                    bgTexture.LoadImage(fileData);
                    originalPixels = bgTexture.GetPixels32();
                    lastLoadedIndex = currentImageIndex;
                    //*/
                }

                // Always start from a clean copy of the original pixels
                Color32[] pixels = (Color32[])originalPixels.Clone();

                if (transparentImage)
                {
                    // Add slight transparency by lowering each pixel's alpha channel
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        // Adjust 0.85f lower for more transparency, higher for less
                        pixels[i].a = (byte)(pixels[i].a * 0.85f);
                    }
                }

                bgTexture.SetPixels32(pixels);
                bgTexture.Apply();
            }
            //*/

            // old shit hardcoded didnt work:
            /*
            if (System.IO.File.Exists(path))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(path);
                bgTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                bgTexture.LoadImage(fileData);

                if (transparentImage)
                {
                    // Add slight transparency by lowering each pixel's alpha channel
                    Color32[] pixels = bgTexture.GetPixels32();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        // Adjust 0.85f lower for more transparency, higher for less
                        pixels[i].a = (byte)(pixels[i].a * 0.85f);
                    }
                    bgTexture.SetPixels32(pixels);
                    bgTexture.Apply();
                } 
                
                // i know the following is bloated/hardcoded and could be better but i neeeded a quick fix and i felt lazy at the time, plus it still works either way.
                else
                {
                    // Reload original pixels or reset alpha back to full (assuming originalPixels is cached)
                    Color32[] pixels = bgTexture.GetPixels32();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i].a = (byte)(pixels[i].a * 1.0f); // Reset alpha to fully opaque
                    }
                    bgTexture.SetPixels32(pixels);
                    bgTexture.Apply();
                }
            }
            */

            // fix initialisation for text coloration depending on image type by number
            if (!styleInitialized)
            {
                windowStyle = new GUIStyle(GUI.skin.window);
                toggleStyle = new GUIStyle(GUI.skin.toggle);
                buttonStyle = new GUIStyle(GUI.skin.button);
                labelStyle = new GUIStyle(GUI.skin.label);
                styleInitialized = true;
            }

            if (bgTexture != null)
            {
                windowStyle.normal.background = bgTexture;
                windowStyle.active.background = bgTexture;
                windowStyle.hover.background = bgTexture;
                windowStyle.focused.background = bgTexture;
                windowStyle.onNormal.background = bgTexture;
                windowStyle.onActive.background = bgTexture;
                windowStyle.onHover.background = bgTexture;
                windowStyle.onFocused.background = bgTexture;
            }

            // newer color returner for img change & no bloating
            Color dynamicTextColor = (currentImageIndex == 0) ? Color.black : Color.white;

            windowStyle.normal.textColor = dynamicTextColor;
            windowStyle.active.textColor = dynamicTextColor;
            windowStyle.hover.textColor = dynamicTextColor;
            windowStyle.focused.textColor = dynamicTextColor;
            windowStyle.onNormal.textColor = dynamicTextColor;
            windowStyle.onActive.textColor = dynamicTextColor;
            windowStyle.onHover.textColor = dynamicTextColor;
            windowStyle.onFocused.textColor = dynamicTextColor;

            toggleStyle.normal.textColor = dynamicTextColor;
            toggleStyle.active.textColor = dynamicTextColor;
            toggleStyle.hover.textColor = dynamicTextColor;
            toggleStyle.focused.textColor = dynamicTextColor;
            toggleStyle.onNormal.textColor = dynamicTextColor;
            toggleStyle.onActive.textColor = dynamicTextColor;
            toggleStyle.onHover.textColor = dynamicTextColor;
            toggleStyle.onFocused.textColor = dynamicTextColor;

            buttonStyle.normal.textColor = dynamicTextColor;
            buttonStyle.active.textColor = dynamicTextColor;
            buttonStyle.hover.textColor = dynamicTextColor;
            buttonStyle.focused.textColor = dynamicTextColor;

            labelStyle.normal.textColor = dynamicTextColor;
        }

        void OnGUI()
        {
            if (Time.unscaledTime < statusUntil)
            {
                GUI.Label(new Rect(20f, Screen.height - 40f, 900f, 30f), "[EyeTrainer] " + status);
            }

            if (menuOpen)
            {
                // New panel for category/tabs
                // Ensure window has safe initial coordinates if uninitialized
                if (window.width == 0) window = new Rect(100f, 100f, 320f, 300f);

                // --- DOCKED SIDE TAB PANEL (Using safe non-layout GUI.Button to prevent layout crashes) --- (idc if you think its shit AJ)
                Rect sideTabRect = new Rect(window.x - 90f, window.y, 85f, 175f);

                GUIStyle panelStyle = imageBg ? windowStyle : GUI.skin.box;

                GUI.Box(sideTabRect, "", panelStyle);

                GUIStyle sideBtnStyle = imageBg ? buttonStyle : GUI.skin.button;

                if (GUI.Button(new Rect(sideTabRect.x + 5f, sideTabRect.y + 8f, 75f, 28f), "Settings", sideBtnStyle)) currentTab = 0;
                if (GUI.Button(new Rect(sideTabRect.x + 5f, sideTabRect.y + 40f, 75f, 28f), "Mods 1", sideBtnStyle)) currentTab = 1;
                if (GUI.Button(new Rect(sideTabRect.x + 5f, sideTabRect.y + 72f, 75f, 28f), "Mods 2", sideBtnStyle)) currentTab = 2;
                if (GUI.Button(new Rect(sideTabRect.x + 5f, sideTabRect.y + 104f, 75f, 28f), "Mods 3", sideBtnStyle)) currentTab = 3;
                if (GUI.Button(new Rect(sideTabRect.x + 5f, sideTabRect.y + 136f, 75f, 28f), "Credits", sideBtnStyle)) currentTab = 4;

                // ------------------------------------------------------------------------

                // --- MAIN WINDOW ---

                if (imageBg)
                {
                    InitWindowStyle();

                    window = GUILayout.Window(0x7C0DE, window, DrawWindow, "EyeCantSee\'s Clone Drone Trainer  (" + Config.MenuKey + " to close)", windowStyle);
                }

                else
                {
                    window = GUILayout.Window(0x7C0DE, window, DrawWindow, "EyeCantSee\'s Clone Drone Trainer  (" + Config.MenuKey + " to close)");
                }
            }
        }

        void DrawWindow(int id)
        {
            // Variables
            GUIStyle tStyle = imageBg ? toggleStyle : GUI.skin.toggle;
            GUIStyle bStyle = imageBg ? buttonStyle : GUI.skin.button;
            GUIStyle lStyle = imageBg ? labelStyle : GUI.skin.label;

            // -----------------------------------------------------

            // Settings
            if (currentTab == 0)
            {
                GUILayout.Space(2f);

                if (GUILayout.Button("Change Background Image (Current: " + (currentImageIndex + 1) + " / " + currImgName + ")", bStyle))
                {
                    if (imageBg == false)
                    {
                        Say("Error: No Current Image as Background - Enable The Image as Background Setting");
                    }

                    else if (imageBg == true)
                    {
                        currentImageIndex = (currentImageIndex + 1) % 4;
                        Say("Current Image: " + (currentImageIndex + 1) + " / " + nextCurrImgName);
                    }

                    else
                        return;
                }

                GUILayout.Space(6f);

                imageBg = GUILayout.Toggle(imageBg, " Image as Background ", tStyle);
                transparentImage = GUILayout.Toggle(transparentImage, " Transparent Image ", tStyle);

                if (!imageBg) transparentImage = false; // simple if statement to ensure transparent image is off when image background is as it can't do anything

                GUILayout.Space(6f);

                GUILayout.Space(15f);

                GUILayout.Label("Current Image: " + (currentImageIndex + 1) + " / " + currImgName, lStyle);
            }

            // -----------------------------------------------------

            // Mods 1
            else if (currentTab == 1)
            {
                bool imm = GUILayout.Toggle(ImmunityPatch.Enabled, " Immunity  [" + Config.ImmunityKey + "]", tStyle);
                if (imm != ImmunityPatch.Enabled) SetImmunity(imm);

                bool rf = GUILayout.Toggle(rapidFire, " Rapid fire  [" + Config.RapidFireKey + "]", tStyle);
                if (rf != rapidFire) SetRapid(rf);

                bool g = GUILayout.Toggle(gunEquipped, " Custom Gun  [" + Config.GunKey + "]   Fire: " + Config.FireKey, tStyle);
                if (g != gunEquipped) SetGun(g);

                GUILayout.Space(6f);

                if (GUILayout.Button("Kill all other robots  [" + Config.KillAllKey + "]", bStyle)) KillAll();
                if (GUILayout.Button("Write API info to file  [" + Config.DumpKey + "]", bStyle)) Dump();

                GUILayout.Space(6f);

                GUILayout.Label("Immunity patch: " + ImmunityPatch.Status, lStyle);
            }

            // -----------------------------------------------------

            // Mods 2
            else if (currentTab == 2)
            {
                GUILayout.Label("empty", lStyle);
                GUILayout.Space(28f);
            }

            // -----------------------------------------------------

            // Mods 3
            else if (currentTab == 3)
            {
                GUILayout.Label("empty", lStyle);
                GUILayout.Space(28f);
            }

            // -----------------------------------------------------

            // Credits
            else if (currentTab == 4)
            {
                GUILayout.Label("Welcome to EyeCantSee's Trainer!", lStyle);
                GUILayout.Space(6f);
                GUILayout.Label("Trainer Made By: EyeCantSee", lStyle);
                GUILayout.Space(6f);
                GUILayout.Label("eyecantsee0466 On Discord", lStyle);
                GUILayout.Space(6f);
                if (GUILayout.Button("Click Me For Credits", lStyle))
                {
                    Say(
                        "Menu/Trainer Made By: EyeCantSee (eyecantsee0466 On Discord)" +
                        "\n" +
                        "Background Image Switcher Suggestion By: EyeCantSee"
                    );

                    GUILayout.Space(6f);

                    GUILayout.Label(
                        "Menu/Trainer Made By: EyeCantSee (eyecantsee0466 On Discord)" +
                        "\n" +
                        "Background Image Switcher Suggestion By: EyeCantSee",
                        lStyle
                    );
                }
            }

            // -----------------------------------------------------

            GUI.DragWindow();
        }
    }
}
