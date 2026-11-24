using UnityEngine;
public class PlayerStats : MonoBehaviour{
    public int health = 100;
    public int damage = 10;
    public float speed = 5f;

    public void TakeDamage(int amount){
        health -= amount;
        if(health <= 0){
            health =0;
            Debug.Log("Player Defeated.");
        }
    }
    public void Heal(int amount){ 
        health += amount;
    }
}