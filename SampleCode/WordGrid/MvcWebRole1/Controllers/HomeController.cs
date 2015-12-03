using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MvcWebRole1.Models;

namespace MvcWebRole1.Controllers
{
    [ValidateInput(true)]
    public class HomeController : Controller
    {
        
        public ActionResult Index()
        {
            PlayerModel playerModel = null;
            ViewBag.Message = "A hangman developed in C#/F#/Javascript/MVC and hosted on Windows Azure.";
            playerModel = GetCurrentUserAsPlayer();
            return View(playerModel); 
        }

        public ActionResult About()
        {
            ViewBag.Message = "This game is a hangman game that can be played by two players anywhere on the Internet.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "github.com/Awallace4/HangmanCloud .";

            return View();
        }

        private PlayerModel GetCurrentUserAsPlayer()
        {
            return new PlayerModel(GetCurrentUserProfile());
        }

        private UserProfile GetCurrentUserProfile()
        {
            // For this user, find the UserProfile
            var userName = User.Identity.Name;
            var usersContext = new UsersContext();
            return usersContext.UserProfiles.FirstOrDefault(u => u.UserName.ToLower() == userName.ToLower());
        }
    }
}
