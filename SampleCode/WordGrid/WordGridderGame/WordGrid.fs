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

// This is a type provider for the SQL Database that contains all the game information.
//type sqldata = Microsoft.FSharp.Data.TypeProviders.SqlDataConnection< @"Data Source=(LocalDb)\v11.0;AttachDbFilename=C:\myfiles\WordGrid1\MvcWebRole1\App_Data\aspnet-MvcWebRole1-20120917184036.mdf;Initial Catalog=aspnet-MvcWebRole1-20120917184036;Integrated Security=True",
//                                                                      StoredProcedures=true>
type sqldata = SqlDataConnection<ConnectionStringName = "DefaultConnection", ConfigFile = @"Web.config">

// Constants.
module WordGrid =

    [<Literal>]
    let BoardSize = 13

    [<Literal>]
    let MaxTilesInRack = 6

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

// Represents the direction of play, Across or Down. Single is used when there
// is only one tile played.
type Direction =
    | Across = 0
    | Down = 1
    | Single = 2

type GameState =
    | PreGame = 0
    | FirstMove = 1
    | InProgress = 2
    | GameOver = 3

// Represents a WordGrid tile.
[<AllowNullLiteral>]
type Tile(letter : Letter, blankLetter : Letter option) =

    // For a played tile, the second parameter can be a letter when the first parameter is Letter.Blank
    static let letterChar = 
       [| '_'; 'A'; 'B'; 'C'; 'D'; 'E'; 'F'; 'G'; 'H'; 'I'; 'J'; 'K'; 'L'; 'M'; 'N'; 'O'; 'P'; 'Q'; 'R'; 'S'; 'T'; 'U'; 'V'; 'W'; 'X'; 'Y'; 'Z' |]

    static let pointValues =
       [| 0; 1; 3; 4; 2; 1; 4; 2; 4; 1; 8; 5; 1; 3; 1; 1; 3; 10; 1; 1; 1; 1; 5; 4; 8; 4; 10 |]

    let letterValue = letter

    let pointValue = 
        pointValues.[(int) letterValue]
 

    member this.LetterChar = letterChar.[int letterValue]
    member this.LetterValue = letterValue
    member this.PointValue = pointValue        
    member val BlankLetter : Letter option = blankLetter with get, set

    static member FromString(tiles : string) =
        Seq.map(fun (elem : char) -> new Tile(elem)) tiles
        |> Seq.toList

    new(letter : Letter) =
        new Tile(letter, None)
        
    new(ch : char) =
        if (ch =  '_') then Tile(Letter.Blank) else
            if (ch >= 'a' && ch <= 'z') then
                // This represents a blank tile that has been played as a letter.
                Tile( Letter.Blank, Some(enum<Letter> (int ch - int 'a' + 1 )))
            else
                Tile( enum<Letter> (int ch - int 'A' + 1))


// Represents a player in an active game and manages the state for that player,
// including the score and tile rack.
type Player(userId, gameId, playerId, name, score, tiles) =

    // The userId that the player uses to log in.
    member val UserId = userId

    // The database Id of the game
    member val GameId = gameId

    // The database ID of the player
    member val PlayerId = playerId

    // The list of tiles in the player's rack
    member val Tiles = tiles with get, set

    // The player's score.
    member val Score = score with get, set

    // The name of the player.
    member val Name = name

    // Returns the tiles as a System.Collections.Generic.List instead of an F# list.
    member this.GetTilesAsList() =
        let returnList = new System.Collections.Generic.List<Tile>()
        for tile in this.Tiles do
            returnList.Add(tile);
        returnList

    // Sets the player's tiles from a System.Collections.Generic.List.
    member this.TilesFromList(tileList : System.Collections.Generic.List<_>) =
        this.Tiles <- List.ofSeq tileList        

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
        | [] -> Player(player.UserId, gameId, player.Id, player.UserName, 0, List.empty)
        | [ playerState ] -> Player(player.UserId, gameId, player.Id, player.UserName, playerState.Score, Tile.FromString (playerState.Tiles))
        | _ -> raise (new System.InvalidOperationException()); Player(0, 0, 0, "", 0, List.empty)

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
                Player(userId, 0, player.Id, userName, 0, List.empty)
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
type Move(gameId : int, playerId : int, row : int, col : int,
          dir : Direction, tiles : string, remainingTiles : string) =
      // The id from the database for the game this move is a part of.
      member val GameID = gameId

      // The player who is making this move.
      member val PlayerID = playerId

      // The row of the first tile played in this move.
      member val Row = row

      // The col of the first tile played in this move.
      member val Col = col

      // The direction of the move.
      member val Direction = dir

      // The tiles played for this move. This does not include any previously
      // played tiles that are part of the words formed from the play.
      member val Tiles = Tile.FromString(tiles)

      // The tiles that the user has remaining in the rack after the play.
      member val RemainingTiles = Tile.FromString(remainingTiles)
    
