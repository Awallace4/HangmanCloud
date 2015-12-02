namespace WordGridGame

open System.Collections.Generic
open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.StorageClient
open Microsoft.WindowsAzure.ServiceRuntime
open FsAzureHelper
open FsAzureHelper.FsAzureHelpers
open Microsoft.FSharp.Data.TypeProviders
open Microsoft.FSharp.Linq.NullableOperators
open System.Data
open System.Data.SqlClient

// TODO: go through this entire file, rip out unneeded stuff, figure out what we need to change/add
// and rename the file/namespace/etc
 
// This is a type provider for the SQL Database that contains all the game information.
//type sqldata = Microsoft.FSharp.Data.TypeProviders.SqlDataConnection< @"Data Source=(LocalDb)\v11.0;AttachDbFilename=C:\myfiles\WordGrid1\MvcWebRole1\App_Data\aspnet-MvcWebRole1-20120917184036.mdf;Initial Catalog=aspnet-MvcWebRole1-20120917184036;Integrated Security=True",
//                                                                      StoredProcedures=true>
type sqldata = SqlDataConnection<ConnectionStringName = "DefaultConnection", ConfigFile = @"Web.config">


// Utility functions.
module Util =

    let nullable value =
        new System.Nullable<_>(value)

// Represents the type of a WordGrid tile.
type Letter = 
    | Blank = 0
    | A = 1
    | B = 2
    | C = 3
    | D = 4
    | E = 5
    | F = 6
    | G = 7
    | H = 8
    | I = 9
    | J = 10
    | K = 11
    | L = 12
    | M = 13
    | N = 14
    | O = 15
    | P = 16
    | Q = 17
    | R = 18
    | S = 19
    | T = 20
    | U = 21
    | V = 22
    | W = 23
    | X = 24
    | Y = 25
    | Z = 26

// Represents the type of a WordGrid board space.
type SpaceType =
    | Normal = 0
    | DLS = 1
    | DWS = 2
    | TLS = 3
    | TWS = 4
    // | Center = 5

type GameState =
    | PreGame = 0
    | FirstMove = 1
    | InProgress = 2
    | GameOver = 3

type HangManState =
    | Empty = 0
    | Head = 1
    | Body = 2
    | LeftArm = 3
    | RightArm = 4
    | LeftLeg = 5
    | Dead = 6

type WordGenerator =
    // Get a random word from the dictionary
    // TODO: this needs to be static, outside of Game type, so we can call it from the
    // Game constructor.
    static member GenerateRandomWord(gameName : string, generateQueue : CloudQueue, resultsQueue : CloudQueue) =
        let now = System.DateTime.Now
        let messageHeader = now.ToLongTimeString()
                            + " " + now.ToLongDateString()
                            + " " + gameName
        let message = new CloudQueueMessage(messageHeader)
        generateQueue.AddMessage(message)
        let rec findResults n = 
            // Wait for a return message.
            System.Threading.Thread.Sleep(1000)
            let result =
                resultsQueue.GetMessages(32)
                |> Seq.tryPick(fun message ->
                    let results = message.AsString
                    let split = results.Split([|'\n'|])
                    if (split.[0] = messageHeader) then
                        resultsQueue.DeleteMessage(message.Id, message.PopReceipt)
                        let result = split.[1]
                        Some (result)
                    else
                        None)
            match result with
            | Some x -> x
            | None -> findResults (n + 1)

        let result = findResults 0

        result


