using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FinishClimbLedge : MonoBehaviour
{
    public GameObject player;
    Rigidbody rb;
    public Animator playerAnimator;
    private Animator animator;
    public Transform endOfAnim;
    // Start is called before the first frame update
    void Start()
    {
        rb = player.transform.GetComponent<Rigidbody>();
        animator = transform.GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void LedgeClimbOver()
    {
        
        player.GetComponent<WallClimb>().OrientationHitWall = false;
        animator.SetBool("canClimb", false);
        playerAnimator.SetTrigger("LedgeClimbEnd");
        //Vector3 offset = new Vector3(0f, 1.9836f, 1.1042f); //the final position at the end of the animation
        rb.Sleep();
        player.transform.position = endOfAnim.position;//player.transform.position + offset;
        rb.isKinematic = false;
        Invoke("endAnim", 0.2f);
    }

    private void endAnim() => player.GetComponent<WallClimb>().climbAnim = false;
}
