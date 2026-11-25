using Codice.CM.Client.Differences;
using UnityEngine;

public class PlanetFlyby : MonoBehaviour
{
    [SerializeField] private float moveSpeed = .3f;
    [SerializeField] private float travelDistance = 9f;

    private bool isMoving = false;

    private float distanceTraveled = 0f;

    public void BeginFlyby()
    {
        if(isMoving || distanceTraveled >= travelDistance)
        {
            return;
        }

        isMoving = true;
    }

    private void Update()
    {
        if (!isMoving)
        {
            return;
        }

        float movement = moveSpeed * Time.deltaTime;
        movement = Mathf.Min(movement, travelDistance - distanceTraveled);

        transform.localPosition += Vector3.down * movement;

        distanceTraveled += movement;

        if(distanceTraveled >= travelDistance)
        {
            isMoving = false;
        }
    }
    
}
