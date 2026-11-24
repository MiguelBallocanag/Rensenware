using UnityEngine;

public class ShmupPlayerShooting : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;

    [SerializeField] private Transform firePoint;

    [SerializeField] private float fireInterval = .12f;

    private float nextFireTime = 0f;

    private bool shootingEnabled = false;

    public void BeginShooting()
    {
        shootingEnabled = true;

        nextFireTime = Time.time;

    }

    private void Update()
    {
        if (!shootingEnabled)
        {
            return;
        }
        if (Input.GetKey(KeyCode.Space))
        {
            if(Time.time >= nextFireTime)
            {
                Shoot();

                nextFireTime = Time.time + Mathf.Max(.01f, fireInterval);
            }
        }
    }
    private void Shoot()
    {
        if(bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning("ShmupPlayerShooting: Missing bullet prefab or fire point");
            return;
        }
        Instantiate(bulletPrefab, firePoint.position, Quaternion.identity, transform.parent);
    }
}