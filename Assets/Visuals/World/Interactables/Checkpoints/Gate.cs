using System;
using System.Collections.Generic;
using UnityEngine;

public class Gate : MonoBehaviour
{

    private static PlayerMove playerMove;
    public event Action ResetCharges;

    private int numChargeCollected = 0;

    float groundCheckInterval = 0.1f;

    private List<GateCharge> gateCharges = new List<GateCharge>();


    private void Start()
    {
        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>();
        }
    }

    void Update()
    {
        if(numChargeCollected == 0 && playerMove != null){
            return;
        }
        
        if (playerMove.GetPlayerState() == PlayerMove.PlayerState.Grounded)
        {
           // print("Player grounded, resetting gate charges." + ResetCharges.GetInvocationList().Length);
            numChargeCollected = 0;
            foreach (GateCharge gateCharge in gateCharges)
            {
                gateCharge.ResetCharge();
            }
        }
    }


    public void Collect(GateCharge gateCharge)
    {
        gateCharges.Add(gateCharge);

        numChargeCollected++;

        if (numChargeCollected >= 3)
        {
            OpenGate();
        }
    }

    private void OpenGate()
    {
        // Logic to open the gate
        Debug.Log("Gate is now open!");
    }
}
