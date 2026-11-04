namespace Deepwave.RetroOS.Core.Auth
{
    // A single login identity.
    public class UserAccount
    {
        public string Username { get; }
        public string Password { get; private set; }
        public string DisplayName { get; }

        public UserAccount(string username, string password, string displayName = null)
        {
            Username = username;
            Password = password;
            DisplayName = string.IsNullOrEmpty(displayName) ? username : displayName;
        }

        /// <summary>Changes the password for this session only; the profile asset is untouched.</summary>
        public void SetPassword(string password) => Password = password ?? "";
    }
}