// Represents a player in an active game and manages the state for that player,
// including the score and tile rack.
type Player(userId, gameId, playerId, name) =

    // The userId that the player uses to log in.
    member val UserId = userId

    // The database Id of the game
    member val GameId = gameId

    // The database ID of the player
    member val PlayerId = playerId

    // The name of the player.
    member val Name = name  

    // This constructor is used when creating players for a game when the players
    // already exist.
    new(gameId, playerId) =
        // Get the player from the database
        let dataContext = sqldata.GetDataContext()
        let player =
            query { 
                for player in dataContext.Players do
                where (player.Id = playerId)
                select player
            }
            |> Seq.exactlyOne

        let playerStateResults =
            query {
                for playerState in dataContext.PlayerState do
                where (playerState.GameID = gameId && playerState.PlayerID = playerId)
                select playerState
                }
            |> Seq.toList

        match playerStateResults with
        | [] -> Player(player.UserId, gameId, player.Id, player.UserName)
        | [ playerState ] -> Player(player.UserId, gameId, player.Id, player.UserName)
        | _ -> raise (new System.InvalidOperationException()); Player(0, 0, 0, "")

    // This constructor is used when creating a player at the start of a game. If the player has
    // played previous games, there is a record for them in the database. If not, a new one is created.
    static member FindOrCreate(userId, userName) : Player =

        // Look up the player and create if he/she doesn't exist.
        // Get the player from the database
        let dataContext = sqldata.GetDataContext()
        let players =
            query { 
                for player in dataContext.Players do
                where (player.UserId = userId && player.UserName = userName)
                select player
            }
            |> Seq.toList
        match players with
        | [] -> // no player was found, so create
                let sqlText = System.String.Format("INSERT INTO Players VALUES ('{0}', '{1}')", userName, userId)
                dataContext.DataContext.ExecuteCommand(sqlText) |> ignore
                Player.FindOrCreate(userId, userName)
        | [ player ] -> // one player was found
                Player(userId, 0, player.Id, userName)
        | _ -> // multiple players found: error case
                raise (new System.Exception("Duplicate player found."))

    // Gets all the games the player is currently playing by their ID.
    member this.GetGameIDs(myTurnOnly : bool) =
        let dataContext = sqldata.GetDataContext()
        if (myTurnOnly) then
            query {
                for game in dataContext.Games do
                join gamePlayer in dataContext.GamePlayers on (game.Id = gamePlayer.GameId)
                where (game.GameState <> int GameState.GameOver &&
                       gamePlayer.PlayerId = this.PlayerId &&
                       gamePlayer.Position =? game.CurrentPlayerPosition)
                select (game.Id)
                }
                |> Seq.toArray
        else
            query {
                for game in dataContext.Games do
                join gamePlayer in dataContext.GamePlayers on (game.Id = gamePlayer.GameId)
                where (game.GameState <> int GameState.GameOver &&
                       gamePlayer.PlayerId = this.PlayerId )
                select (game.Id)
                }
                |> Seq.toArray

    // Get the names of the games the player is currently playing.
    member this.GetGameNames(myTurnOnly : bool) =
        let dataContext = sqldata.GetDataContext()
        if (myTurnOnly) then
            query {
                for game in dataContext.Games do
                join gamePlayer in dataContext.GamePlayers on (game.Id = gamePlayer.GameId)
                where (game.GameState <> int GameState.GameOver &&
                       gamePlayer.PlayerId = this.PlayerId &&
                       gamePlayer.Position =? game.CurrentPlayerPosition)
                select (game.Name)
                }
                |> Seq.toArray    
        else
            query {
                for game in dataContext.Games do
                join gamePlayer in dataContext.GamePlayers on (game.Id = gamePlayer.GameId)
                where (game.GameState <> int GameState.GameOver &&
                       gamePlayer.PlayerId = this.PlayerId)
                select (game.Name)
                }
                |> Seq.toArray

// This is used when an invalid move is attempted.
exception InvalidMoveException;

// Represents the play for a single turn in a WordGrid game.
type Move(gameId : int, playerId : int, guessedLetter : char) =
      // The id from the database for the game this move is a part of.
      member val GameID = gameId

      // The player who is making this move.
      member val PlayerID = playerId

      // The letter that the player guessed
      member val GuessedLetter = guessedLetter

