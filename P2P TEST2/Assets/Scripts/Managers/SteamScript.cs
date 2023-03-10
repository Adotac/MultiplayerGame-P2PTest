#if !STEAMWORKS_MODULE || !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
#if !DISABLESTEAMWORKS
using System.Collections;
using Steamworks;
#endif

public class SteamScript : MonoBehaviour
{
    [DisallowMultipleComponent]
    public class SteamManager : MonoBehaviour
    {
#if !DISABLESTEAMWORKS
        protected static bool s_EverInitialized = false;

        protected static SteamManager s_instance;
        public static SteamManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    return new GameObject("SteamManager").AddComponent<SteamManager>();
                }
                else
                {
                    return s_instance;
                }
            }
        }

        protected bool m_bInitialized = false;
        public static bool Initialized
        {
            get
            {
                return Instance.m_bInitialized;
            }
        }

        protected SteamAPIWarningMessageHook_t m_SteamAPIWarningMessageHook;

        [AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
        protected static void SteamAPIDebugTextHook(int nSeverity, System.Text.StringBuilder pchDebugText)
        {
            Debug.LogWarning(pchDebugText);
        }

#if UNITY_2019_3_OR_NEWER
        // In case of disabled Domain Reload, reset static members before entering Play Mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitOnPlayMode()
        {
            s_EverInitialized = false;
            s_instance = null;
        }
#endif

        //Enter Steam App ID here when available
        public const uint STEAM_APPID = 0;

        public string GetSteamID()
        {
            return SteamUser.GetSteamID().GetAccountID().ToString();
        }

        HAuthTicket sessionTicketHandle = HAuthTicket.Invalid;
        string sessionTicketString = null;
        public string GetSessionTicket()
        {
            if (sessionTicketHandle != HAuthTicket.Invalid)
            {
                return sessionTicketString;
            }

            int bufferSize = 1024;
            byte[] buffer = new byte[bufferSize];
            uint ticketSize = 0;
            sessionTicketHandle = SteamUser.GetAuthSessionTicket(buffer, bufferSize, out ticketSize);
            if ((int)ticketSize > bufferSize)
            {
                SteamUser.CancelAuthTicket(sessionTicketHandle);
                bufferSize = (int)ticketSize;
                sessionTicketHandle = SteamUser.GetAuthSessionTicket(buffer, bufferSize, out ticketSize);
            }
            Array.Resize(ref buffer, (int)ticketSize);
            //convert to hex string
            sessionTicketString = System.BitConverter.ToString(buffer).Replace("-", "");
            return sessionTicketString;
        }

        CallResult<EncryptedAppTicketResponse_t> appTicketCallResult = new CallResult<EncryptedAppTicketResponse_t>();
        private event Action<string> appTicketEvent;
        private string encryptedAppTicket = null;

        public void RequestAppTicket(Action<string> callback)
        {
            if (encryptedAppTicket != null)
            {
                callback?.Invoke(encryptedAppTicket);
                return;
            }

            if (appTicketEvent != null)
            {
                appTicketEvent += callback;
                return;
            }

            appTicketEvent += callback;
            var call = SteamUser.RequestEncryptedAppTicket(null, 0);
            appTicketCallResult.Set(call, RequestAppTicketResponse);
        }

        private void RequestAppTicketResponse(EncryptedAppTicketResponse_t response, bool ioFailure)
        {
            if (ioFailure || response.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError("Error occured when requesting Encrypted App Ticket");
                appTicketEvent?.Invoke(null);
                appTicketEvent = null;
                return;
            }

            int bufferSize = 1024;
            byte[] buffer = new byte[bufferSize];
            uint ticketSize = 0;
            bool success = SteamUser.GetEncryptedAppTicket(buffer, bufferSize, out ticketSize);
            if (!success && (int)ticketSize > bufferSize)
            {
                SteamUser.CancelAuthTicket(sessionTicketHandle);
                bufferSize = (int)ticketSize;
                success = SteamUser.GetEncryptedAppTicket(buffer, bufferSize, out ticketSize);
            }
            if (!success)
            {
                Debug.LogError("Failed to retrieve Encrypted App Ticket");
                appTicketEvent?.Invoke(null);
                appTicketEvent = null;
                return;
            }

            Array.Resize(ref buffer, (int)ticketSize);
            //convert to hex string
            encryptedAppTicket = System.BitConverter.ToString(buffer).Replace("-", "");

            appTicketEvent?.Invoke(encryptedAppTicket);
            appTicketEvent = null;
        }

        private void OnApplicationQuit()
        {
            if (sessionTicketHandle != HAuthTicket.Invalid)
            {
                SteamUser.CancelAuthTicket(sessionTicketHandle);
                sessionTicketHandle = HAuthTicket.Invalid;
                sessionTicketString = null;
            }
        }

        protected virtual void Awake()
        {
            // Only one instance of SteamManager at a time!
            if (s_instance != null)
            {
                Destroy(gameObject);
                return;
            }
            s_instance = this;

            if (s_EverInitialized)
            {
                // This is almost always an error.
                // The most common case where this happens is when SteamManager gets destroyed because of Application.Quit(),
                // and then some Steamworks code in some other OnDestroy gets called afterwards, creating a new SteamManager.
                // You should never call Steamworks functions in OnDestroy, always prefer OnDisable if possible.
                throw new System.Exception("Tried to Initialize the SteamAPI twice in one session!");
            }

            // We want our SteamManager Instance to persist across scenes.
            DontDestroyOnLoad(gameObject);

            if (!Packsize.Test())
            {
                Debug.LogError("[Steamworks.NET] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.", this);
            }

            if (!DllCheck.Test())
            {
                Debug.LogError("[Steamworks.NET] DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.", this);
            }

            try
            {
                // If Steam is not running or the game wasn't started through Steam, SteamAPI_RestartAppIfNecessary starts the
                // Steam client and also launches this game again if the User owns it. This can act as a rudimentary form of DRM.

                // Once you get a Steam AppID assigned by Valve, you need to replace AppId_t.Invalid with it and
                // remove steam_appid.txt from the game depot. eg: "(AppId_t)480" or "new AppId_t(480)".
                // See the Valve documentation for more information: https://partner.steamgames.com/doc/sdk/api#initialization_and_shutdown
                AppId_t id = STEAM_APPID > 0 ? new AppId_t(STEAM_APPID) : AppId_t.Invalid;
                if (SteamAPI.RestartAppIfNecessary(id))
                {
                    Application.Quit();
                    return;
                }
            }
            catch (System.DllNotFoundException e)
            { // We catch this exception here, as it will be the first occurrence of it.
                Debug.LogError("[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in the correct location. Refer to the README for more details.\n" + e, this);

                Application.Quit();
                return;
            }

            // Initializes the Steamworks API.
            // If this returns false then this indicates one of the following conditions:
            // [*] The Steam client isn't running. A running Steam client is required to provide implementations of the various Steamworks interfaces.
            // [*] The Steam client couldn't determine the App ID of game. If you're running your application from the executable or debugger directly then you must have a [code-inline]steam_appid.txt[/code-inline] in your game directory next to the executable, with your app ID in it and nothing else. Steam will look for this file in the current working directory. If you are running your executable from a different directory you may need to relocate the [code-inline]steam_appid.txt[/code-inline] file.
            // [*] Your application is not running under the same OS user context as the Steam client, such as a different user or administration access level.
            // [*] Ensure that you own a license for the App ID on the currently active Steam account. Your game must show up in your Steam library.
            // [*] Your App ID is not completely set up, i.e. in Release State: Unavailable, or it's missing default packages.
            // Valve's documentation for this is located here:
            // https://partner.steamgames.com/doc/sdk/api#initialization_and_shutdown
            m_bInitialized = SteamAPI.Init();
            if (!m_bInitialized)
            {
                Debug.LogError("[Steamworks.NET] SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.", this);

                return;
            }

            s_EverInitialized = true;
        }

        // This should only ever get called on first load and after an Assembly reload, You should never Disable the Steamworks Manager yourself.
        protected virtual void OnEnable()
        {
            if (s_instance == null)
            {
                s_instance = this;
            }

            if (!m_bInitialized)
            {
                return;
            }

            if (m_SteamAPIWarningMessageHook == null)
            {
                // Set up our callback to receive warning messages from Steam.
                // You must launch with "-debug_steamapi" in the launch args to receive warnings.
                m_SteamAPIWarningMessageHook = new SteamAPIWarningMessageHook_t(SteamAPIDebugTextHook);
                SteamClient.SetWarningMessageHook(m_SteamAPIWarningMessageHook);
            }
        }

        // OnApplicationQuit gets called too early to shutdown the SteamAPI.
        // Because the SteamManager should be persistent and never disabled or destroyed we can shutdown the SteamAPI here.
        // Thus it is not recommended to perform any Steamworks work in other OnDestroy functions as the order of execution can not be garenteed upon Shutdown. Prefer OnDisable().
        protected virtual void OnDestroy()
        {
            if (s_instance != this)
            {
                return;
            }

            s_instance = null;

            if (!m_bInitialized)
            {
                return;
            }

            SteamAPI.Shutdown();
        }

        protected virtual void Update()
        {
            if (!m_bInitialized)
            {
                return;
            }

            // Run Steam client callbacks
            SteamAPI.RunCallbacks();
        }
#else
	public static bool Initialized
    {
		get
        {
			return false;
		}
	}

    protected virtual void Awake()
    {
        Destroy(gameObject);
    }

    public string GetSteamID()
    {
        return null;
    }

    public string GetSessionTicket()
    {
        return null;
    }

    public void RequestAppTicket(Action<string> callback)
    {
        callback?.Invoke(null);
    }

    public static SteamManager Instance
    {
        get
        {
#if !STEAMWORKS_MODULE
            Debug.LogError("Steamworks.NET plugin not installed. Install through the package manager from the git URL https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net");
#endif
            return null;
        }
    }
#endif // !DISABLESTEAMWORKS
    }
}