// A utility module for swapping elements of arrays.
module Swap =
    let swap (array1:array<_>) (i:int) (j:int) =
        let temp = array1.[i];
        array1.[i] <- array1.[j];
        array1.[j] <- temp;

    let swap2D (array2 : _ [] []) (i1:int) (j1:int) (i2:int) (j2:int) =
        let temp = array2.[i1].[j1]
        array2.[i1].[j1] <- array2.[i2].[j2]
        array2.[i2].[j2] <- temp

// Represents the Word Grid game board, including its layout and current state
// of played tiles.
type GameBoard(boardIn) =
    let mutable board : Tile option [][] =
        Array.init (Array.length boardIn) (fun index -> Array.copy boardIn.[index])

    let spaces : int [][] =
         [|
            [| 4; 0; 0; 1; 0; 0; 4; 0; 0; 1; 0; 0; 4 |];
            [| 0; 2; 0; 0; 3; 0; 0; 0; 3; 0; 0; 2; 0; |];
            [| 0; 0; 2; 0; 0; 1; 0; 1; 0; 0; 2; 0; 0; |];
            [| 1; 0; 0; 2; 0; 0; 1; 0; 0; 2; 0; 0; 1; |];
            [| 0; 3; 0; 0; 1; 0; 0; 0; 1; 0; 0; 3; 0; |];
            [| 0; 0; 1; 0; 0; 1; 0; 1; 0; 0; 1; 0; 0; |];
            [| 4; 0; 0; 1; 0; 0; 2; 0; 0; 1; 0; 0; 4; |];
            [| 0; 0; 1; 0; 0; 1; 0; 1; 0; 0; 1; 0; 0; |];
            [| 0; 3; 0; 0; 1; 0; 0; 0; 1; 0; 0; 3; 0; |]; 
            [| 1; 0; 0; 2; 0; 0; 1; 0; 0; 2; 0; 0; 1; |];
            [| 0; 0; 2; 0; 0; 1; 0; 1; 0; 0; 2; 0; 0; |];
            [| 0; 2; 0; 0; 3; 0; 0; 0; 3; 0; 0; 2; 0; |];
            [| 4; 0; 0; 1; 0; 0; 4; 0; 0; 1; 0; 0; 4; |];
        |]

    // This constructor creates a board with no played tiles.
    new() =
        GameBoard(GameBoard.Empty)

    // Creates a new board with no played tiles.
    static member Empty =
        Array.create WordGrid.BoardSize (Array.create WordGrid.BoardSize None)

    // Gets the tile at a specific location, or None if the space is empty.
    member this.Item(row, col) =
        board.[row].[col]

    // Gets the tile at a specific location, or null if the space is empty.
    // This is useful if calling from C# so you don't have to work with FSharpOption.
    member this.ItemOrNull(row, col) =
        match board.[row].[col] with
        | Some item -> item
        | None -> null

    // Creates a deep copy of the game board.
    member this.Copy() =
        new GameBoard(board)

   // Adds a move to the board.            
    member this.AddMove(move : Move) =
        let mutable row = move.Row
        let mutable col = move.Col
        match move.Direction with
        | Direction.Across | Direction.Single ->
            for tile in move.Tiles do
                let mutable occupiedSpace = true
                while (occupiedSpace) do
                    match board.[row].[col] with
                    | None -> occupiedSpace <- false   
                    | Some(otherTile) -> col <- col + 1
                if (row < WordGrid.BoardSize && col < WordGrid.BoardSize) then
                    board.[row].[col] <- Some(tile)
                else
                    raise InvalidMoveException
                col <- col + 1
        | Direction.Down ->
            for tile in move.Tiles do
                 let mutable occupiedSpace = true
                 while (occupiedSpace) do
                    match board.[row].[col] with
                    | None -> occupiedSpace <- false   
                    | Some(otherTile) -> row <- row + 1
                 if (row < WordGrid.BoardSize && col < WordGrid.BoardSize) then
                     board.[row].[col] <- Some(tile)
                 else
                    raise InvalidMoveException
                 row <- row + 1
        | _ -> raise (new System.InvalidOperationException())

        // Transposes the board, reversing across and down.
        // Used to simplify scoring and searching for plays.
        member this.Transpose() =
            for row in 0 .. WordGrid.BoardSize - 1 do
                for col in row + 1 .. WordGrid.BoardSize - 1 do
                   Swap.swap2D board row col col row

        // Returns the rows on this board.
        member this.Rows =
            board

        // Get the space type for this location.
        member this.GetSpace(row, col) =
            enum<SpaceType> (spaces.[row].[col])

        // Convert a text string into a game board. Text strings are used in the database to store
        // the board state, so this method is called whenever the game is loaded from the database.
        static member FromString(boardIn:string) = 
            Array.init WordGrid.BoardSize (fun row -> Array.init WordGrid.BoardSize (fun col ->
                match boardIn.Chars(row*WordGrid.BoardSize + col) with
                | ch when ch = ' ' -> None
                // Capital letters represent non-blank tiles
                | ch when ch >= char 'A' && ch <= char 'Z' -> Some(new Tile(enum<Letter> (int ch - int 'A' + 1)))
                // Lowercase letters represent blank tiles played as a given letter
                | ch when ch >= char 'a' && ch <= char 'z' -> Some(new Tile(ch))
                | _ -> raise (new System.InvalidOperationException())))

        // Convert a board state to a text string in order to save it in the database.
        member this.AsString =
                 let charArray =
                     seq { for row in 0 .. WordGrid.BoardSize - 1 do
                               for col in 0 .. WordGrid.BoardSize - 1 do
                                   match board.[row].[col] with
                                   | None -> yield ' '
                                   | Some(tile) -> yield (match tile.LetterValue with
                                                          | Letter.Blank -> char (int (tile.BlankLetter.Value) + int 'a' - 1)
                                                          | _ -> tile.LetterChar)
                     }
                     |> Seq.toArray
                 new System.String(charArray)

