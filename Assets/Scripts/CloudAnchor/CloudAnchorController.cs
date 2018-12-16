﻿namespace Love.Core
{
    using System.Collections;
    using System.Collections.Generic;
    using GoogleARCore;
    using GoogleARCore.CrossPlatform;
    using Love.Common;
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.Networking;

#if UNITY_EDITOR
    using Input = GoogleARCore.InstantPreviewInput;
#endif

    public class CloudAnchorController : MonoBehaviour
    {
        /// <summary>
        /// Manages sharing Anchor Ids across the local network to clients using Unity's NetworkServer.  There
        /// are many ways to share this data and this not part of the ARCore Cloud Anchors API surface.
        /// </summary>
        [SerializeField] RoomSharingServer RoomSharingServer;
        [SerializeField] UIController UIController;
        [SerializeField] GameObject lightPrefab;
        [SerializeField] float lightDistance = 1f;

        [Header("ARCore")]

        [SerializeField] GameObject ARCoreRoot;

        // use lighting estimation shaders
        [SerializeField] GameObject WallPrefab;

        [Header("ARKit")]

        [SerializeField] GameObject ARKitRoot;
        [SerializeField] Camera ARKitFirstPersonCamera;

        // diffuse shader --- future implmentation
        [SerializeField] GameObject ARKitWallPrefab;

        const string k_LoopbackIpAddress = "127.0.0.1";

        [SerializeField] float rotation = 180.0f;
        [SerializeField] float height = 1.2f;

        ARKitHelper m_ARKit = new ARKitHelper();

        // True if the app is in the process of quitting due to an ARCore connection error, otherwise false.
        bool m_IsQuitting = false;

        public Component m_LastPlacedAnchor = null;

        public XPAnchor m_LastResolvedAnchor = null;

        ApplicationMode m_CurrentMode = ApplicationMode.Ready;

        GameObject player;

        int m_CurrentRoom;

        public enum ApplicationMode
        {
            Ready,
            Hosting,
            Resolving,
        }

        [SerializeField] float totalTime = 60f;
        string min;
        string sec;

        public delegate void AnchorSavedCallback(Transform anchor);
        public event AnchorSavedCallback OnAnchorSaved;

        public void Start()
        {
            if (Application.platform != RuntimePlatform.IPhonePlayer)
            {
                ARCoreRoot.SetActive(true);
                ARKitRoot.SetActive(false);
                player = ARCoreRoot;
            }
            else
            {
                ARCoreRoot.SetActive(false);
                ARKitRoot.SetActive(true);
                player = ARKitRoot;
            }

            _ResetStatus();
        }

        public void Update()
        {
            _UpdateApplicationLifecycle();

            if (m_CurrentMode != ApplicationMode.Hosting || m_LastPlacedAnchor != null)
            {
                return;
            }

            Touch touch;
            if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
            {
                return;
            }

            // Raycast against the location the player touched to search for planes.
            if (Application.platform != RuntimePlatform.IPhonePlayer)
            {
                TrackableHit hit;
                if (Frame.Raycast(touch.position.x, touch.position.y,
                        TrackableHitFlags.PlaneWithinPolygon, out hit))
                {
                    m_LastPlacedAnchor = hit.Trackable.CreateAnchor(hit.Pose);
                }
            }
            else
            {
                Pose hitPose;
                if (m_ARKit.RaycastPlane(ARKitFirstPersonCamera, touch.position.x, touch.position.y, out hitPose))
                {
                    m_LastPlacedAnchor = m_ARKit.CreateAnchor(hitPose);
                }
            }

            if (m_LastPlacedAnchor != null)
            {
                // // spawn a wall
                // GameObject wall = Instantiate(_GetWallPrefab(), m_LastPlacedAnchor.transform.position + new Vector3(0, height, 0),
                // m_LastPlacedAnchor.transform.rotation);
                // GameObject light1 = Instantiate(lightPrefab, wall.transform.position + new Vector3(0, 0, lightDistance), Quaternion.identity);
                // GameObject light2 = Instantiate(lightPrefab, wall.transform.position + new Vector3(0, 0, -lightDistance), Quaternion.identity);
                // light1.transform.Rotate(0, 180, 0);

                // // Make the wall a child of the anchor.
                // wall.transform.parent = m_LastPlacedAnchor.transform;
                // light1.transform.parent = m_LastPlacedAnchor.transform;
                // light2.transform.parent = m_LastPlacedAnchor.transform;

                // Save cloud anchor.
                _HostLastPlacedAnchor();
            }
        }

        // host
        public void OnEnterHostingModeClick()
        {
            // host the server
            NetworkManager.singleton.StartHost();
            
            if (m_CurrentMode == ApplicationMode.Hosting)
            {
                m_CurrentMode = ApplicationMode.Ready;
                _ResetStatus();
                return;
            }

            m_CurrentMode = ApplicationMode.Hosting;
            m_CurrentRoom = Random.Range(1, 9999);
            UIController.SetRoomTextValue(m_CurrentRoom);
            UIController.ShowHostingModeBegin();
            player.tag = "player1";
        }

        // client
        public void OnEnterResolvingModeClick()
        {
            if (m_CurrentMode == ApplicationMode.Resolving)
            {
                m_CurrentMode = ApplicationMode.Ready;
                _ResetStatus();
                return;
            }

            m_CurrentMode = ApplicationMode.Resolving;
            UIController.ShowResolvingModeBegin();
            player.tag = "player2";
        }

        public void OnResolveRoomClick()
        {
            var roomToResolve = UIController.GetRoomInputValue();
            if (roomToResolve == 0)
            {
                UIController.ShowResolvingModeBegin("Anchor resolve failed due to invalid room code.");
                return;
            }

            UIController.SetRoomTextValue(roomToResolve);
            string ipAddress =
                UIController.GetResolveOnDeviceValue() ? k_LoopbackIpAddress : UIController.GetIpAddressInputValue();

            UIController.ShowResolvingModeAttemptingResolve();

            // join as a client
            NetworkManager.singleton.networkAddress = ipAddress;
            NetworkManager.singleton.networkPort = 8888;
            NetworkManager.singleton.StartClient();
            // NetworkManager.singleton.client.Connect(ipAddress, 8888);

            RoomSharingClient roomSharingClient = new RoomSharingClient();
            roomSharingClient.GetAnchorIdFromRoom(roomToResolve, ipAddress, (bool found, string cloudAnchorId) =>
            {
                Debug.Log("try to get an anchor in the loop");

                if (!found)
                {
                    UIController.ShowResolvingModeBegin("Anchor resolve failed due to invalid room code, " +
                                                        "ip address or network error.");
                }
                else
                {
                    _ResolveAnchorFromId(cloudAnchorId);
                }
            });
        }

        // host -- color and ui
        private void _HostLastPlacedAnchor()
        {
#if !UNITY_IOS || ARCORE_IOS_SUPPORT

#if !UNITY_IOS
            var anchor = (Anchor)m_LastPlacedAnchor;
#else
            var anchor = (UnityEngine.XR.iOS.UnityARUserAnchorComponent)m_LastPlacedAnchor;
#endif
            UIController.ShowHostingModeAttemptingHost();
            XPSession.CreateCloudAnchor(anchor).ThenAction(result =>
            {
                if (result.Response != CloudServiceResponse.Success)
                {
                    UIController.ShowHostingModeBegin(
                        string.Format("Failed to host cloud anchor: {0}", result.Response));
                    return;
                }

                RoomSharingServer.SaveCloudAnchorToRoom(m_CurrentRoom, result.Anchor);
                if(OnAnchorSaved != null)
                {
                    OnAnchorSaved(anchor.transform);
                }
                UIController.ShowHostingModeBegin("Cloud anchor was created and saved.");
                UIController.ShowHostReadyUI(); // for host
                AssignColors();
            });
#endif
        }

        // client -- color and ui
        private void _ResolveAnchorFromId(string cloudAnchorId)
        {
            XPSession.ResolveCloudAnchor(cloudAnchorId).ThenAction((System.Action<CloudAnchorResult>)(result =>
            {
                if (result.Response != CloudServiceResponse.Success)
                {
                    UIController.ShowResolvingModeBegin(string.Format("Resolving Error: {0}.", result.Response));
                    return;
                }

                m_LastResolvedAnchor = result.Anchor;
                GameObject wall = Instantiate(WallPrefab, result.Anchor.transform.position + new Vector3(0, height, 0),
                result.Anchor.transform.rotation);
                GameObject light1 = Instantiate(lightPrefab, wall.transform.position + new Vector3(0, 0, lightDistance), Quaternion.identity);
                GameObject light2 = Instantiate(lightPrefab, wall.transform.position + new Vector3(0, 0, -lightDistance), Quaternion.identity);
                light1.transform.Rotate(0, 180, 0);

                UIController.ShowResolvingModeSuccess();
                AssignColors();
            }));
        }

        /// <summary>
        /// Resets the internal status and UI.
        /// </summary>
        private void _ResetStatus()
        {
            // Reset internal status.
            m_CurrentMode = ApplicationMode.Ready;

            if (m_LastPlacedAnchor != null)
            {
                Destroy(m_LastPlacedAnchor.gameObject);
            }

            m_LastPlacedAnchor = null;
            if (m_LastResolvedAnchor != null)
            {
                Destroy(m_LastResolvedAnchor.gameObject);
            }

            m_LastResolvedAnchor = null;
            player.tag = "Untagged";
            UIController.ShowLobbyUI();
        }

        /// <summary>
        /// Check and update the application lifecycle.
        /// </summary>
        private void _UpdateApplicationLifecycle()
        {
            // Exit the app when the 'back' button is pressed.
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            var sleepTimeout = SleepTimeout.NeverSleep;

#if !UNITY_IOS
            // Only allow the screen to sleep when not tracking.
            if (Session.Status != SessionStatus.Tracking)
            {
                const int lostTrackingSleepTimeout = 15;
                sleepTimeout = lostTrackingSleepTimeout;
            }
#endif

            Screen.sleepTimeout = sleepTimeout;

            if (m_IsQuitting)
            {
                return;
            }

            // Quit if ARCore was unable to connect and give Unity some time for the toast to appear.
            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                _ShowAndroidToastMessage("Camera permission is needed to run this application.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status.IsError())
            {
                _ShowAndroidToastMessage("ARCore encountered a problem connecting.  Please start the app again.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        private void _DoQuit()
        {
            Application.Quit();
        }

        private void _ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                        message, 0);
                    toastObject.Call("show");
                }));
            }
        }

        public void ResetStatus()
        {
            _ResetStatus();
        }

        public void StartGame()
        {
            UIController.StartGameUI();
            StartCoroutine(CountDown());
            Debug.Log("on count down");
        }

        void AssignColors()
        {
            // show color ui
            if (player.tag == "player1")
            {
                UIController.GetHostColor();
            }
            else if (player.tag == "player2")
            {
                UIController.GetClientColor();
            }
        }

        IEnumerator CountDown()
        {
            while (totalTime > 0f)
            {
                totalTime--;
                min = Mathf.FloorToInt(totalTime / 60).ToString("00");
                sec = Mathf.RoundToInt(totalTime % 60).ToString("00");
                UIController.timer.text = (min + ":" + sec);
                yield return new WaitForSeconds(1f);

                if (totalTime <= 0f)
                {
                    // final
                    UIController.timer.text = "00:00";
                    Debug.Log("game end");
                    UIController.ShowEndUI();
                    yield break;
                }
            }
        }
    }
}
