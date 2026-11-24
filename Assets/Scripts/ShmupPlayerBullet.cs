using UnityEngine;

public class ShmupPlayerBullet : MonoBehaviour
{
    [SerializeField] private float bulletSpeed = 12f;

    [SerializeField] private float bulletLifetime = 2f;
    void Start()
    {
        Destroy(gameObject, bulletLifetime);
    }

    // Update is called once per frame
    void Update()
    {
        transform.position += Vector3.up * bulletSpeed * Time.deltaTime;
    }
}
