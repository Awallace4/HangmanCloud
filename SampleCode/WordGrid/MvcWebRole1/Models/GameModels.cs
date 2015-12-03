using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WordGridGame;

namespace MvcWebRole1.Models
{
    // Some hard coded constants.
    public class Constants
    {
        //public const int MaxTilesInRack = WordGridGame.WordGrid.MaxTilesInRack;
        //public const int BoardSize = WordGridGame.WordGrid.BoardSize;
    }

    // Represents a player in a WordGrid game.
    public class PlayerModel
    {
        Player player;

        public int PlayerID { get { return player.PlayerId; } }
        public UserProfile Profile;

        public PlayerModel(UserProfile userProfile)
        {
            Profile = userProfile;
            player = Player.FindOrCreate(userProfile.UserId, userProfile.UserName);
        }

        public int[] GetAllGameIDs()
        {
            return player.GetGameIDs(false);
            
        }
        public string[] GetAllGameNames()
        {
            return player.GetGameNames(false);
        }

        public int[] GetMyTurnGameIDs()
        {
            return player.GetGameIDs(true);
        }
        public string[] GetMyTurnGameNames()
        {
            return player.GetGameNames(true);
        }
    }






    // Represents a player's Hangman session, including the game they are currently
    // playing, and all its elements.
    public class GameModel
    {
        Game game;
        Player currentPlayer;
        UserProfile currentUser;
        int userPlayerId;

        // From a Game object, and the current user, construct the information needed
        // to render a particular play layout for a user's game.
        public GameModel(Game gameIn, UserProfile currentUserIn)
        {
            game = gameIn;
            currentUser = currentUserIn;

            // Find out the Player object and
            // playerId of the current user
            int playerId = 0;
            bool found = false;
            int index = 0;
            int indexOfCurrentPlayer = 0;
            while (! found) 
            {
                Player player = game.Players[index];
                if (player.UserId == currentUser.UserId)
                {
                    playerId = player.PlayerId;
                    indexOfCurrentPlayer = index;
                    found = true;
                }
                index++;
            }
            if (!found)
                throw new InvalidOperationException();

            currentPlayer = game.Players[indexOfCurrentPlayer];
            userPlayerId = playerId;
        }

        // Create a brand new game given an array of user profiles who
        // will be the players.
        public static GameModel NewGame(UserProfile[] playerProfiles)
        {

            Player[] players = new Player[2];
            int count = 0;
            foreach (UserProfile profile in playerProfiles)
            {      
                players[count++] = Player.FindOrCreate(profile.UserId, profile.UserName);
            }
            Game game = new Game(players);
            game.StartGame();
            return new GameModel(game, playerProfiles[0]);
        }

        // Retrieves a game already in progress.
        public static GameModel GetByID(int id, UserProfile currentUser)
        {
            Game game = new Game(id);
            return new GameModel(game, currentUser);
        }

        // Gets the ID from the database for the current game.
        public int GameID
        {
            get { return game.GameId; }
        }

        // Gets the array of players for this game.
        public Player[] Players
        { 
            get { return game.Players; }
        }

        // Gets the playerID from the database for the player whose
        // turn it is currently.
        public int CurrentTurnPlayerID
        { 
            get 
            {
                if (game.CurrentPlayerPosition == -1)
                {
                    return -1;
                }
                else
                    return game.Players[game.CurrentPlayerPosition].PlayerId; 
            } 
        }

        // Gets the playerID for the user who is currently viewing this game.
        public int UserPlayerID
        {
            get { return userPlayerId; }
        }

        // True if the user who is currently viewing this game is the current player.
        public bool IsUsersTurn
        {
            get { return (this.CurrentTurnPlayerID == this.UserPlayerID); }
        }

        // Gets the game state.
        public GameState State
        {
            get { return game.State; }
        }

        public string UserMessage;

        // Returns the player who is the winner of this game, if it's over.
        // If it's not over, returns null.
        // TODO: rewrite me maybe... do we even care about the winner?
        public Player Winner
        {
            get
            {
                if (game.State != GameState.GameOver)
                {
                    return null;
                }
                Player winner = this.Players[0];
                //foreach (Player player in this.Players)
                //{
                //    if (winner != null && player.Score > winner.Score)
                //    {
                //        winner = player;
                //    }
                //}
                return winner;
            }
        }

        // Attempts to play a move. Processes the move sent from the client.
        public string PlayMove(Move move)
        {
            string userMessage = game.ProcessMove(userPlayerId, move);
            return userMessage;
        }
    }

}