// Represents the Word Grid game board, including its layout and current state
// of played tiles.
type WordState(wordToGuess : string) =
    let length = wordToGuess.Length

    // TODO: we need to handle spaces in the words carefully.

    let mutable wordToFill : char [] =
        Array.init (length) (fun i -> if wordToGuess.Chars(i) = ' ' then ' ' else '_')

    // This constructor creates a board with no played tiles.
    new(wordToGuess) =
        WordState(wordToGuess)

    // TODO: this should be temporary...
    new() = 
        WordState("YOUMUSTPOPULATETHISWORD")

    // Gets the letter at a specific location, or None if the space is empty.
    member this.Letter(index) =
        wordToFill.[index]

    // Creates a deep copy of the word state.
    member this.Copy() =
        new WordState(wordToGuess)

   // Adds a letter to the board.            
    member this.AddLetter(move : Move) =
        let  guessedLetter = move.GuessedLetter
        this.AddLetter(move.GuessedLetter)

    member this.AddLetter(letter : char) =
        let guessedLetter = letter
        for i = 0 to length do
            // if guessedLetter = letter, put the letter at the same spot in wordToFill
            // TODO: add array containing letters that have been guessed, and
            // check this array before looping???
            if (guessedLetter = wordToGuess.[i]) then
                Array.set wordToFill i guessedLetter
                

    // Returns the current state of the word to fill
    member this.WordToFill =
        wordToFill

    // returns char array containing letters of secret word
    member this.WordToGuess = 
        wordToGuess

    // Convert a text string into a word to fill. Text strings are used in the database to store
    // the word state, so this method is called whenever the game is loaded from the database.
    static member FromString(wordStateIn:string) = 
        let newLength = wordStateIn.Length / 2
        let newState = new WordState(wordStateIn.Substring(0, newLength))
            
        for i = newLength to wordStateIn.Length do
            newState.AddLetter(wordStateIn.Chars(i))
        newState

    // Convert a word state to a text string in order to save it in the database.
    member this.AsString =
        let charArray = Array.init (2*length) (fun index -> '_')
        // copy wordToGuess into the first half of the array
        for i = 0 to length - 1 do
            Array.set charArray i wordToGuess.[i]
        // copy wordToFill into the second half of the array
        for i = 0 to length-1 do
            Array.set charArray (i+length) wordToFill.[i]
        // create string out of resulting array
        new System.String(charArray)

