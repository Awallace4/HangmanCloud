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
        public const int MaxTilesInRack = WordGridGame.WordGrid.MaxTilesInRack;
        public const int BoardSize = WordGridGame.WordGrid.BoardSize;
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

    // Represents the tile rack in a Word Grid game.
    public class TileRack
    {
        //int numberOfTiles;
        Tile[] tilesInRack = new Tile[26];

        public TileRack(List<Tile> tiles)
        {
            var count = 0;
            foreach (Tile tile in tiles)
            {
                tilesInRack[count++] = tile;
            }
        }

        public Tile[] Tiles
        {
            get { return tilesInRack; }
        }

        public void UpdateRack(List<Tile> tiles)
        {
            var count = 0;
            foreach (Tile tile in tiles)
            {
                tilesInRack[count++] = tile;
            }
        }

    }

    // Represents a WordGrid board space, with or without a tile.
    // Used in the rendering of a cell in CSHTML.
    public class BoardCell
    {
        Tile tile;
        SpaceType space;
        int cellID;

        // These images are used for the spaces on the board.
        string[] imageFiles = { 
            "/images/spaces/empty.png" };

        // Construct a board cell.
        public BoardCell(Tile tileIn, SpaceType spaceIn, int cellIDIn)
        {
            tile = tileIn;
            space = spaceIn;
            cellID = cellIDIn;
        }

        // Returns the tile at a given board space, or null if the space is empty.
        public Tile Tile
        {
            get
            {
                return tile;
            }
        }

        // Returns the space type at this cell.
        public SpaceType Space
        {
            get
            {
                return space;
            }
        }

        // Returns the tile letter at this space, or null if the space is empty.
        public char Letter
        {
            get
            {
                if (tile == null)
                    return '_';
                return tile.LetterChar;
            }
        }

        // Returns the ID of the cell, which is row*BoardSize + col.
        public string CellID
        {
            get
            {
                return "space" + cellID.ToString();
            }
        }

        // Returns the image filename for this space or tile.
        public string ImagePath
        {
            get
            {
                if (! this.IsEmpty)
                {
                    if (tile.LetterValue == WordGridGame.Letter.Blank)
                    {
                        // TODO: Indicate that this is a blank somehow
                        return "/images/tiles/Tile_" + tile.BlankLetter.Value + "_Blank.png";
                    }
                    else
                    {
                        return "/images/tiles/Tile_" + tile.LetterChar + ".png";
                    }
                }
                return imageFiles[(int) space];
            }
        }

        // Returns true if there is no tile at this space.
        public bool IsEmpty
        {
            get
            {
                if (tile == null)
                    return true;
                else
                    return false;
            }
        }

        // Returns the HTML class name for this cell, which is "spaceimage" or a space,
        // or "tileOnBoard" for a space filled with a tile.
        public string ClassName
        {
            get
            {
                if (this.IsEmpty)
                {
                    return "spaceimage";
                }
                else
                {
                    return "tileOnBoard";
                }
            }
        }

        // Returns the point value of the tile at the current board space, or 0
        // if there is no tile. Does not factor in the bonus due to the space type.
        public int PointValue
        {
            get
            {
                if (this.IsEmpty)
                {
                    return 0;
                }
                else
                {
                    return tile.PointValue;
                }
            }
        }
    }

    // Represents a row of tiles on the board. Used in the rendering of a table row in CSHTML.
    public class BoardRow
    {
        Game game;
        int row;

        // Constructs a row.
        public BoardRow(Game gameIn, int rowIn)
        {
            BoardCell[] cells = new BoardCell[Constants.BoardSize];
            game = gameIn;
            row = rowIn;
            GameBoard board = game.Board;
            for (int col = 0; col < cells.Length; col++)
            {
                cells[col] = new BoardCell(board.ItemOrNull(row, col), board.GetSpace(row, col), row*WordGrid.BoardSize + col);
            }
        }

        // Return the cells in this row.
        public BoardCell[] Cells
        {
            get
            {
                BoardCell[] cells = new BoardCell[Constants.BoardSize];
                GameBoard board = game.Board;
                for (int col = 0; col < cells.Length; col++)
                {
                    cells[col] = new BoardCell(board.ItemOrNull(row, col), board.GetSpace(row, col), row * WordGrid.BoardSize + col);
                }
                return cells;
            }
        }

    }

    // Represents a player's WordGrid session, including the game they are currently
    // playing, and all its elements.
    public class GameModel
    {
        Game game;
        BoardRow[] rows;
        TileRack rack;
        Player currentPlayer;
        UserProfile currentUser;
        int userPlayerId;

        // From a Game object, and the current user, construct the information needed
        // to render a particular play layout for a user's game.
        public GameModel(Game gameIn, UserProfile currentUserIn)
        {
            game = gameIn;
            currentUser = currentUserIn;
            rows = new BoardRow[Constants.BoardSize];
            for (int row = 0; row < Constants.BoardSize; row++)
            {
                rows[row] = new BoardRow(game, row);
            }

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
            List<Tile> tiles = currentPlayer.GetTilesAsList();
            rack = new TileRack(tiles);
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

        // Gets the rows for the HTML table to display this game.
        public BoardRow[] Rows
        {
            get { return rows; }
        }

        // Gets a specific row in the HTML table for this game.
        public BoardRow GetRow(int row)
        {
            return rows[row];
        }

        // Gets the current player's tile rack.
        public TileRack Rack
        {
            get { return rack; }
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
        public Player Winner
        {
            get
            {
                if (game.State != GameState.GameOver)
                {
                    return null;
                }
                Player winner = null;
                foreach (Player player in this.Players)
                {
                    if (winner != null && player.Score > winner.Score)
                    {
                        winner = player;
                    }
                }
                return winner;
            }
        }

        // Attempts to play a move. Processes the move sent from the client.
        public string PlayMove(Move move)
        {
            string userMessage = game.ProcessMove(userPlayerId, move);
            rack.UpdateRack(game.GetPlayerById(userPlayerId).GetTilesAsList());
            return userMessage;
        }

        // Processes a tile swap play.
        public void SwapTiles(string tilesToSwap)
        {
            game.SwapTiles(userPlayerId, tilesToSwap);
            rack.UpdateRack(game.GetPlayerById(userPlayerId).GetTilesAsList());
        }

        // Returns the number of tiles remaining in the tile bag.
        public int RemainingTilesInBag
        {
            get
            {
                return game.RemainingTilesInBag;
            }
        }
    }

}