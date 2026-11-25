using UnityEngine;

public class ShmupPlayerMovement : MonoBehaviour
{
    [SerializeField] private Camera shmupCamera;
    [SerializeField] private float moveSpeed = 5f;

    private SpriteRenderer playerSprite;
    private Animator playerAnimator;

    private bool movementEnabled = false;

    private void Awake()
    {
        playerSprite = GetComponent<SpriteRenderer>();
        playerAnimator = GetComponent<Animator>();
    }

    public void BeginMovement()
    {
        movementEnabled = true;
    }

    private void Update()
    {
        if(!movementEnabled || shmupCamera == null)
        {
            return;
        }

        //reads what key the player is pressing 
        Vector2 input = ReadMovementInput();

        if(playerAnimator != null)
        {
            playerAnimator.SetInteger("HorizontalDirection", Mathf.RoundToInt(input.x));
        }

        Vector2 nextPosition = CalculateNextPosition(transform.position, input, moveSpeed, Time.deltaTime);

        //quinn reccommended using render texture
        //store a camera outside of the visibile area 
        //point it at a game and render that onto a texture
       
        //referenced this discussion on how to keep the player within the boundries
        //https://discussions.unity.com/t/keeping-the-player-within-the-boundaries-of-the-camera/537585
        //calculate camera boundries and adjust them using the player's half-size to keep entire sprite visible

        //also a video from wild cockatiel games "How to keep your player within the screen in unity"
        //on how to prevent the sprite from moving partly outside the camera's visible area

        //from unity documentation
        //half the camera's visible height measured from the center to the top or bottom edge
        float halfHeight = shmupCamera.orthographicSize;

        float aspect;
        //calculate the width to height ratio
        //if a render texture exists we use its dimensions otherwise use the camera's normal aspect ratio
        if(shmupCamera.targetTexture != null)
        {
            aspect = (float)shmupCamera.targetTexture.width / shmupCamera.targetTexture.height;
        } 
        else
        {
            aspect = shmupCamera.aspect;
        }

        //calculate half the visible horizontal width
        //this gives the distance from the camera center to either horizontal edge
        float halfWidth = halfHeight * aspect;

        //grab where the camera is positioned 
        //x and y coords are used as the center of the visible area
        Vector3 cameraPosition = shmupCamera.transform.position;

        //get half the player's sprite size in world units
        //prevents the sprite from moving partly outside the camera's visible area
        Vector3 playerExtents;
        if(playerSprite != null)
        {
            playerExtents = playerSprite.bounds.extents;
        }
        else
        {
            playerExtents = Vector3.zero;
        }

        //calculate the left and right movement boundries
        //add player's half-width to the left boundry and subtract it from the right boundry so the sprite stays visible
        float minX = cameraPosition.x - halfWidth + playerExtents.x;
        float maxX = cameraPosition.x + halfWidth - playerExtents.x;

        //calculate top and bottom boundries
        float minY = cameraPosition.y - halfHeight + playerExtents.y;
        float maxY = cameraPosition.y + halfHeight - playerExtents.y;

        //what actually stops the player from exceeding those boundries
        float x = Mathf.Clamp(nextPosition.x, minX, maxX);
        float y = Mathf.Clamp(nextPosition.y, minY, maxY);

        //update the players world position
        transform.position = new Vector3(x,y,transform.position.z);

    }
    
    public static Vector2 CalculateNextPosition(Vector2 currentPosition, Vector2 input, float speed, float deltaTime)
    {
        //player moves faster diagonally due to having a magnitude greater than one
        //https://discussions.unity.com/t/how-can-i-stop-the-player-movement-from-being-too-fast-while-moving-diagonally-in-my-2d-game/234929
        Vector2 direction = Vector2.ClampMagnitude(input, 1f);

        //from bawky's unity 2D player movement mastery
        //calculate the movement distance 
        //direction * speed * elapsed time
        //mathf.max prevents negative values
        return currentPosition + direction * Mathf.Max(0f,speed) * Mathf.Max(0f, deltaTime);
    }

    private Vector2 ReadMovementInput()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            horizontal -= 1f;
        }
        if(Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            horizontal += 1f;
        }

        if(Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            vertical += 1f;
        }

        if(Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            vertical -= 1f;
        }

        //(0,1) up
        //(-1,0) left
        //(1,1) up and right
        //(0,0) still
        return new Vector2(horizontal,vertical);
    }
}