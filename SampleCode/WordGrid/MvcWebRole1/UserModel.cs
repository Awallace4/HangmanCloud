using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MvcWebRole1.Models;

namespace MvcWebRole1.Models
{
    public class UserModel
    {
        UsersContext context = new UsersContext();
        UserProfile[] profiles;

        public UserModel()
        {
            var userProfiles = context.UserProfiles;
            profiles = userProfiles.ToArray();
        }

        public UserProfile[] Users
        {
            get { return profiles; }
        }
    }
}