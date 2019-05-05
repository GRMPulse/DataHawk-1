using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour 
{
    public bool isStart;
    public bool isQuit;

    public GameObject mainLoadScreen;
    public AudioSource menuSelectionSound;
    AudioSource myMenuSelectionSound;

    void Start()
    {
        myMenuSelectionSound = menuSelectionSound.GetComponent<AudioSource>();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnMouseUp()
    {
        if (isStart)
        {
            Instantiate(myMenuSelectionSound);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            //Invoke("LoadMain", 2f);
            LoadMain();
        }
        if (isQuit)
        {
            Application.Quit();
        }
    } 

    void LoadMain()
    {
        //SceneManager.LoadScene("Main");
        mainLoadScreen.SetActive(true);
    }
}
