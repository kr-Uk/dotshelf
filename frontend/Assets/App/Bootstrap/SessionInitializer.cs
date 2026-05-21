using UnityEngine;

namespace App.Bootstrap
{
    public sealed class SessionInitializer : MonoBehaviour
    {
        [SerializeField] private string sessionUserId = "user_me";
        [SerializeField] private string sessionNickname = "bookmori";

        public string SessionUserId => sessionUserId;
        public string SessionNickname => sessionNickname;
    }
}

