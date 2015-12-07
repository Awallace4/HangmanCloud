using MvcWebRole1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MvcWebRole1
{
    // This class accepts incoming HTTP requests and
    // determines the appropriate action, which could
    // be a hangman move (see Move and Play)
    // or creating a new game (see NewGame and CreateGame).
    [ValidateInput(true)]
    public class BoardController : Controller
    {
        GameModel gameModel;

        // GET: Board/Play
        // Handles the main game display at the start of a turn.
        public ActionResult Play(int gameID, string userMessage)
        {
            var currentUser = GetCurrentUserProfile();
            gameModel = GameModel.GetByID(gameID, currentUser);
            gameModel.UserMessage = userMessage;
            ViewBag.Title = "Hangman";
            return View(gameModel);
        }

        // POST: Board/Move
        // Handles the main game board's form submission which takes
        // place when a move is submitted to the server.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Move(FormCollection formValues)
        {
            int gameId = Int32.Parse( formValues["gameId"]);
            int playerId = Int32.Parse( formValues["playerId"]);
            char guessedLetter = Char.Parse(formValues["guessedLetter"]);

            var move = new WordGridGame.Move(gameId, playerId, guessedLetter);
            var user = GetCurrentUserProfile();
            gameModel = GameModel.GetByID(move.GameID, user);
            System.Diagnostics.Debug.WriteLine("In BoardController.Move(): game model word state is:" + gameModel.WordToFillDisplay);
            string userMessage = gameModel.PlayMove(move);
            
            return RedirectToAction("Play", new { gameId, userMessage });
        }


        // GET: Board/NewGame
        // Allows the user to start a new game.
        public ActionResult NewGame()
        {
            return View();
        }

        // GET: Board/CreateGame
        // Creates a new game with a given opponent.
        public ActionResult CreateGame(int opponentUserId)
        {
            var userProfile = GetCurrentUserProfile();
            var opponentUserProfile = GetUserProfileFromUserId(opponentUserId);
            
            gameModel = GameModel.NewGame(new UserProfile[] { userProfile, opponentUserProfile });
                
            string userMessage = "";
            return RedirectToAction("Play", new { gameModel.GameID, userMessage });
        }

        // Gets the user's profile object given the userId.
        private UserProfile GetUserProfileFromUserId(int userId)
        {
            UsersContext context = new UsersContext();
            var profiles = context.UserProfiles;
            var userProfile = from profile in profiles
                              where profile.UserId == userId
                              select profile;
            return userProfile.FirstOrDefault();
        }

        // Gets the profile for the current user.
        private UserProfile GetCurrentUserProfile()
        {
            UsersContext context = new UsersContext();
            var profiles = context.UserProfiles;

            var userName = User.Identity.Name;
            var userProfile = from profile in profiles
                                where profile.UserName == userName
                                select profile;
            return userProfile.FirstOrDefault();
        }
    }
}
