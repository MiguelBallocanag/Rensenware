using System;
using System.Collections.Generic;
using System.Linq;

namespace Deepwave.RetroOS.Core.Auth
{
    // Credential check over a fixed user list.
    public class AuthService
    {
        readonly IReadOnlyList<UserAccount> _users;

        public UserAccount Current { get; private set; }

        public AuthService(IReadOnlyList<UserAccount> users) => _users = users;

        /// <summary>Whether the given credentials sign somebody in.</summary>
        public LoginResult Login(string username, string password)
        {
            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

            if (user == null) return LoginResult.UnknownUser;
            if (user.Password != password) return LoginResult.WrongPassword;

            Current = user;
            return LoginResult.Success;
        }

        public void Logout() => Current = null;
    }
}
