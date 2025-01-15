using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using DG.Tweening;

public class Playermovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float sprintSpeed = 15f;
    public float crouchSpeed = 3f;
    private float Speed;
    private float groundDrag;
    public float groundDragStart = 2f;
    public float groundDragSlide = 0f;
    public float currentSpeed;

    [Header("Jump")]
    public float jumpForce = 12f;
    public float jumpCooldown = 0.25f;
    public float airAcceleration = 0.4f;
    bool readyToJump = true;

    [Header("Crouch")]
    public float crouchYScale = 0.5f;
    private float startYScale;
    public float crouchChange = 0.005f;
    private float currentYScale;
    private float goalYScale;
    private bool callCrouchDown;
    public float crouchSpeedChange = 0.001f;
    private int crouchState = 0;

    [Header("Slide")]
    public float slideFriction = 60f;
    public float slideThreshold = 10f;
    public float slideEndSpeed = 1f;
    private bool isSliding;
    private bool isSlidingPrevious;
    private bool SlideCheck;
    public float slideTimer = 0.5f;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode crouchKey = KeyCode.LeftControl;
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Ground Check")]
    public float playerHeight = 2f;
    private LayerMask groundMask;
    public bool isGrounded;
    public bool isGroundedPrevious;

    [Header("Slope Handling")]
    public float maxSlopeAngle = 70f;
    private RaycastHit slopeHit;

    [Header("Wall Running")]
    private float wallRunStartSpeed;
    public bool isWallRunning;
    private bool isWallRunningPrevious;
    public float maxWallRunSpeed = 9999999f;
    public float minWallRunSpeed = 10f;
    public float exitWallSpeedTimer;

    public Transform orientation;
 
    private float horizontalInput;
    private float verticalInput;
    public ChangeCam cameraScript;
    public Mousemovement playerCam;

    Vector3 moveDirection;
    Rigidbody rb;

    [Header("Animation")]
    public Transform rotationParent;
    public Animator animator;

    void Awake(){
        groundMask = LayerMask.GetMask("groundMask");
        orientation = GameObject.Find("Orientation").gameObject.transform;
        cameraScript = GameObject.Find("CameraMonitor").GetComponent<ChangeCam>();
        playerCam = GameObject.Find("PlayerCam").GetComponent<Mousemovement>();
    }
    void Start(){
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        startYScale = transform.localScale.y;
        isSliding = false;
        isWallRunning = false;
        SlideCheck = false;
        groundDrag = groundDragStart;
        rb.AddForce(Vector3.right * 0.001f, ForceMode.Impulse);
        isGroundedPrevious = true;
    }
    
    void Update(){
        //check for wall and limit speed
        isGrounded = Physics.Raycast(transform.position, Vector3.down, out var kys, playerHeight * 0.5f + 0.2f, groundMask);
        StateHandler();
        MyInput();
        if(!SlideCheck) LimitSpeed();
        currentSpeed = (new Vector3(rb.velocity.x, 0f, rb.velocity.z)).magnitude;
        float forwardSpeed = Vector3.Dot(rb.velocity, orientation.forward);
        animator.SetFloat("forward", forwardSpeed);

        if(isSliding){
            groundDrag = groundDragSlide;
        }else{
            groundDrag = groundDragStart;
        }
        
        if(isGrounded){
            rb.drag = groundDrag;
        }else{
            rb.drag = 0;
        }

        if(isSliding && (crouchState == 0 || !isGrounded)) isSliding = false;

        //Camera Effects
        if(isGrounded && !isGroundedPrevious){
            Debug.Log("Landed" + Time.time);
            animator.SetBool("Jump", false);
            playerCam.CameraShake();
        }
        if(isSliding && !isSlidingPrevious){
            playerCam.DoFov(playerCam.SlideFov, playerCam.SlideTransitionSpeed);
        }
        if(!isSliding && isSlidingPrevious){
            playerCam.DoFov(playerCam.StartFov, playerCam.SlideTransitionSpeed);
        }
        if(!isWallRunning && isWallRunningPrevious){
            playerCam.DoFov(playerCam.StartFov, playerCam.WallRunTransitionSpeed);
            playerCam.DoTilt(0, playerCam.WallRunTransitionSpeed);
        }

        isWallRunningPrevious = isWallRunning;
        isGroundedPrevious = isGrounded;
        isSlidingPrevious = isSliding;
    }

    void FixedUpdate(){
        MovePlayer();
    }

    void MyInput(){
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        if(Input.GetKey(jumpKey) && readyToJump && isGrounded){
            Invoke(nameof(DisableSlide), 0.1f);
            Invoke(nameof(StopSlideAccelerate), 0.1f);
            if(crouchState == 1){
                goalYScale = startYScale;
                currentYScale = crouchYScale;
                CrouchHandlerUp();
            }
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        //start crouch
        if (isGrounded && (Input.GetKeyDown(crouchKey)|| callCrouchDown)){
            callCrouchDown = false;
            goalYScale = crouchYScale;
            currentYScale = startYScale;
            CrouchHandlerDown();
        }else if (Input.GetKeyDown(crouchKey) && !isGrounded){
            callCrouchDown = true;
        }

        //stop crouch
        if (Input.GetKeyUp(crouchKey) && isGrounded){
            isSliding = false;
            SlideCheck = false;
            goalYScale = startYScale;
            currentYScale = crouchYScale;
            CrouchHandlerUp();
        }
        
    }

    void MovePlayer(){
        if(cameraScript.CamMode == 0){
            moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;
        }else{
            Vector3 cameraForward = new Vector3(cameraScript.thirdCam.transform.forward.x, 0f, cameraScript.thirdCam.transform.forward.z).normalized;
            Vector3 cameraRight = new Vector3(cameraScript.thirdCam.transform.right.x, 0f, cameraScript.thirdCam.transform.right.z).normalized;
            moveDirection = cameraForward * verticalInput + cameraRight * horizontalInput;
            if (moveDirection != Vector3.zero) {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                //orientation.DORotateQuaternion(targetRotation, 0.5f); // Interpolate rotation over 0.5 seconds}
                rotationParent.transform.DORotateQuaternion(targetRotation, 0.5f);
            }
        }

        //slope handling
        if (OnSlope())
        {
            rb.AddForce(GetSlopeMoveDirection() * Speed * 10f, ForceMode.Force);
        }

        //ground and air handling
        if(isGrounded){
            rb.AddForce(moveDirection.normalized * Speed * 10f, ForceMode.Force);
        }else if(!isGrounded){
            rb.AddForce(moveDirection.normalized * Speed * 10f * airAcceleration, ForceMode.Force);
        }
    }

    void LimitSpeed(){
        if(exitWallSpeedTimer > 0){
            exitWallSpeedTimer -= Time.deltaTime;
            return;
        }

        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if(isWallRunning){
            if(!isWallRunningPrevious) wallRunStartSpeed = Math.Min(Math.Max(horizontalVel.magnitude, minWallRunSpeed), maxWallRunSpeed);

                Vector3 limitedVel = horizontalVel.normalized * wallRunStartSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        }
        else if(isSliding || !isGrounded)
        {
                float reductionFactor = (horizontalVel.magnitude - (slideFriction * Time.deltaTime)) / horizontalVel.magnitude;
                Vector3 limitedVel = horizontalVel * reductionFactor;
                if(double.IsNaN(limitedVel.z))limitedVel.x = 0.001f;
                if(double.IsNaN(limitedVel.z))limitedVel.z = 0.001f;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
                if (limitedVel.magnitude < slideEndSpeed) isSliding = false;
        }
        else if(!isSliding && horizontalVel.magnitude > Speed)
        {
            Vector3 limitedVel = horizontalVel.normalized * Speed;
            rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        }
        
    }

    void Jump(){
        animator.SetBool("Jump", true);
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    void ResetJump(){
        readyToJump = true;
    }

    void StateHandler(){
        if (!isGrounded)
        {
            //in air
            Speed = sprintSpeed;
        }
        else if (Input.GetKey(crouchKey))
        {
            //player is crouching
            Speed = crouchSpeed;
            //Check for slide
            if (isGrounded)
            {
                Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                if (horizontalVel.magnitude > slideThreshold)
                {
                    if (!isSliding)
                    {
                        SlideCheck = true;
                        Invoke(nameof(StopSlideAccelerate), slideTimer);
                    }
                    isSliding = true;
                }
            }
        }
        else if (Input.GetKey(sprintKey))
        {
            //player is sprinting
            Speed = sprintSpeed;
        }
        else
        {
            //player is walking
            Speed = moveSpeed;
        }
    }
    
    void CrouchHandlerDown(){
        crouchState = 1;
        if (currentYScale < goalYScale)
            currentYScale = goalYScale;
        else
            Invoke(nameof(CrouchHandlerDown), crouchSpeedChange);

        transform.localScale = new Vector3(transform.localScale.x, currentYScale, transform.localScale.z);
        //transform.Translate(Vector3.down * ((currentYScale - goalYScale) / 2));
        currentYScale -= crouchChange;
    }

    void CrouchHandlerUp()
    {
        crouchState = 0;
        if (currentYScale > goalYScale)
            currentYScale = goalYScale;
        else
            Invoke(nameof(CrouchHandlerUp), crouchSpeedChange);

        transform.localScale = new Vector3(transform.localScale.x, currentYScale, transform.localScale.z);
        currentYScale += crouchChange;
    }

    void StopSlideAccelerate()
    {
        SlideCheck = false;
    }

    private bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight / 2 + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    private Vector3 GetSlopeMoveDirection(){
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }

    private void DisableSlide()
    {
        isSliding = false;
    }
}