// Represents a WordGrid game, including the players, state, tile bag, and board layout.
type Game( id, name, players : Player[], gameState : GameState,
              tileBagIn : Tile list, boardLayout, currentPlayerPosition) =

    // Represents the game board with all committed plays.
    let mutable gameBoard = new GameBoard(boardLayout)

    // Represents the proposed game board including the current play.
    let mutable newBoard : GameBoard = new GameBoard()

    // Used to randomize the tile bag.
    let random = new System.Random()

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
    let lookupQueue = queueClient.GetQueueReference("lookup")
    let resultsQueue = queueClient.GetQueueReference("results")
    do lookupQueue.CreateIfNotExist() |> ignore
    do resultsQueue.CreateIfNotExist() |> ignore

    let mutable nextId = 0

    // Fisher Yates shuffle algorithm
    static member private Shuffle array1 =
        let random = new System.Random()
        for i in 0 .. Array.length array1 - 1 do
            // swap with another random element
            let randomElement = (int) (random.NextDouble() * float ( Array.length array1))
            Swap.swap array1 i randomElement
        array1

    // The data context which is used to get at the type provider's types.
    static member DataContext = sqldata.GetDataContext()

    // The number of moves made so far.
    member val MoveCount = 0 with get, set

    // The current player as a number that indicates what position they are playing.
    // For a two-player game, this is 0 or 1. The position is used as the array index
    // in the Players array.
    member val CurrentPlayerPosition = currentPlayerPosition with get, set

    // The name of the current game.
    member val Name = name

    // The tile bag, which starts with 100 tiles.
    member val TileBag = tileBagIn with get, set

    // The state of the game as a enumeration type. Possible values include
    // PreGame, FirstMove, InProgress, GameOver.
    member val State = gameState with get, set

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
        Game( gameFromDb.Id, gameFromDb.Name, players, enum<GameState> gameFromDb.GameState,
              Game.FromString gameFromDb.Tilebag, GameBoard.FromString (gameFromDb.BoardLayout), currentPlayerPosition)

    // This constructor is used to create a brand new game.
    new(players : Player []) =
        let gameName = players.[0].Name + " VS " + players.[1].Name

        let tilePopulation =
            [| 2; 8; 2; 2; 4; 9; 2; 3; 2; 8; 1; 1; 4; 2; 6; 8; 2; 1; 6; 4; 5; 3; 1; 2; 1; 2; 1 |]

        let mutable tileBag =
            [| for i in 0 .. 26 do
                    for j in 0 .. tilePopulation.[i] - 1 do
                        let letter = enum<Letter>(i);
                        yield Tile(letter)
            |] |> Game.Shuffle |> Array.toList
 
        Game.DataContext.Connection.Open()
        
        // Make sure all the players exist in the database.
        for player in players do
            Game.DataContext.SpAddPlayer(Util.nullable player.UserId, player.Name) |> ignore
        let board = (new GameBoard()).AsString
        let stringTileBag = Game.AsString tileBag
        let results = Game.DataContext.SpCreateGame(gameName, board, stringTileBag)
                      |> List.ofSeq
        assert(results.Length = 1)
        let gameId = results.Head.Id.Value

        Array.iteri (fun index (player:Player) ->
            Game.DataContext.SpAddPlayerToGame(Util.nullable gameId, Util.nullable player.PlayerId, Util.nullable index) |> ignore) players
        Game(gameId)

    // Transforms the tilebag into a string to save in the database.
    static member AsString(tilebag) =
              let charArray =
                  List.map (fun (tile:Tile) -> tile.LetterChar) tilebag
                  |> List.toArray
              new System.String(charArray)

    // Reads the tile bag from a string. This is used to recreate the tilebag after loading the string
    // from the database.
    static member FromString (tilebagString:string ) =
            List.ofSeq tilebagString
            |> List.map (fun tile -> if tile = '_' then
                                         new Tile(Letter.Blank)
                                     else
                                         new Tile(enum<Letter> (int(tile) - int('A') + 1)))

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


    // Given a list of all the words played, look them up in the dictionary
    // Return a list of tuples, with the word and whether it was found
    member this.CheckWords words =
