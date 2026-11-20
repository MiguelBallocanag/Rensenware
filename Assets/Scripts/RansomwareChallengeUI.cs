using UnityEngine;

public class RansomWareChallengeUI : MonoBehaviour
{
    [SerializeField] private GameObject introContent;
    [SerializeField] private GameObject gameplayArea;

    public bool HasStarted { get; private set; }

    private void Awake()
    {
        if(introContent != null)
        {
            introContent.SetActive(true);
        }
        if(gameplayArea != null)
        {
            gameplayArea.SetActive(false);
        }

        HasStarted = false;
    }

    public void StartChallenge()
    {
        if (HasStarted)
        {
            return;
        }

        if (introContent == null || gameplayArea == null)
        {
            Debug.LogError("RansomChallengeUI: Missing UI references");
            return;
        }

        HasStarted = true;

        introContent.SetActive(false);

        gameplayArea.SetActive(true);

        Debug.Log("Shoot 'em up challenge started");
    }

}