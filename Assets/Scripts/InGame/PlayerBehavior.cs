﻿namespace Love.Core
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Networking;

    public class PlayerBehavior : NetworkBehaviour
    {
        static PlayerBehavior s_localPlayer;
        public static PlayerBehavior LocalPlayer
        {
            get { return s_localPlayer; }
        }

        NetworkIdentity identity { get; set; }
        int _playerIndex = -1;
        public int playerIndex
        {
            get { return _playerIndex; }
        }

        public enum PlayerStates
        {
            idle, hover, grab, release
        }

        PlayerStates playerState = PlayerStates.idle;
        GameObject currentBlock;

        [SerializeField] float hoverDistance = 0.5f;

        #region Unity methods

        void Start()
        {
            identity = GetComponent<NetworkIdentity>();
            if (!isLocalPlayer) return;

            if (s_localPlayer != null)
            {
                Debug.LogError("local player already exists");
            }
            s_localPlayer = this;

            _playerIndex = FindObjectsOfType<PlayerBehavior>().Length - 1;
            Debug.Log("player index: " + playerIndex);

            if (playerIndex != 0)
            {
                GetColorBlocks();
                UIController.Instance.GetClientColor();
                Debug.Log("client/ getcolorblocks");
            }
            else
            {
                UIController.Instance.GetHostColor();
            }
        }

        void Update()
        {
            if (GameManager.Instance.isPlaying)
            {
                // now able to play
                Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, hoverDistance))
                {
                    // if holding something else, return
                    if (playerState == PlayerStates.grab) return;

                    // check if the block is interactable
                    GameObject temp = hit.collider.gameObject;

                    if ((currentBlock == null || currentBlock != temp) && temp.GetComponent<NetworkIdentity>().hasAuthority)
                    {
                        currentBlock = temp;
                        playerState = PlayerStates.hover;
                        // Debug.Log("hovering! " + currentBlock.name);
                        UIController.Instance.SetSnackbarText("hovering! " + currentBlock.name);
                    }
                }
                else if (playerState != PlayerStates.idle)   // not hitting anything
                {
                    currentBlock = null;
                    playerState = PlayerStates.idle;
                    // Debug.Log("idling! not hovering anything");
                    UIController.Instance.SetSnackbarText("idling! not hovering anything or non-interactable block!");
                }

                Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward);
            }
        }

        void OnDestroy()
        {
            if (this == s_localPlayer)
            {
                s_localPlayer = null;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log("client start");
        }

        #endregion

        public void GetColorBlocks()
        {
            BlockBehavior[] blocks = FindObjectsOfType<BlockBehavior>();

            for (int i = 0; i < blocks.Length; i++)
            {
                if (playerIndex == 0)
                {
                    if (blocks[i].blockColor == BlockColors.purple || blocks[i].blockColor == BlockColors.white)
                    {
                        blocks[i].GetComponent<NetworkIdentity>().AssignClientAuthority(this.connectionToClient);
                    }
                }
                else
                {
                    if (blocks[i].blockColor == BlockColors.pink || blocks[i].blockColor == BlockColors.yellow)
                    {
                        CmdSetAuthority(blocks[i].GetComponent<NetworkIdentity>(), identity);

                        // blocks[i].GetComponent<NetworkIdentity>().AssignClientAuthority(this.connectionToClient);
                    }
                    Debug.Log("client assign authority!!");
                }
            }
        }

        [Command]
        void CmdSetAuthority(NetworkIdentity grabID, NetworkIdentity playerID)
        {
            grabID.AssignClientAuthority(playerID.connectionToClient);
        }

        [Command]
        void CmdRemoveAuthority(NetworkIdentity grabID, NetworkIdentity playerID)
        {
            grabID.RemoveClientAuthority(playerID.connectionToClient);
        }
    }
}