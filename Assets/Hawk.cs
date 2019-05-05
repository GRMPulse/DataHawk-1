using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Valve.VR.InteractionSystem
{
	//-------------------------------------------------------------------------
	[RequireComponent( typeof( Interactable ) )]
	
public class Hawk : MonoBehaviour {
	public GameObject rightHand;
	public GameObject leftHand;
	public GameObject GameController;
	public GameObject Goal;

	private Hand hand;

	public bool gettingHawk;
	public bool grabbingHawk;
	public bool throwingHawk;

	public bool leftTriggerPressed; 
	public bool rightTriggerPressed;

	bool resetSound = false;

	float time = 0.0f;
	float timeToMove = 1.5f; 

	public GameObject ExitPointOne;
    public GameObject ExitPointTwo;


	// bool  
	// Use this for initialization
	void Start () {
		leftHand = GameObject.FindGameObjectWithTag("LeftHand");
		rightHand = GameObject.FindGameObjectWithTag("RightHand");
		ExitPointOne = GameObject.FindGameObjectWithTag("ExitPointOne");
		ExitPointTwo = GameObject.FindGameObjectWithTag("ExitPointTwo");
		GameController  = GameObject.FindGameObjectWithTag("GameController");
		Goal = GameObject.FindGameObjectWithTag("Goal");
	}

	private void OnAttachedToHand(Hand attachedHand) {
		hand = attachedHand;
    }

	// Update is called once per frame
	void Update () {
		if (gettingHawk == true && leftTriggerPressed) {
			GetHawk(leftHand);
		}

		if (gettingHawk == true && rightTriggerPressed) {
			GetHawk(rightHand);
		}
	}

	void GetHawk (GameObject hand) {
        gameObject.GetComponent<Rigidbody>().isKinematic = true;
		Quaternion finalAxeAngle = Quaternion.Euler(0, 0, 90);
		time += Time.deltaTime;
		float lerpTime = time / timeToMove;
		transform.position = Vector3.Lerp(transform.position, hand.transform.position, lerpTime);
		transform.rotation = Quaternion.Slerp(transform.rotation, finalAxeAngle, lerpTime);
		if (!resetSound) {
			hand.GetComponent<AudioSource>().Play();
			resetSound = true;
		}
		if (lerpTime >= 1.0f || grabbingHawk == true) {
			gameObject.GetComponent<Rigidbody>().isKinematic = false;
			gettingHawk = false;
			resetSound = false;
			rightTriggerPressed = false;
			leftTriggerPressed = false;
			time = 0.0f;
		}
	}

	public void Teleport (GameObject ExitPoint)
	{
		ExitPoint.GetComponent<AudioSource>().Play();
		transform.position = ExitPoint.transform.position; 
		float hawkVelocity = gameObject.GetComponent<Rigidbody>().velocity.magnitude;
		float finalVelocity = hawkVelocity * 2.5f;
		gameObject.GetComponent<Rigidbody>().AddForce(ExitPoint.transform.up * finalVelocity, ForceMode.Impulse);
	}

	void OnCollisionEnter (Collision col) {
		if (col.gameObject.tag == "Teleport_Target_One")
        {
            Teleport(ExitPointTwo);
        }

        if (col.gameObject.tag == "Teleport_Target_Two")
        {
            Teleport(ExitPointOne);
        }

		if (col.gameObject.tag == "Goal")
		{
			GameController.GetComponent<GameController>().goalHit = true;
			Goal.GetComponent<AudioSource>().Play();
		}

	}
}

}
