using UnityEngine;

public class DesktopManager : MonoBehaviour{
    [SerializeField] private GameObject mailWindow;
    [SerializeField] private GameObject photosWindow;

    void Start(){
        if(mailWindow != null){
            mailWindow.SetActive(false);
        }
        if (photosWindow !=null){
            photosWindow.SetActive(false);
        }
    }
    public void OpenMail(){
        if(mailWindow != null){
            mailWindow.SetActive(true);
        }
    }

    public void CloseMail(){
        if(mailWindow != null){
            mailWindow.SetActive(false);
        }
    }
    public void OpenPhotos(){
        if(photosWindow != null){
            photosWindow.SetActive(true);
        }
    }
    public void ClosePhotos(){
        if (photosWindow != null){
            photosWindow.SetActive(false);
        }
    }
}
