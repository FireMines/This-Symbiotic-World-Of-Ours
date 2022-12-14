using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System;

public class CharacterController2D : MonoBehaviour
{
	[SerializeField] private float m_JumpForce = 400f;							// Amount of force added when the player jumps.
	[Range(0, 1)] [SerializeField] private float m_CrouchSpeed = .36f;			// Amount of maxSpeed applied to crouching movement. 1 = 100%
	[Range(0, .3f)] [SerializeField] private float m_MovementSmoothing = .05f;	// How much to smooth out the movement
	[SerializeField] private bool m_AirControl = false;							// Whether or not a player can steer while jumping;
	[SerializeField] private LayerMask m_WhatIsGround;							// A mask determining what is ground to the character
	[SerializeField] private Transform m_GroundCheck;							// A position marking where to check if the player is grounded.
	[SerializeField] private Transform m_CeilingCheck;							// A position marking where to check for ceilings
	[SerializeField] private Collider2D m_CrouchDisableCollider;				// A collider that will be disabled when crouching

	const float k_GroundedRadius = .2f; // Radius of the overlap circle to determine if grounded
	private bool m_Grounded;            // Whether or not the player is grounded.
	const float k_CeilingRadius = .2f; // Radius of the overlap circle to determine if the player can stand up
	private Rigidbody2D m_Rigidbody2D;
	private bool m_FacingRight = true;  // For determining which way the player is currently facing.
	private Vector3 m_Velocity = Vector3.zero;

	private bool doubleJump = true; // Cant he player double jump? Default, False
	private int extraJumps = 0,
				jumpsLeft = 0;

	[Header("Events")]
	[Space]

	public UnityEvent OnLandEvent;

	[System.Serializable]
	public class BoolEvent : UnityEvent<bool> { }

	public BoolEvent OnCrouchEvent;
	private bool m_wasCrouching = false;

	//"Library" that stores the orbTypes and the amount we collected (Modular, could be expanded for other collectibles)
	public Dictionary<OrbController.Element, int> OrbsCollected = new Dictionary<OrbController.Element, int> ();

    private void Start()
    {
		//Initialize the enum Dictionary (OrbsCollected)
		foreach (OrbController.Element orbType in Enum.GetValues(typeof(OrbController.Element)))
        {
			OrbsCollected.Add(orbType, 0);
		}
    }

	private bool isSwimming=false;
	private float swimmingGravity=0.5f;
	private float defaultGravity=3f;
	private float swimmingLinearDrag=1f;
	private float defaultLinearDrag=0f;
	private float swimmingAngularDrag=1f;
	private float defaultAngularDrag=0.05f;


	private void Awake()
	{
		m_Rigidbody2D = GetComponent<Rigidbody2D>();

		if (OnLandEvent == null)
			OnLandEvent = new UnityEvent();

		if (OnCrouchEvent == null)
			OnCrouchEvent = new BoolEvent();
	}

	private void OnTriggerEnter2D(Collider2D hit){
		if(hit.gameObject.tag =="Water"){
			//if player hits the edge of the water, either he goes from swim->!swim or from !swim->swim
			isSwimming = !isSwimming;
			//makes character stop moving when it hits the water but it looks kinda weird
			//m_Rigidbody2D.velocity = new Vector2(0f, 0f);
			//m_Rigidbody2D.angularVelocity = 0f;
			if(isSwimming){
				//set player gravity to swimmingGravity if the player starts swimming
				m_Rigidbody2D.gravityScale=swimmingGravity;
				m_Rigidbody2D.angularDrag=swimmingAngularDrag;
			}
		}
	}

	private void OnTriggerExit2D(Collider2D hit){
		if(hit.gameObject.tag =="Water"){
			//if player hits the edge of the water, either he goes from swim->!swim or from !swim->swim
			isSwimming = !isSwimming;
			if(!isSwimming){
				m_Rigidbody2D.gravityScale=defaultGravity;
				m_Rigidbody2D.angularDrag=defaultAngularDrag;
			}
		}
	}

	private void FixedUpdate()
	{
		bool wasGrounded = m_Grounded;
		m_Grounded = false;

		// The player is grounded if a circlecast to the groundcheck position hits anything designated as ground
		// This can be done using layers instead but Sample Assets will not overwrite your project settings.
		Collider2D[] colliders = Physics2D.OverlapCircleAll(m_GroundCheck.position, k_GroundedRadius, m_WhatIsGround);
		for (int i = 0; i < colliders.Length; i++)
		{
			if (colliders[i].gameObject != gameObject)
			{
				m_Grounded = true;
				if (!wasGrounded)
					OnLandEvent.Invoke();
			}
		}

		if (m_Grounded && !wasGrounded)
		{
			jumpsLeft = extraJumps+1;
		}
	}


