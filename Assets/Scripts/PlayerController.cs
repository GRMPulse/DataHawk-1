using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;
using Valve.VR.InteractionSystem;

public class PlayerController : MonoBehaviour {

    // Action to spawn hawk
    public SteamVR_Action_Boolean spawnHawk;
    public SteamVR_Action_Boolean getHawk;
    public SteamVR_Action_Boolean interactUI;
    // Sound when hawk is spawned
    public GameObject SpawnObjectSound;
    public GameObject dataHawk;
    public GameObject GameController;
    public GameObject menuSound;

    public bool hawkFind;
    bool gettingHawk;

    float time = 0.0f;
    float timeToMove = 3.0f;
    // Use this for initialization
    void Start () {
        GameController = GameObject.FindGameObjectWithTag("GameController");
    }

    private void FixedUpdate () {
        if (SceneManager.GetActiveScene().name == "MainMenu") {
            var leftTriggerDown = interactUI.GetStateDown(SteamVR_Input_Sources.LeftHand);
            if (leftTriggerDown) {
                menuSound.GetComponent<AudioSource>().Play();
                GameController.GetComponent<GameController>().mainMenu = true; 
            }
        } else {
            var leftGripDown = spawnHawk.GetStateDown (SteamVR_Input_Sources.LeftHand);
            var leftTriggerDown = getHawk.GetStateDown (SteamVR_Input_Sources.LeftHand);
            var leftTriggerUp = getHawk.GetStateUp (SteamVR_Input_Sources.LeftHand);

            var rightGripDown = spawnHawk.GetStateDown (SteamVR_Input_Sources.RightHand);
            var rightTriggerDown = getHawk.GetStateDown (SteamVR_Input_Sources.RightHand);
            var rightTriggerUp = getHawk.GetStateUp (SteamVR_Input_Sources.RightHand);

            if (leftTriggerDown) {
                dataHawk.GetComponent<Hawk> ().gettingHawk = true;
                dataHawk.GetComponent<Hawk> ().leftTriggerPressed = true;
            }
            if (rightTriggerDown) {
                dataHawk.GetComponent<Hawk> ().gettingHawk = true;
                dataHawk.GetComponent<Hawk> ().rightTriggerPressed = true;
            }

            if (leftGripDown || rightGripDown) {
                dataHawk.GetComponent<Hawk> ().grabbingHawk = true;
            } else {
                dataHawk.GetComponent<Hawk> ().grabbingHawk = false;
            }
        }

    }

    void Update () {
        if (hawkFind) {
            dataHawk = GameObject.FindGameObjectWithTag ("Hawk");
            hawkFind = false;
        }
    }
}