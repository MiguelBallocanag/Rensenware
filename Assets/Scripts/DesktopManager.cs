using UnityEngine;

public class DesktopManager : MonoBehaviour
{
    [SerializeField] private GameObject mailWindow;
    void Start()
    {
     //mail starts closed when the game boots.
     mailWindow.SetActive(false);   
    }

    public void OpenMail()
    {
        mailWindow.SetActive(true);
    }

    public void CloseMail()
    {
        mailWindow.SetActive(false);
    }

}