// Represents a hangman game, including the players, state, tile bag, and board layout.
type Game( id, name, players : Player[], wordToGuess : string, 
            wordState : WordState, state : GameState, hangManState : HangManState, currentPlayerPosition) =

    // Represents the game board with all committed plays.
    let mutable wordState = new WordState(wordToGuess)

    // Represents the proposed game state including the current play.
    let mutable newState : WordState = new WordState()

    let mutable hangManState = hangManState 



    let mutable nextId = 0

    // The data context which is used to get at the type provider's types.
    static member DataContext = sqldata.GetDataContext()

    // The number of moves made so far.
    member val MoveCount = 0 with get, set

    // The current player as a number that indicates what position they are playing.
    // For a two-player game, this is 0 or 1. The position is used as the array index
    // in the Players array.
    member val CurrentPlayerPosition = currentPlayerPosition with get, set
    member val AvailableLetters = [| 'A'; 'B'; 'C'; 'D'; 'E'; 'F'; 'G'; 'H'; 'I'; 'J'; 'K'; 'L'; 'M'; 'N'; 'O'; 'P'; 'Q'; 'R'; 'S'; 'T'; 'U'; 'V'; 'W'; 'X'; 'Y'; 'Z' |]
    // The name of the current game.
    member val Name = name

    // The state of the game as a enumeration type. Possible values include
    // PreGame, FirstMove, InProgress, GameOver.
    member val State = state with get, set

    // This constructor is used to load a game from the database, given the game Id.
    new(gameId) =
        let gameFromDb =
            query {
                for game in Game.DataContext.Games do
                where (game.Id = gameId)
                select game
                }
            |> Seq.exactlyOne
        // Construct the game object from the database table
        new Game(gameFromDb)

    // This constructor is used to load a game from the database given the game as a provided type.
    new(gameFromDb) =
        let dataContext = Game.DataContext;
        let players = query { for player in dataContext.Players do
                                  join gamePlayer in dataContext.GamePlayers on (player.Id = gamePlayer.PlayerId)
                                  where (gamePlayer.GameId = gameFromDb.Id)
                                  select player.Id }
                          |> Seq.map (fun playerId -> new Player(gameFromDb.Id, playerId))
                          |> Seq.toArray
        let currentPlayerPosition = gameFromDb.CurrentPlayerPosition.GetValueOrDefault()
        Game( gameFromDb.Id, gameFromDb.Name, players, gameFromDb.WordToGuess, 
             WordState.FromString(gameFromDb.WordState), enum<GameState> gameFromDb.GameState, 
             enum<HangManState> gameFromDb.HangManState, currentPlayerPosition)

    // This constructor is used to create a brand new game.
    new(players : Player []) =
        // TODO: move to FsAzureHelper project.
        do CloudStorageAccount.SetConfigurationSettingPublisher(new System.Action<_, _>(fun configName configSetter  ->
                          // Provide the configSetter with the initial value
                          configSetter.Invoke( RoleEnvironment.GetConfigurationSettingValue( configName ) ) |> ignore
                          RoleEnvironment.Changed.AddHandler( new System.EventHandler<_>(fun sender arg ->
                            arg.Changes
                            |> Seq.toList
                            |> List.filter (fun change -> change :? RoleEnvironmentConfigurationSettingChange)
                            |> List.map (fun change -> change :?> RoleEnvironmentConfigurationSettingChange)
                        |> List.filter (fun change -> change.ConfigurationSettingName = configName && 
                                                          not (configSetter.Invoke( RoleEnvironment.GetConfigurationSettingValue(configName))))
                            |> List.iter (fun change ->
                                // In this case, the change to the storage account credentials in the
                                // service configuration is significant enough that the role needs to be
                                // recycled in order to use the latest settings (for example, the 
                                // endpoint may have changed)
                                RoleEnvironment.RequestRecycle())))))

        let storageAccount = CloudStorageAccount.FromConfigurationSetting("StorageConnectionString")
        let queueClient = storageAccount.CreateCloudQueueClient()
        let generateQueue = queueClient.GetQueueReference("generate")
        let resultsQueue = queueClient.GetQueueReference("results")
        do generateQueue.CreateIfNotExist() |> ignore
        do resultsQueue.CreateIfNotExist() |> ignore
        let gameName = players.[0].Name + " VS " + players.[1].Name
 
        Game.DataContext.Connection.Open()
        
        // Make sure all the players exist in the database.
        for player in players do
            Game.DataContext.SpAddPlayer(Util.nullable player.UserId, player.Name) |> ignore
        let wordToFill = (new WordState()).AsString
        // TODO: need to generate random word here.
        let wordToGuess = WordGenerator.GenerateRandomWord(gameName, generateQueue, resultsQueue)
        let guessedLetters = ""

        // TODO: THIS PROC NEEDS TO BE REWORKED
        // this proc now takes parameters name, wordToGuess, wordToFill, guessedLetter
        let results = Game.DataContext.SpCreateGame(gameName, wordToGuess, wordToFill, guessedLetters)
                      |> List.ofSeq
        assert(results.Length = 1)
        let gameId = results.Head.Id.Value

        Array.iteri (fun index (player:Player) ->
            Game.DataContext.SpAddPlayerToGame(Util.nullable gameId, Util.nullable player.PlayerId, Util.nullable index) |> ignore) players
        Game(gameId)

    // This is the Id of the game, which is also the primary key in the database.
    member this.GameId = id

    // An array of the players.  CurrentPlayerPosition is used as the index.
    member this.Players = players

    // Gets the type provider type for one of the players from the database.
    member this.GetPlayerById(playerId) =
        query {
           for player in players do
           where (player.PlayerId = playerId)
           select player
           }
        |> Seq.exactlyOne

            
    member this.TryFindResponse(messageHeader) =
        async {
            let! results = resultsQueue.GetMessagesAsync(32)
            let response = results
                           |> Seq.tryPick (fun message ->
                                    let results = message.AsString
                                    let split = results.Split([|'\n'|])
                                    if (split.[0] = messageHeader) then
                                        Async.Start <| resultsQueue.DeleteMessageAsync(message.Id, message.PopReceipt)
                                        let result = split |> Array.toList |> List.tail
                                        Some(result)
                                    else
                                        None
                                    )
            return response
        }


    // Rejects a move when one or more words are not in the dictionary.
    // TODO: do we need this -- could rewrite for notifying when user tries a letter that's already been used?
    member this.RejectMove(invalidWords:string list) =
        match invalidWords with
        | [] -> "Internal error in Word Grid server."
        | [word] -> System.String.Format("Sorry, {0} is not in the dictionary.", word)
        | [first; second] -> System.String.Format("Sorry, {0} and {1} are not in the dictionary.", first, second)
        | head :: tail -> System.String.Format("Sorry, the words {0}, and {1} are not in the dictionary.", String.concat ", " tail, head)

    // Process a move submitted by a player.
    // TODO: rewrite this to handle hangman stuff
    member this.ProcessMove(playerId : int, move : Move) =
        newState <- wordState.Copy()
        newState.AddLetter(move)
        // TODO: check if letter has already been tried -- this determines legality of move
        let isLegalMove, score, wordsResults = this.CheckMove(move)
        let mainWord, _ = List.head wordsResults
        if (not isLegalMove) then
            // Identify invalid words
            let invalidWords =
                List.filter (fun (_, isValid) -> isValid = false) wordsResults
                |> List.map (fun (word, _) -> word)

            // Return invalid words to the player, reject move.
            this.RejectMove(invalidWords)
        else
            // Legal move. Commit it, check for game over, draw tiles, update current player's turn.
            wordState <- newState.Copy()
            this.State <- GameState.InProgress
            // current player should already be known!
            let player = players.[this.CurrentPlayerPosition]

            // TODO: if hangman is dead or word is solved, game is over
            if (List.isEmpty this.TileBag && player.Tiles.Length = 0) then
                // The game is over
                this.MoveCount <- this.MoveCount + 1
                this.State <- GameState.GameOver
                this.EndGameScoreAdjust(true)
                // Update the database tables: Games, PlayerState, Plays
                   
                // TODO: this proc now takes parameters gameId, playerId, moveNumber, 
                // guessedLetter, guessedLetters, wordToFill, hangmanState, gameState, and currentPlayerPosition.
                Game.DataContext.SpUpdateGame(Util.nullable this.GameId,
                                            Util.nullable player.PlayerId,
                                            Util.nullable this.MoveCount,
                                            Game.AsString player.Tiles,
                                            mainWord,
                                            gameBoard.AsString,
                                            Game.AsString this.TileBag,
                                            Util.nullable player.Score,
                                            Util.nullable (int this.State),
                                            Util.nullable (-1))
                            |> ignore

                Game.DataContext.Connection.Open()
                for player in this.Players do
                    let commandText = System.String.Format("UPDATE PlayerState SET PlayerState.Score={0} WHERE PlayerState.GameID={1} AND PlayerState.PlayerId={2}",
                                                    player.Score, this.GameId, player.PlayerId)
                    
                    Game.DataContext.DataContext.ExecuteCommand(commandText)
                    |> ignore
                Game.DataContext.Connection.Close()
                System.String.Format("Game over! Your final score is {0}.", player.Score)
            else
                this.MoveCount <- this.MoveCount + 1
                    
                // Update the database tables: Games, PlayerState, Plays
                Game.DataContext.SpUpdateGame(Util.nullable this.GameId,
                                            Util.nullable player.PlayerId,
                                            Util.nullable this.MoveCount,
                                            Game.AsString player.Tiles,
                                            mainWord,
                                            gameBoard.AsString,
                                            Game.AsString this.TileBag,
                                            Util.nullable player.Score,
                                            Util.nullable (int this.State),
                                            Util.nullable ((this.CurrentPlayerPosition + 1) % players.Length))
                |> ignore
                this.CurrentPlayerPosition <- (this.CurrentPlayerPosition + 1) % players.Length
                System.String.Format("You played {0} for {1} points.", mainWord, score )

    // Start a game by drawing tiles for each player and updating the initial game state in the database.
    member this.StartGame() =            
       
        Game.DataContext.Connection.Open()
        let commandText = System.String.Format("UPDATE Games SET Games.Name = '{0}', Games.GameState = {1}, Games.BoardLayout = '{2}', Games.Tilebag = '{3}'" +
                                                "WHERE Games.Id = {4}",
                                                this.Name, int GameState.FirstMove, this.Board.AsString, Game.AsString this.TileBag, this.GameId)
        Game.DataContext.DataContext.ExecuteCommand(commandText) |> ignore;
        for player in this.Players do
            let commandText = System.String.Format("INSERT INTO PlayerState VALUES('{0}', '{1}', '{2}', '{3}')",
                                                    Game.AsString player.Tiles, player.Score, player.PlayerId, this.GameId);
            Game.DataContext.DataContext.ExecuteCommand(commandText) |> ignore;
            Game.DataContext.Connection.Close()
           