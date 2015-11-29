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
    // be a Word Grid move (see Move and Play), swapping tiles (see Swap)
    // or creating a new game (see NewGame and CreateGame).
    [ValidateInput(true)]
    public class BoardController : Controller
    {

        int x;

        GameModel gameModel;

        // GET: Board/Play
        // Handles the main game board display at the start of a turn.
        public ActionResult Play(int gameID, string userMessage)
        {
            var currentUser = GetCurrentUserProfile();
            gameModel = GameModel.GetByID(gameID, currentUser);
            gameModel.UserMessage = userMessage;
            ViewBag.Title = "Word Grid";
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
            int row = Int32.Parse( formValues["row"]);
            int col = Int32.Parse( formValues["col"]);
            int direction = Int32.Parse(formValues["direction"]);
            string tiles = formValues["tiles"];
            string remainingTiles = formValues["remainingTiles"];
            WordGridGame.Direction directionOfPlay;
            switch (direction)
            {
                case 0: directionOfPlay = WordGridGame.Direction.Across;
                    break;
                case 1: directionOfPlay = WordGridGame.Direction.Down;
                    break;
                case 2: directionOfPlay = WordGridGame.Direction.Single;
                    break;
                default: directionOfPlay = WordGridGame.Direction.Across;
                    break;
            }

            var move = new WordGridGame.Move(gameId, playerId, row, col, directionOfPlay, tiles, remainingTiles);
            var user = GetCurrentUserProfile();
            gameModel = GameModel.GetByID(move.GameID, user);
            string userMessage = gameModel.PlayMove(move);
            
            return RedirectToAction("Play", new { gameId, userMessage });
        }

        // GET: Board/Swap
        // Processes a tile swap move.
        [HttpGet]
        public ActionResult Swap(string gameId, string playerId, string tilesToSwap)
        {
            var user = GetCurrentUserProfile();
            gameModel = GameModel.GetByID(Int32.Parse(gameId), user);
            if (gameModel.UserPlayerID == Int32.Parse(playerId))
            {
                gameModel.SwapTiles(tilesToSwap);
            }
            string userMessage = "You've swapped tiles.";
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