#if SKIPDICTIONARY
        List.init (List.length words) (fun _ -> true)
        |> List.zip words
#else
        let now = System.DateTime.Now
        let messageHeader = now.ToLongTimeString()
                            + " " + now.ToLongDateString()
                            + " " + this.Players.[this.CurrentPlayerPosition].PlayerId.ToString()
                            + " " + this.GameId.ToString()
        let message = new CloudQueueMessage(String.concat "\n" (messageHeader :: words))
        lookupQueue.AddMessage(message)
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
                        let result = split |> Array.toList |> List.tail 
                        Some (result)
                    else
                        None)
            match result with
            | Some x -> x
            | None -> findResults (n + 1)

        let result = findResults 0

        result
        |> List.map (fun (elem : string)-> elem.Split([|' '|]))
        |> List.map( fun (elem :string []) -> (elem.[0], elem.[1].ToLower() = "true"))
#endif
            
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

    member this.CheckWordsAsync words =
        async {
            let now = System.DateTime.Now
            let messageHeader = now.ToLongTimeString()
                                + " " + now.ToLongDateString()
                                + " " + this.Players.[this.CurrentPlayerPosition].PlayerId.ToString()
                                + " " + this.GameId.ToString()
            let message = new CloudQueueMessage(String.concat "\n" (messageHeader :: words))

            do! lookupQueue.AddMessageAsync(message)
            let rec findResponse messageHeader n =
                async {
                    let! response = this.TryFindResponse(messageHeader)
                    let result =
                        match response with
                        | Some value -> async { return value }
                        | None -> findResponse messageHeader (n + 1)
                    return! result
                }
            let! result = findResponse messageHeader 0
            return result
                    |> List.map (fun (elem : string)-> elem.Split([|' '|]))
                    |> List.map( fun (elem :string []) -> (elem.[0], elem.[1].ToLower() = "true"))
        }

    // Determine if this move is legal and calculate its score.
    member this.CheckMove (move : Move) =
        let mutable cumScore = 0
        let mutable wordScore = 0
        let mutable wordMultiplier = 1
        let mutable tilesPlayed = 0
        let mutable isLegalFirstMove = false
        let mutable row = move.Row
        let mutable col = move.Col
        let mutable mainWord = new System.Text.StringBuilder("")
        let mutable wordsPlayed : string list  = List.empty

        let mutable direction = move.Direction;

        // If only one tile is played, the value of direction is "Direction.Single"
        // We then have to decide whether to treat this as a down or across play.
        if (direction = Direction.Single) then
            let tilesInRow =
                seq {
                 if (move.Col > 0 && newBoard.[move.Row, move.Col - 1].IsSome) then
                     yield newBoard.[move.Row, move.Col - 1].Value
                 if (move.Col < WordGrid.BoardSize && newBoard.[move.Row, move.Col + 1].IsSome) then
                     yield newBoard.[move.Row, move.Col + 1].Value
                     }
            let tilesInCol =
                seq {
                 if (move.Row > 0 && newBoard.[move.Row - 1, move.Col].IsSome) then
                     yield newBoard.[move.Row - 1, move.Col].Value
                 if (move.Row < WordGrid.BoardSize && newBoard.[move.Row + 1, move.Col].IsSome) then
                     yield newBoard.[move.Row  + 1, move.Col].Value
                     } 
            // If there are more adjacent tiles in the same column as in the same row,
            // then treat it as a "down" move.
            if (Seq.length tilesInRow < Seq.length tilesInCol) then
                direction <- Direction.Down
            else
                direction <- Direction.Across
        
        // Transpose the board so that all our calculations assume a
        // play across the board, not down.
        if (direction = Direction.Down) then
            gameBoard.Transpose()
            newBoard.Transpose()
            let temp = row;
            row <- col;
            col <- temp;

        // Find the start of the main word being played.
        while (col >= 0 && newBoard.[row, col] <> None) do
            col <- col - 1
        col <- col + 1

        while (row < WordGrid.BoardSize && col < WordGrid.BoardSize && (newBoard.[row, col] <> None)) do
            let mutable crossWordMultiplier = 1
            let mutable letterMultiplier = 1
            mainWord <- if (newBoard.[row, col].Value.LetterValue = Letter.Blank) then
                            mainWord.Append(newBoard.[row, col].Value.BlankLetter.Value)
                        else
                            mainWord.Append(newBoard.[row, col].Value.LetterChar)

            // If the board square on the old board was empty, this is a played tile
            // otherwise it's a tile that was already on the board.
            if (gameBoard.[row, col] = None) then
                tilesPlayed <- tilesPlayed + 1
                match gameBoard.GetSpace(row, col) with
                | SpaceType.DLS -> letterMultiplier <- 2
                | SpaceType.DWS -> crossWordMultiplier <- 2
                                   wordMultiplier <- 2
                | SpaceType.TLS -> letterMultiplier <- 3
                | SpaceType.TWS -> crossWordMultiplier <- 3
                                   wordMultiplier <- 3
                | _ -> ()

                // Identify crosswords by starting at the first played letter
                // and counting adjacent tiles in both directions.

                let mutable rowCrossBegin = row
                let mutable rowCross = row
                let mutable crossScore = 0

                // Find the beginning of the crossword (if any) that crosses the current square.
                while (rowCrossBegin >= 0 && newBoard.[rowCrossBegin, col] <> None) do
                   rowCrossBegin <- rowCrossBegin - 1
                rowCrossBegin <- rowCrossBegin + 1

                // Now scan forward along the cross word.
                let mutable rowCrossEnd = row;

                while (rowCrossEnd < WordGrid.BoardSize && newBoard.[rowCrossEnd, col] <> None) do
                    rowCrossEnd <- rowCrossEnd + 1
                rowCrossEnd <- rowCrossEnd - 1

                if (rowCrossBegin <> rowCrossEnd) then
                    // A crossword was found.
                    // Parse out the word and score it.
                    let mutable crossWord : System.Text.StringBuilder = new System.Text.StringBuilder("")
                    for rowCross in rowCrossBegin .. rowCrossEnd do
                        crossWord <- if (newBoard.[rowCross, col].Value.LetterValue = Letter.Blank) then
                                         crossWord.Append(newBoard.[rowCross, col].Value.BlankLetter.Value)
                                     else
                                         crossWord.Append(newBoard.[rowCross, col].Value.LetterChar)
                        if (rowCross = row) then
                           crossScore <- crossScore + newBoard.[rowCross, col].Value.PointValue * letterMultiplier
                        else
                           crossScore <- crossScore + newBoard.[rowCross, col].Value.PointValue
                    
                    wordsPlayed <- crossWord.ToString() :: wordsPlayed
                    crossScore <- crossScore * crossWordMultiplier
                    cumScore <- cumScore + crossScore

            wordScore <- wordScore + letterMultiplier * newBoard.[row, col].Value.PointValue
            col <- col + 1


        wordsPlayed <- mainWord.ToString() :: wordsPlayed
        wordScore <- wordScore * wordMultiplier
        cumScore <- cumScore + wordScore

        // Bonus for playing all tiles.
        if (tilesPlayed = WordGrid.MaxTilesInRack) then
            cumScore <- cumScore + 50

        // If the direction was originally "Down", transpose the board again.
        if (direction = Direction.Down) then
            gameBoard.Transpose()
            newBoard.Transpose()

        // The return value is a tuple of three elements, with information about
        // the legality of the move, the score, and the validity of each of the words played.
        let results = this.CheckWords(wordsPlayed)
        let isLegalMove = List.forall (fun (word, exists) -> exists) results
        isLegalMove, cumScore, results

        member this.Board = gameBoard

        // Returns a tile as an option, or None if no tiles remain in the bag.
        // Updates the tile bag.
        member this.DrawTile () =
           match this.TileBag with
           | [] -> None
           | head :: tail -> this.TileBag <- tail; Some(head)

        // Fills up the player's rack after a play by drawing from the tile bag.
        member this.DrawTiles(playerPosition : int) =
           let player = this.Players.[playerPosition];
           while (player.Tiles.Length < WordGrid.MaxTilesInRack && this.TileBag.Length > 0) do
               match this.DrawTile() with
               | Some tile -> player.Tiles <- tile :: player.Tiles
               | None -> ()

        // Returns the number of tiles remaining in the tile bag.
        member this.RemainingTilesInBag with get() = this.TileBag.Length

        // Process a tile swap, which is when a player puts tiles back into the bag
        // and draws new ones.
        member this.SwapTiles(playerId, tilesToSwap) =
            let tiles = Game.FromString tilesToSwap
            let player = Array.find (fun (player:Player) -> player.PlayerId = playerId) this.Players
            let mutable tileList = System.Collections.Generic.List<Tile>(player.Tiles)
            for tile in tiles do
                // Find the tile in the tileList
                let findTile = List.tryFind (fun (playerTile : Tile) -> tile.LetterValue = playerTile.LetterValue) player.Tiles
                match findTile with
                | Some(tile) ->
                    if (tileList.Remove(tile) = false) then
                        raise (new System.InvalidOperationException())
                | None -> raise (new System.InvalidOperationException())
            player.Tiles <- List.ofSeq (tileList)
            assert(players.[this.CurrentPlayerPosition] = player)
            this.TileBag <- this.TileBag |> List.toArray |> Game.Shuffle |> Array.toList
            this.DrawTiles(this.CurrentPlayerPosition)
            this.MoveCount <- this.MoveCount + 1
                    
            // Update the database tables: Games, PlayerState, Plays
            Game.DataContext.SpUpdateGame(Util.nullable this.GameId,
                                        Util.nullable player.PlayerId,
                                        Util.nullable this.MoveCount,
                                        Game.AsString player.Tiles,
                                        "-",
                                        gameBoard.AsString,
                                        Game.AsString this.TileBag,
                                        Util.nullable player.Score,
                                        Util.nullable (int this.State),
                                        Util.nullable ((this.CurrentPlayerPosition + 1) % players.Length))
            |> ignore
            this.CurrentPlayerPosition <- (this.CurrentPlayerPosition + 1) % players.Length

        // Adjusts point totals at the end of a game. The point value of any unplayed tiles is
        // subtracted from a player's score.  If you use all your tiles, you get a bonus, the total
        // point value of your opponents' unplayed tiles.
        member this.EndGameScoreAdjust(endbonus) =
           let mutable bonus = 0
           for player in this.Players do
               let mutable pointDeduction = 0
               if (player.Tiles.Length > 0) then
                   for tile in player.Tiles do
                       pointDeduction <- pointDeduction + tile.PointValue
               player.Score <- player.Score - pointDeduction
               bonus <- bonus + pointDeduction
           if endbonus then
               this.Players.[this.CurrentPlayerPosition].Score <- 
                    this.Players.[this.CurrentPlayerPosition].Score + bonus
           ()

        // Rejects a move when one or more words are not in the dictionary.
        // TODO implement this function. This should indicate to the player
        // which words were not valid.
        member this.RejectMove(invalidWords:string list) =
            match invalidWords with
            | [] -> "Internal error in Word Grid server."
            | [word] -> System.String.Format("Sorry, {0} is not in the dictionary.", word)
            | [first; second] -> System.String.Format("Sorry, {0} and {1} are not in the dictionary.", first, second)
            | head :: tail -> System.String.Format("Sorry, the words {0}, and {1} are not in the dictionary.", String.concat ", " tail, head)

        // Process a move submitted by a player.
        member this.ProcessMove(playerId : int, move) =
            newBoard <- gameBoard.Copy()
            newBoard.AddMove(move)
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
                gameBoard <- newBoard.Copy()
                this.State <- GameState.InProgress
                // current player should already be known!
                let player = players.[this.CurrentPlayerPosition]
                player.Score <- player.Score + score
                // replace the player tiles with the list of remaining tiles
                player.Tiles <- Seq.toList move.RemainingTiles
                if (List.isEmpty this.TileBag && player.Tiles.Length = 0) then
                    // The game is over
                    this.MoveCount <- this.MoveCount + 1
                    this.State <- GameState.GameOver
                    this.EndGameScoreAdjust(true)
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
                    this.DrawTiles(this.CurrentPlayerPosition)
                    
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
            for playerPosition in 0 .. this.Players.Length - 1 do
                this.DrawTiles(playerPosition)
                this.Players.[playerPosition].Score <- 0
            
       
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
           