	public void Move(float move, bool crouch, bool jump)
	{
		
		//add downward and upward movement instead of crouch and jump when is swimming
		if(isSwimming&&jump||isSwimming&&Input.GetKeyDown(KeyCode.W)){
			m_Rigidbody2D.AddForce(new Vector2(0f, 100f));
		}else if(isSwimming&&crouch){
			m_Rigidbody2D.AddForce(new Vector2(0f, -50f));
		}else{
			// If crouching, check to see if the character can stand up
			if (!crouch)
			{
				// If the character has a ceiling preventing them from standing up, keep them crouching
				if (Physics2D.OverlapCircle(m_CeilingCheck.position, k_CeilingRadius, m_WhatIsGround))
				{
					crouch = true;
				}
			}

			//only control the player if grounded or airControl is turned on
			if (m_Grounded || m_AirControl)
			{

				// If crouching
				if (crouch)
				{
					if (!m_wasCrouching)
					{
						m_wasCrouching = true;
						OnCrouchEvent.Invoke(true);
					}

					// Reduce the speed by the crouchSpeed multiplier
					move *= m_CrouchSpeed;

					// Disable one of the colliders when crouching
					if (m_CrouchDisableCollider != null)
						m_CrouchDisableCollider.enabled = false;
				} else
				{
					// Enable the collider when not crouching
					if (m_CrouchDisableCollider != null)
						m_CrouchDisableCollider.enabled = true;

					if (m_wasCrouching)
					{
						m_wasCrouching = false;
						OnCrouchEvent.Invoke(false);
					}
				}

				// Move the character by finding the target velocity
				Vector3 targetVelocity = new Vector2(move * 10f, m_Rigidbody2D.velocity.y);
				// And then smoothing it out and applying it to the character
				m_Rigidbody2D.velocity = Vector3.SmoothDamp(m_Rigidbody2D.velocity, targetVelocity, ref m_Velocity, m_MovementSmoothing);

				// If the input is moving the player right and the player is facing left...
				if (move > 0 && !m_FacingRight)
				{
					// ... flip the player.
					Flip();
				}
				// Otherwise if the input is moving the player left and the player is facing right...
				else if (move < 0 && m_FacingRight)
				{
					// ... flip the player.
					Flip();
				}
			}
			
		}
		// If the player should jump...
		if ((m_Grounded || jumpsLeft > 0) && jump && !isSwimming)
		{
			// Add a vertical force to the player.
			m_Grounded = false;

			// SETS the player's y velocity to be our jumpvelocity
			Vector2 velSet = m_Rigidbody2D.velocity;
			velSet.y = m_JumpForce / m_Rigidbody2D.mass;
			velSet.x /= Time.deltaTime;

			Vector2 velAdd = m_Rigidbody2D.velocity;
			velAdd.x /= Time.deltaTime; //keeps the current vel, prevents "chopping"
			velAdd.y /= Time.deltaTime;
			velAdd.y += m_JumpForce / m_Rigidbody2D.mass;

			float lerpFactor = 0.5f; //0: SET the velocity 1: ADD the velocity

			//m_Rigidbody2D.velocity = vel * Time.deltaTime;

			// ADDS the player's jumpvelocity to their current velocity
			//m_Rigidbody2D.AddForce(new Vector2(0f, m_JumpForce));

			m_Rigidbody2D.velocity = (velSet*(1f-lerpFactor) + (velAdd)*(lerpFactor)) * Time.deltaTime;

			jumpsLeft--;
		}
	}


	private void Flip()
	{
		// Switch the way the player is labelled as facing.
		m_FacingRight = !m_FacingRight;

		// Multiply the player's x local scale by -1.
		Vector3 theScale = transform.localScale;
		theScale.x *= -1;
		transform.localScale = theScale;
	}

	public int GetOrbAmount(OrbController.Element orbType) {
		try {
			return OrbsCollected[orbType];
		} 
		catch (KeyNotFoundException) {
			OrbsCollected.Add(orbType, 0);
			return OrbsCollected[orbType];
		}
	}

	public void UpdateOrbAmount(int amount,OrbController.Element orbType) {
		try {
			OrbsCollected[orbType] = amount;
		} 
		catch (KeyNotFoundException) {
			OrbsCollected.Add(orbType, amount);
		}
		updatePowers();
	}

	private void updatePowers()
    {
		//EARTH ABILITIES
		//Abilities tied to the first orb
		if (OrbsCollected[OrbController.Element.Earth] > 0)
        {
			//Double jump
			extraJumps = 1;

			//Abilities tied to the second orb
			if (OrbsCollected[OrbController.Element.Earth] > 1) 
			{
				//Heavy ranged attack (throw a boulder or smth)

				//Abilities tied to the third orb
				if (OrbsCollected[OrbController.Element.Earth] > 2)
                {

					//Glide
                }

			}
		}

		//WATER ABILITIES
		//Abilities tied to the first orb
		if (OrbsCollected[OrbController.Element.Water] > 0)
		{
			//light attack Ranged attack / projectile (water drops)



			//Abilities tied to the second orb
			if (OrbsCollected[OrbController.Element.Water] > 1)
			{

				//Illuminate player (see in the dark)


				//Abilities tied to the third orb
				if (OrbsCollected[OrbController.Element.Water] > 2)
				{
					//Dash / Blink (double tap direction)
				}

			}
		}

		//enable abilities based on the amount of orbs collected
	}
}
