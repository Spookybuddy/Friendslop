using UnityEngine;

public class Mirror : MonoBehaviour
{
    public PlayerController player;
    public Camera renderCam;

    public float dist;
    public float dot;

    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        //No player / Too far / Wrong side
        dot = Vector3.Dot(transform.forward, player.head.forward);
        dist = Vector3.Distance(transform.position, player.transform.position);
        if (player == null || dot > 0 || dist > 16) {
            renderCam.enabled = false;
            return;
        } else if (!renderCam.enabled) {
            renderCam.enabled = true;
        }

        renderCam.transform.rotation = Quaternion.LookRotation(Vector3.Reflect(player.head.forward, transform.forward), transform.up);
    }
}