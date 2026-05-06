using UnityEngine;

public class ClearPrefsOnce : MonoBehaviour
{
    private void Start()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("[DEBUG] PlayerPrefs cleared");
    }
}