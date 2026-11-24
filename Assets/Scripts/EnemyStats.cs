using UnityEngine;
public class EnemyStats : MonoBehaviour{
    public int health = 100;
    public int damage = 10;
    public float speed = 3f;

    public void TakeDamage (int amount){
        health -= amount;
        if(health <= 0){
            health = 0;
            //removes enemy when its health reaches zero
            Destroy(gameObject);
        }
    }
}