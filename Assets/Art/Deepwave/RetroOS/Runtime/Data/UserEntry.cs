using System;
using UnityEngine;

namespace Deepwave.RetroOS.Data
{
    [Serializable]
    public class UserEntry
    {
        public string username;
        public string password;
        public string displayName;
        public Sprite avatar;
    }
}
