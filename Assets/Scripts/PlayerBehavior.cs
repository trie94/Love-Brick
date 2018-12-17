﻿namespace Love.Core
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Networking;

    public class PlayerBehavior : MonoBehaviour
    {
        int playerIndex = -1;

        void Awake()
        {
            // assign player index
            playerIndex = FindObjectsOfType<PlayerBehavior>().Length - 1;
            Debug.Log("player index: " + playerIndex);
            if (playerIndex == 0)
            {
                UIController.Instance.GetHostColor();
            }
            else
            {
                UIController.Instance.GetClientColor();
            }
        }
    }

}